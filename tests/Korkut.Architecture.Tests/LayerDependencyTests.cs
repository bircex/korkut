using System.Reflection;
using NetArchTest.Rules;

namespace Korkut.Architecture.Tests;

/// <summary>
/// Enforces the modular-monolith dependency rules from the architecture decision:
/// Domain and Application stay free of infrastructure/framework concerns, and the
/// SharedKernel depends on nothing else in the solution. These fail the build if a
/// forbidden dependency is introduced.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Korkut.Domain.Primitives.Entity).Assembly;
    private static readonly Assembly Application = typeof(Korkut.Application.ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(Korkut.Infrastructure.InfrastructureAssembly).Assembly;
    private static readonly Assembly SharedKernel = typeof(Korkut.SharedKernel.Ids.UuidV7).Assembly;

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbidden)}]. " +
            $"Offenders: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Domain_does_not_depend_on_other_layers()
    {
        AssertNoDependency(
            Domain,
            "Korkut.Application",
            "Korkut.Infrastructure",
            "Korkut.Api",
            "Korkut.Worker",
            "Korkut.Migrator");
    }

    [Fact]
    public void Domain_does_not_depend_on_infrastructure_frameworks()
    {
        AssertNoDependency(
            Domain,
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "Minio");
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_host()
    {
        AssertNoDependency(
            Application,
            "Korkut.Infrastructure",
            "Korkut.Api",
            "Korkut.Worker",
            "Korkut.Migrator",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore");
    }

    [Fact]
    public void SharedKernel_depends_on_nothing_else_in_the_solution()
    {
        AssertNoDependency(
            SharedKernel,
            "Korkut.Domain",
            "Korkut.Application",
            "Korkut.Infrastructure",
            "Korkut.Api",
            "Korkut.Worker",
            "Korkut.Migrator");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_web_host()
    {
        AssertNoDependency(Infrastructure, "Korkut.Api");
    }
}
