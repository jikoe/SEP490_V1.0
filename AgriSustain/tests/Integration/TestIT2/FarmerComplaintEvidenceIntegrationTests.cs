using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using AgriSustain.Infrastructure.FileStorage;

namespace AgriSustain.TestIT2.Integration;

[Trait("Layer", "Integration")]
public sealed class FarmerComplaintEvidenceIntegrationTests
{
    [Fact]
    public async Task IT_FAR_10_FILE_001_ValidPdf()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerComplaintFileStorage(folder);
        var reference = await storage.SaveAsync(101, PrivateEvidenceDirectory.File(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 test"), "proof.pdf"), default);
        Assert.True(await storage.IsValidOwnedAsync(101, reference, default));
        Assert.False(await storage.IsValidOwnedAsync(102, reference, default));
        Assert.Null(storage.Resolve(101, "/api/files/evidence/101/../../secret.pdf"));
        Assert.Equal(1, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_10_FILE_002_WrongExtension()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerComplaintFileStorage(folder);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(101, PrivateEvidenceDirectory.File([1, 2, 3, 4], "proof.exe"), default));
        Assert.Equal(FarmerComplaintMessages.Invalid, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_10_FILE_003_SpoofedHeader()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerComplaintFileStorage(folder);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(101, PrivateEvidenceDirectory.File([1, 2, 3, 4], "proof.pdf"), default));
        Assert.Equal(FarmerComplaintMessages.Invalid, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_10_FILE_004_Empty()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerComplaintFileStorage(folder);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(101, PrivateEvidenceDirectory.File([], "proof.jpg"), default));
        Assert.Equal(FarmerComplaintMessages.Invalid, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_10_FILE_005_Oversized()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerComplaintFileStorage(folder);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(101, PrivateEvidenceDirectory.File(new byte[5242881], "proof.png"), default));
        Assert.Equal(FarmerComplaintMessages.Invalid, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_10_FILE_006_MaximumFileSize()
    {
        using var folder = new PrivateEvidenceDirectory();
        var bytes = new byte[5242880];
        new byte[]
        {
            137,
            80,
            78,
            71,
            13,
            10,
            26,
            10
        }.CopyTo(bytes, 0);
        var storage = new FarmerComplaintFileStorage(folder);
        string reference = await storage.SaveAsync(101, PrivateEvidenceDirectory.File(bytes, "proof.png"), default);
        Assert.True(await storage.IsValidOwnedAsync(101, reference, default));
        Assert.Equal(5242880, new FileInfo(storage.Resolve(101, reference)!).Length);
    }
}
