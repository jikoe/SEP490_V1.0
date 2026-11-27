using System.Net;
using AgriSustain.IntegrationTests.UT1X;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-FAR-07 Submit Certificate Dossier (Farmer) against SQL Server and private dossier storage.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-FAR-07")]
public sealed class FarmCertificateApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static MultipartFormDataContent Certificate(string type = "VietGAP", string number = "VG-2026-777",
        string expiry = "2028-01-10", bool withFile = true)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(type), "certificateType" },
            { new StringContent(number), "certificateNumber" },
            { new StringContent("Sở NN&PTNT Lâm Đồng"), "issuedBy" },
            { new StringContent("2026-01-10"), "issueDate" },
            { new StringContent(expiry), "expiryDate" },
        };
        if (withFile) form.Add(DuApiHost.Pdf(), "file", "vietgap.pdf");
        return form;
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FAR-07 #01] An approved farm's certificate is saved as PendingReview with its file and audit entry")]
    public async Task Submit_Valid_PendingReview()
    {
        var response = await _api.FarmerA().PostAsync("api/farmer/certificates", Certificate());

        var json = await ApiAssert.Status(response, HttpStatusCode.OK);
        Assert.Equal("PendingReview", json.GetProperty("certificate").GetProperty("verificationStatus").GetString());
        await using var db = _api.Open();
        var certificate = await db.FarmCertificates.SingleAsync(c => c.CertificateNumber == "VG-2026-777");
        Assert.Equal((_api.FarmAId, "VietGAP", "PendingReview", new DateOnly(2028, 1, 10)),
            (certificate.FarmerProfileId, certificate.CertificateType, certificate.VerificationStatus, certificate.ExpiryDate));
        string stored = Path.Combine(_api.ContentRoot, "private-files", "dossiers", "certificates",
            _api.FarmerAId.ToString(), Path.GetFileName(certificate.DocumentUrl));
        Assert.True(File.Exists(stored), "The certificate file must be stored in private dossier storage.");
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "FarmCertificate"
            && a.EntityId == certificate.FarmCertificateId && a.Action == "SubmitCertificate"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FAR-07 #02] A farm that is not verified yet cannot submit certificates")]
    public async Task Submit_FarmNotApproved_Forbidden()
    {
        var response = await _api.FarmerNew().PostAsync("api/farmer/certificates", Certificate());

        await ApiAssert.Problem(response, HttpStatusCode.Forbidden, "FARM_NOT_APPROVED");
        await using var db = _api.Open();
        Assert.False(await db.FarmCertificates.AnyAsync(c => c.CertificateNumber == "VG-2026-777"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FAR-07 #03] An expiry date already in the past is a field error")]
    public async Task Submit_Expired_FieldError()
    {
        var response = await _api.FarmerA().PostAsync("api/farmer/certificates", Certificate(expiry: "2026-10-01"));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "expiryDate");
        await using var db = _api.Open();
        Assert.False(await db.FarmCertificates.AnyAsync(c => c.CertificateNumber == "VG-2026-777"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FAR-07 #04] A certificate number already registered returns 409")]
    public async Task Submit_Duplicate_Conflict()
    {
        var response = await _api.FarmerA().PostAsync("api/farmer/certificates", Certificate(number: "VG-SEED-001"));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "CERTIFICATE_EXISTS");
        await using var db = _api.Open();
        Assert.Equal(1, await db.FarmCertificates.CountAsync(c => c.CertificateNumber == "VG-SEED-001"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FAR-07 #05] A dossier without a file is rejected before anything is stored")]
    public async Task Submit_NoFile_FileInvalid()
    {
        var response = await _api.FarmerA().PostAsync("api/farmer/certificates", Certificate(withFile: false));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "FILE_INVALID");
        await using var db = _api.Open();
        Assert.False(await db.FarmCertificates.AnyAsync(c => c.CertificateNumber == "VG-2026-777"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FAR-07 #06] A certificate type outside VietGAP / GlobalGAP / Organic / OCOP is a field error")]
    public async Task Submit_UnknownType_FieldError()
    {
        var response = await _api.FarmerA().PostAsync("api/farmer/certificates", Certificate(type: "ISO9001"));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "certificateType");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FAR-07 #07] The farmer's certificate list shows the new dossier first")]
    public async Task List_AfterSubmit_NewestFirst()
    {
        await ApiAssert.Status(await _api.FarmerA().PostAsync("api/farmer/certificates", Certificate()), HttpStatusCode.OK);

        var json = await ApiAssert.Status(await _api.FarmerA().GetAsync("api/farmer/certificates"), HttpStatusCode.OK);

        var numbers = json.EnumerateArray().Select(c => c.GetProperty("certificateNumber").GetString()).ToList();
        Assert.Equal(new[] { "VG-2026-777", "VG-SEED-001" }, numbers);
    }
}
