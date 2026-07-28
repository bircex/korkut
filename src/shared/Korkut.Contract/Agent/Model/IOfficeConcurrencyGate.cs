namespace Korkut.Contract.Agent.Model;

/// <summary>Per-office concurrency gate (process-wide): a shared semaphore capping in-flight model calls per office.</summary>
public interface IOfficeConcurrencyGate
{
    SemaphoreSlim For(Guid officeId, int cap);
}
