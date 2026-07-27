using Korkut.Channels.Services;
using Korkut.Channels.Repositories;
using Korkut.Contract.Channels;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Channels.Extensions;

/// <summary>Self-registration for the Channels module: binds the repository + service ports.</summary>
public sealed class ChannelsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IChannelService, ChannelService>();
    }
}
