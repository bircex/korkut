using Korkut.Contract.Tasks;
using Korkut.Core;
using Korkut.Tasks.Repositories;
using Korkut.Tasks.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Tasks.Extensions;

/// <summary>Self-registration for the Tasks module: binds the repository + service ports.</summary>
public sealed class TasksModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskService, TaskService>();
    }
}
