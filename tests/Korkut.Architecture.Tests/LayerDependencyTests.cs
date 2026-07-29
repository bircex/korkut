using System.Reflection;
using NetArchTest.Rules;

namespace Korkut.Architecture.Tests;

/// <summary>
/// Enforces the modular-monolith dependency rules from the architecture decision:
/// Core is the leaf kernel (it owns the cross-cutting primitives and depends on
/// nothing else in the solution) and Contract (the shared ports/DTOs surface)
/// stays free of infrastructure/framework concerns. These fail the build if a
/// forbidden dependency is introduced.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly Core = typeof(Korkut.Core.Entity).Assembly;
    private static readonly Assembly Contract = typeof(Korkut.Contract.Result).Assembly;
    private static readonly Assembly Infrastructure = typeof(Korkut.Infrastructure.InfrastructureAssembly).Assembly;

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
    public void Core_is_the_leaf_and_depends_on_no_other_layer_or_infrastructure_framework()
    {
        // Core is the shared kernel (entity base, persistence ports, clock, the module system).
        // It carries only the lightweight DI/Config abstractions a module registers against —
        // never a domain module, a host, or a persistence framework.
        AssertNoDependency(
            Core,
            "Korkut.Contract",
            "Korkut.Infrastructure",
            "Korkut.Api",
            "Korkut.Worker",
            "Korkut.Migrator",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "Minio");
    }

    [Fact]
    public void Contract_does_not_depend_on_infrastructure_or_host()
    {
        // Contract is the shared surface every module and host compiles against: ports, DTOs, the
        // Result envelope, and its HTTP mapping (ApiResults) — so it does reference the ASP.NET
        // framework. It must still stay free of the persistence framework, the object store, and
        // any concrete host, so a consumer never drags infrastructure in through the contract.
        AssertNoDependency(
            Contract,
            "Korkut.Infrastructure",
            "Korkut.Api",
            "Korkut.Worker",
            "Korkut.Migrator",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Minio");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_web_host()
    {
        AssertNoDependency(Infrastructure, "Korkut.Api");
    }

}
