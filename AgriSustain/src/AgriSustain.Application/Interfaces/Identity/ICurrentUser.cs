namespace AgriSustain.Application.Interfaces.Identity;

public interface ICurrentUser
{
    long? UserId
    {
        get;
    }
}
