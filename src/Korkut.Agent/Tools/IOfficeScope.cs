namespace Korkut.Agent.Tools;

/// <summary>
/// Runs an office-domain tool's work inside the operational tenant scope (RLS) for its office and
/// commits on success. Office-domain tools never take the office as a tool argument; it comes from
/// the trusted <see cref="ToolContext"/>, and this seam pins it on the operational DbContext so a
/// tool can only ever touch its own office's data (the tool loop and tests share this seam).
/// </summary>
public interface IOfficeScope
{
    Task<T> RunAsync<T>(Guid officeId, Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
