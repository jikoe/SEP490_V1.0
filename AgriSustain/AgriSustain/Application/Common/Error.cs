namespace AgriSustain.Application.Common;

public sealed record Error(
    string Code,
    string Message,
    string Kind = "Validation",
    IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public IReadOnlyDictionary<string, string[]>? FieldErrors => Fields;
}
