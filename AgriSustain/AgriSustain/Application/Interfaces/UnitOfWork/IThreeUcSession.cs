using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Interfaces.UnitOfWork;

public interface IThreeUcSession
{
    IAuthRepository Auth { get; }
    IProfileRepository Profiles { get; }
    IOrderTrackingRepository Orders { get; }
}
