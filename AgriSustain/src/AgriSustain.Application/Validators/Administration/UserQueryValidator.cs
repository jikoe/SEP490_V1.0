using AgriSustain.Application.DTOs.Administration;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

public sealed class UserQueryValidator : AbstractValidator<UserQuery>
{
    /// <summary>
    /// Bounds user-search input, pagination and allowed business statuses; role names remain an
    /// auth-team contract rather than an invented permission matrix.
    /// </summary>
    public UserQueryValidator()
    {
        RuleFor(query => query.Keyword).MaximumLength(100);
        RuleFor(query => query.Page).InclusiveBetween(1, 100000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status).Must(status => status == null
            || new[] { "Active", "Pending", "Rejected", "Suspended" }.Contains(status));
        RuleFor(query => query.Role).MaximumLength(50);
    }
}
