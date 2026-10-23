namespace AgriSustain.Application.Interfaces;

public interface ICurrentActor
{
    long UserId { get; }
    string Role { get; }
}

