using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IFarmerManagementRepository
{
    Task<FarmerProfile?> GetFarmerAsync(long actor, bool forUpdate, CancellationToken ct);

    Task<bool> HasRequestAsync(string requestKey, CancellationToken ct);

    Task<IReadOnlyList<AuditLog>> HistoryAsync(string entity, long id, CancellationToken ct);

    Task<IReadOnlyList<FarmCommitment>> CommitmentsAsync(long seasonId, long? batchId, CancellationToken ct);

    Task<FarmOptions> OptionsAsync(long actor, DateOnly today, CancellationToken ct);

    Task<Product?> ProductAsync(long actor, long id, CancellationToken ct);

    Task<CropSeason?> SeasonAsync(long actor, long id, CancellationToken ct);

    void Audit(
        long actor,
        string entity,
        long id,
        string action,
        FarmCommand command,
        object? before,
        object after,
        string? reason = null);
}

public interface IFarmerCropSeasonRepository : IFarmerManagementRepository
{
    Task<(IReadOnlyList<CropSeason> Items, int Total)> SearchOwnedAsync(
        long actor,
        FarmQuery query,
        CancellationToken ct);

    Task<CropSeason?> LoadOwnedContextAsync(long actor, long id, CancellationToken ct);

    Task<bool> NameExistsAsync(long farm, string name, long? except, CancellationToken ct);

    void Add(CropSeason season);

    Task SaveSeasonAsync(IUnitOfWork unit, CancellationToken ct);
}

public interface IFarmerFarmingLogRepository : IFarmerManagementRepository
{
    Task<(IReadOnlyList<CultivationLog> Items, int Total)> SearchOwnedAsync(
        long actor,
        FarmQuery query,
        CancellationToken ct);

    Task<CultivationLog?> LoadLogAsync(long actor, long id, CancellationToken ct);

    Task<CultivationLog?> LoadOriginalAsync(long actor, long id, CancellationToken ct);

    void Add(CultivationLog log);

    Task AppendLogAsync(IUnitOfWork unit, CancellationToken ct);
}

public interface IFarmerHarvestBatchRepository : IFarmerManagementRepository
{
    Task<(IReadOnlyList<HarvestBatch> Items, int Total)> SearchOwnedAsync(
        long actor,
        FarmQuery query,
        CancellationToken ct);

    Task<HarvestBatch?> LoadOwnedContextAsync(long actor, long id, CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, long? except, CancellationToken ct);

    void Add(HarvestBatch batch);

    void AddMovement(InventoryMovement movement);

    Task SaveBatchAsync(IUnitOfWork unit, CancellationToken ct);

    Task SaveQrAsync(IUnitOfWork unit, CancellationToken ct);
}

public interface IFarmerEvidenceStorage
{
    Task<IReadOnlyList<string>> SaveImagesAsync(
        long actor,
        IReadOnlyList<IFormFile> files,
        string feature,
        CancellationToken ct);

    bool Owns(long actor, string reference);

    string? ResolvePath(long actor, string reference);

    Task<string?> StoreReferencesAsync(long actor, IReadOnlyList<string> references, CancellationToken ct);

    IReadOnlyList<string> ReadReferences(long actor, string? stored);
}
