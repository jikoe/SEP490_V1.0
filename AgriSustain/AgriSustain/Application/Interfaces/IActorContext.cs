namespace AgriSustain.Application.Interfaces;

public interface IActorContext
{
    long UserId { get; }

    string? TraceId => null;
}
