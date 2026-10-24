namespace AgriSustain.Application.Common;

public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize,
    long? CurrentActorId = null);
