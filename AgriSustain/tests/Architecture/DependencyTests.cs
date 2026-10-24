using AgriSustain.Api.Controllers;
using Xunit;

namespace AgriSustain.ArchitectureTests;

public sealed class DependencyTests
{
    [Fact]
    public void Domain_SourceImports_DoNotContainApplicationOrInfrastructure()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(ProjectRoot(), "Domain"),
            "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("using AgriSustain.Application", source);
            Assert.DoesNotContain("using AgriSustain.Infrastructure", source);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source);
            Assert.DoesNotContain("Microsoft.AspNetCore", source);
        }
    }

    [Fact]
    public void NewApplication_SourceImports_DoNotContainPersistenceOrHttp()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(ProjectRoot(), "Application"),
            "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != "AuthService.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("using AgriSustain.Infrastructure", source);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source);
            Assert.DoesNotContain("Microsoft.AspNetCore", source);
            Assert.DoesNotContain("SqlClient", source);
        }
    }

    [Fact]
    public void NewControllers_ConstructorDependencies_AreApplicationAbstractions()
    {
        var controllers = new[]
        {
            typeof(UsersController), typeof(CategoriesController), typeof(CheckoutController)
        };
        Assert.NotEmpty(controllers);
        foreach (var type in controllers)
        {
            foreach (var parameter in type.GetConstructors().SelectMany(x => x.GetParameters()))
            {
                Assert.True(parameter.ParameterType.IsInterface);
                Assert.Equal("AgriSustain.Application.Interfaces", parameter.ParameterType.Namespace);
            }
        }
    }

    private static string ProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgriSustain.sln")))
            {
                return Path.Combine(directory.FullName, "AgriSustain");
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("AgriSustain solution was not found.");
    }
}
