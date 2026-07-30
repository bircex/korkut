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
            "full_name": { "type": "string", "description": "Müvekkil adı soyadı / unvanı" },
            "kind": { "type": "string", "enum": ["individual", "corporate"], "description": "Tür (varsayılan: individual)" },
            "email": { "type": "string", "description": "E-posta (opsiyonel)" },
            "phone": { "type": "string", "description": "Telefon (opsiyonel)" },
            "national_id": { "type": "string", "description": "T.C. kimlik / vergi numarası (opsiyonel)" },
            "address": { "type": "string", "description": "Adres (opsiyonel)" },
            "date_of_birth": { "type": "string", "description": "Doğum tarihi, YYYY-MM-DD (opsiyonel)" }
          },
          "required": ["full_name"]
        }
        """);

    public ToolSpec Spec => new("create_client", "Büroya yeni bir müvekkil ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.ClientsWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var fullName = ToolArgs.String(arguments, "full_name");
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return ToolResult.Error("'full_name' gereklidir.");
        }

        var request = new CreateClientRequest
        {
            FullName = fullName.Trim(),
            Kind = ToolArgs.String(arguments, "kind") ?? "individual",
            Email = ToolArgs.String(arguments, "email"),
            Phone = ToolArgs.String(arguments, "phone"),
            NationalId = ToolArgs.String(arguments, "national_id"),
            Address = ToolArgs.String(arguments, "address"),
            DateOfBirth = DateOnly.TryParse(ToolArgs.String(arguments, "date_of_birth"), out var dob) ? dob : null,
        };
        var result = await clients.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var client = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { client.Id, client.FullName, client.Kind }));
    }
}
