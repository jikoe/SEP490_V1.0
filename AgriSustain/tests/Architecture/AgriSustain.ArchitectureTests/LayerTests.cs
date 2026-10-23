using System.Reflection;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Infrastructure.UnitOfWork;
using Xunit;

namespace AgriSustain.ArchitectureTests;

public sealed class LayerTests
{
    /// <summary>
    /// Examines compiled assembly references to ensure Domain/Application stay independent of
    /// SQL/ASP.NET infrastructure even after checkout/catalog changes.
    /// </summary>
    [Fact]
    public void ProjectReferences_ApplicationAndDomain_DoNotDependOnOuterLayers()
    {
        string[] domain = typeof(RetailOrder).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty).ToArray();
        string[] application = typeof(CheckoutService).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(domain, name => name.StartsWith("AgriSustain."));
        Assert.DoesNotContain(application, name => name.Contains("Infrastructure")
            || name.Contains("EntityFrameworkCore") || name.Contains("AspNetCore"));
        Assert.DoesNotContain(typeof(DemoUnitOfWork).Assembly.GetReferencedAssemblies(),
            name => name.Name == "AgriSustain.Api");
    }
}
