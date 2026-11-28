using System.Net;
using System.Net.Http.Json;
using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.UT2X.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.UT2X.Integration;

[Trait("Layer", "Integration")]
public sealed class FarmerComplaintAccessIntegrationTests
{
    [SqlFact]
    public async Task IT_FAR_10_012_ReadFailure()
    {
        await using var f = await FarmerDatabase.Create();
        f.ReadFailure.Armed = true;
        await FarmerDatabase.Error(await f.Farmer().GetAsync("api/farmer/complaints"), HttpStatusCode.InternalServerError, FarmerComplaintMessages.LoadFailed);
    }
}
