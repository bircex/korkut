using Korkut.Contract.Finance;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Finance;

/// <summary>Self-registration for the Finance module: invoice + expense repos, the unified service, and the totals lookup.</summary>
public sealed class FinanceModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<FinanceService>();
        services.AddScoped<IFinanceService>(sp => sp.GetRequiredService<FinanceService>());
        services.AddScoped<IFinanceLookup>(sp => sp.GetRequiredService<FinanceService>());
    }
}
