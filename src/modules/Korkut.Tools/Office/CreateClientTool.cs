using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Clients;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Adds a new client (müvekkil) to the office.</summary>
public sealed class CreateClientTool(IClientService clients) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "name": { "type": "string", "description": "Müvekkil adı" },
            "kind": { "type": "string", "enum": ["individual", "corporate"], "description": "Tür (varsayılan: individual)" },
            "email": { "type": "string", "description": "E-posta (opsiyonel)" },
            "phone": { "type": "string", "description": "Telefon (opsiyonel)" }
          },
          "required": ["name"]
        }
        """);

    public ToolSpec Spec => new("create_client", "Büroya yeni bir müvekkil ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.ClientsWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var name = ToolArgs.String(arguments, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("'name' gereklidir.");
        }

        var request = new CreateClientRequest
        {
            Name = name.Trim(),
            Kind = ToolArgs.String(arguments, "kind") ?? "individual",
            Email = ToolArgs.String(arguments, "email"),
            Phone = ToolArgs.String(arguments, "phone"),
        };
        var result = await clients.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var client = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { client.Id, client.Name, client.Kind }));
    }
}
