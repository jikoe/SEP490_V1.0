namespace AgriSustain.Application.Common;

public sealed record Error(string Code, string Message, string Kind = "Validation");
