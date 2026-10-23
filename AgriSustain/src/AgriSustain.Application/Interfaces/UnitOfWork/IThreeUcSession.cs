using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Interfaces.UnitOfWork;

// One transaction must span these repositories, benefits, stock, cart removal and audit.
public interface IThreeUcSession
{
    IAccountRepository Accounts
    {
        get;
    }
    ICategoryRepository Categories
    {
        get;
    }
    ICheckoutRepository Checkout
    {
        get;
    }
    /// <summary>
    /// Stages an audit with the enclosing business transaction. A failed audit must fail the
    /// mutation; target identity must remain readable after a category is physically deleted.
    /// </summary>
    Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken);
}
