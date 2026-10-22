using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data.Demo;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class EfThreeUcSession :
    IThreeUcSession, IAuthRepository, IProfileRepository, IOrderTrackingRepository
{
    private readonly AgriSustainContext _db;
    private readonly long? _actorId;
    private readonly SqlDemoDependencies _dependencies;

    public IAuthRepository Auth => this;
    public IProfileRepository Profiles => this;
    public IOrderTrackingRepository Orders => this;

    public EfThreeUcSession(AgriSustainContext db, long? actorId, SqlDemoDependencies dependencies)
    {
        _db = db;
        _actorId = actorId;
        _dependencies = dependencies;
    }
}
