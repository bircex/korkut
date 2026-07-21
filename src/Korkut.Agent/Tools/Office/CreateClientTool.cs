using System.Text.Json;
using Korkut.Application.Features.OfficeCore;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Adds a new client (müvekkil) to the office.</summary>
public sealed class CreateClientTool(IOfficeScope scope, IClientService clients) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "name": { "type": "string", "description": "Ad soyad / unvan" },
            "kind": { "type": "string", "enum": ["individual", "corporate"], "description": "Gerçek kişi mi tüzel kişi mi (varsayılan individual)" },
            "email": { "type": "string" },
            "phone": { "type": "string" }
          },
          "required": ["name"]
        }
        """);

    public ToolSpec Spec => new("create_client", "Büroya yeni bir müvekkil ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.ClientsWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var name = ToolArgs.String(arguments, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("'name' gereklidir.");
        }

        var kind = ToolArgs.String(arguments, "kind") ?? "individual";
        var client = await scope.RunAsync(context.OfficeId,
            c => clients.CreateAsync(context.OfficeId, name.Trim(), kind, ToolArgs.String(arguments, "email"), ToolArgs.String(arguments, "phone"), c), ct);
        return ToolResult.Ok(ToolArgs.Result(new { client.Id, client.Name, client.Kind }));
    }
}
