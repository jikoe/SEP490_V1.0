using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace AgriSustain.TestIT2.Infrastructure;

internal sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 1, 0, 0, TimeSpan.Zero);


    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class FarmerTestData
{
    public static readonly DateOnly Today = new(2026, 11, 2);

    public static Product Product() => new()
    {
        ProductId = 301,
        FarmerProfileId = 10,
        ProductName = "Tomato",
        Unit = "kg",
        FarmerProfile = new()
        {
            FarmerProfileId = 10,
            UserId = 101,
            FarmName = "Test farm"
        }
    };

    public static CropSeason Season(string status = "Growing") => new()
    {
        CropSeasonId = 201,
        FarmerProfileId = 10,
        ProductId = 301,
        Product = Product(),
        SeasonName = "Autumn",
        CropVariety = "Tomato",
        CultivatedArea = 100,
        YieldUnit = "kg",
        Status = status,
        PlantingDate = Today.AddDays(-30),
        EstimatedHarvestDate = Today.AddDays(10),
        EstimatedYield = 100
    };

    public static SeasonPlanRequest Plan() => new()
    {
        SeasonName = "Winter",
        ProductId = 301,
        CropVariety = "Tomato",
        CultivatedArea = 100,
        PlantingDate = Today.AddDays(-1),
        EstimatedHarvestDate = Today.AddDays(10),
        EstimatedYield = 100,
        YieldUnit = "kg"
    };

    public static FarmingLogRequest Log() => new()
    {
        CropSeasonId = 201,
        ActionDate = Today,
        ActionType = "Tưới nước",
        DetailDescription = "Watered the field",
        PerformedBy = "Farmer"
    };

    public static HarvestBatchRequest Harvest() => new()
    {
        ProductId = 301,
        CropSeasonId = 201,
        BatchCode = "BATCH-01",
        Unit = "kg",
        EstimatedHarvestDate = Today.AddDays(2),
        EstimatedYield = 10,
        ActualHarvestDate = Today,
        ActualYield = 10,
        ExpirationDate = Today.AddDays(5)
    };

    public static HarvestBatch Batch() => new()
    {
        HarvestBatchId = 401,
        ProductId = 301,
        CropSeasonId = 201,
        Product = Product(),
        CropSeason = Season("Harvesting"),
        BatchCode = "ACTUAL-01",
        HarvestYield = 10,
        AvailableQuantity = 10,
        Status = "Available",
        ActualHarvestDate = Today,
        ExpirationDate = Today.AddDays(5),
        StockEnteredAt = new FixedClock().GetUtcNow().UtcDateTime
    };

    public static void Reject(Action action, string message, string? field = null)
    {
        var error = Assert.Throws<FarmRuleException>(action);
        Assert.Equal(message, error.Message);
        if (field is not null)
            Assert.Equal(field, error.Field);
    }
}

internal sealed class OwnedEvidence : IFarmerEvidenceStorage
{
    public const string Reference = "/owned/101/proof.png";

    public bool Owns(long actor, string reference) => actor == 101 && reference == Reference;

    public string? ResolvePath(long actor, string reference) => Owns(actor, reference) ? reference : null;

    public Task<IReadOnlyList<string>> SaveImagesAsync(
        long actor,
        IReadOnlyList<IFormFile> files,
        string feature,
        CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<string?> StoreReferencesAsync(
        long actor,
        IReadOnlyList<string> references,
        CancellationToken ct) =>
        Task.FromResult<string?>(string.Join('|', references));

    public IReadOnlyList<string> ReadReferences(
        long actor,
        string? stored) =>
        string.IsNullOrEmpty(stored) ? [] : stored.Split('|');
}
