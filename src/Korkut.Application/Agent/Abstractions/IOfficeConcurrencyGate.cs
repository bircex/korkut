using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Abstractions;

/// <summary>Per-office concurrency gate (process-wide): a shared semaphore capping in-flight model calls per office.</summary>
public interface IOfficeConcurrencyGate
{
    SemaphoreSlim For(Guid officeId, int cap);
}
