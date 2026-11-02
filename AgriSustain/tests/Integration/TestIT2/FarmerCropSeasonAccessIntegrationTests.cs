using System.Net;
using System.Net.Http.Json;
using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.TestIT2.Integration;

[Trait("Layer", "Integration")]
public sealed class FarmerCropSeasonAccessIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_01_013_ReadFailure()
    {
        await using var f = await FarmerDatabase.Create();
        f.ReadFailure.Armed = true;
        await FarmerDatabase.Error(await f.Farmer().GetAsync("api/farmer/crop-seasons"), HttpStatusCode.InternalServerError, FarmManagementMessages.MSG126);
    }


    [SqlFact]
    public async Task IT_FAR_01_014_AccountEligibility()
    {
        await using var f = await FarmerDatabase.Create();
        await f.Edit(async db => (await db.FarmerProfiles.SingleAsync()).VerificationStatus = "Pending");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Farmer().GetAsync("api/farmer/crop-seasons")).StatusCode);
        await f.Edit(async db =>
        {
            (await db.FarmerProfiles.SingleAsync()).VerificationStatus = "Approved";
            (await db.Users.FindAsync(f.FarmerId))!.Status = "Inactive";
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Farmer().GetAsync("api/farmer/crop-seasons")).StatusCode);
    }
}
