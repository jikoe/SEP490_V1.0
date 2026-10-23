namespace AgriSustain.Domain.Exceptions;

/// <summary>
/// Reports a violated domain invariant before invalid entity state can be staged.
/// Application validation handles expected input failures before invoking domain mutations.
/// </summary>
public sealed class BusinessRuleException(string message) : Exception(message);
