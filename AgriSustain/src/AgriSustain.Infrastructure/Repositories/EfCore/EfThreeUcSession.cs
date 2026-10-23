using System.Diagnostics;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Demo;
using Db = AgriSustain.Infrastructure.Data.Entities;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

/// <summary>
/// Implements only the three assigned repository contracts on one scoped scaffolded DbContext.
/// The UoW owns the transaction and commit; missing auth/points/retry dependencies stay mocked.
/// </summary>
internal sealed partial class EfThreeUcSession :
    IThreeUcSession, IAccountRepository, ICategoryRepository, ICheckoutRepository
{
    private readonly AgriSustainDbContext _db;
    private readonly long? _actorId;
    private readonly TimeProvider _clock;
    private readonly Dictionary<long, (decimal Available, decimal Reserved)> _readStock = new();
    internal Dictionary<long, int> Points { get; }
    internal Dictionary<(long Buyer, Guid Key), RetailOrder> RequestOrders { get; }
    internal Dictionary<long, int> SessionVersions { get; }
    public IAccountRepository Accounts => this;
    public ICategoryRepository Categories => this;
    public ICheckoutRepository Checkout => this;

    /// <summary>Receives the transaction's DbContext and detached external mock projections.</summary>
    internal EfThreeUcSession(AgriSustainDbContext db, long? actorId,
        TimeProvider clock, SqlDemoDependencies dependencies)
    {
        _db = db;
        _actorId = actorId;
        _clock = clock;
        Points = new(dependencies.Points);
        RequestOrders = new(dependencies.RequestOrders);
        SessionVersions = new(dependencies.SessionVersions);
    }

    /// <summary>Interprets approved datetime2 snapshots as UTC without rewriting legacy data.</summary>
    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>Stages a real AuditLog entity in the same transaction as the business mutation.</summary>
    public Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _db.AuditLog.Add(new Db.AuditLog
        {
            PerformedByUserId = record.ActorId,
            EntityType = record.EntityType,
            EntityId = record.TargetId,
            Action = record.Action,
            OldValue = record.OldValue,
            NewValue = record.NewValue,
            Reason = record.Reason,
            OccurredAt = record.OccurredAt.UtcDateTime,
            TraceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")
        });
        return Task.CompletedTask;
    }
}
