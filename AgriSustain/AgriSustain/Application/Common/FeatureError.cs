namespace AgriSustain.Application.Common;

public sealed record FeatureError(string Code, string Message,
    IReadOnlyDictionary<string, string[]>? Fields = null);

