using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Contracts;

public sealed record UploadResponse(string Reply, string Title, string UserDisplay);
