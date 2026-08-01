using Korkut.Contract.Agent.Chat;
using Korkut.Contract.Agent.Drafting;
using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools;

/// <summary>
/// Drafts a legal document into the conversation's EDITOR document (not into chat): gets or creates
/// the session's draft document, has the drafting model produce the full markdown per the
/// instruction, and stages it as a PENDING edit for the lawyer's accept/revert. The staged draft is
/// surfaced to the UI as the turn's <see cref="DocumentRef"/> (document card + editor-open signal).
/// </summary>
public sealed class DraftDocumentTool(IDraftingService drafting) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {"type":"object","properties":{
        "instruction":{"type":"string","description":"Belgenin nasıl hazırlanacağını anlatan ayrıntılı talimat: taraflar, konu, talepler, olaylar, tarihler, tutarlar ve dayanaklar"},
        "title":{"type":"string","description":"Belge başlığı (ör. 'Boşanma Dava Dilekçesi — A. Yılmaz')"},
        "petition_type":{"type":"string","description":"Dilekçe/sözleşme tipi anahtarı (biliniyorsa; şablon ve format yönergesini seçer)"}
        },"required":["instruction"]}
        """);

    public ToolSpec Spec => new(
        "draft_document",
        "Dilekçe/sözleşme taslağını sohbete değil sağdaki belge editörüne yazar ve avukatın onayına sunar. "
        + "Belge hazırlama/düzenleme isteklerinde, taraflar/konu/talepler netleşince çağır; talimatı ayrıntılı yaz.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var instruction = ToolArgs.String(arguments, "instruction");
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return ToolResult.Error("instruction gerekli: belgenin nasıl hazırlanacağını ayrıntılı anlat.");
        }

        if (context.ConversationId is not { } conversationId)
        {
            return ToolResult.Error("Bu tur bir sohbete bağlı değil; belge taslağı hazırlanamadı.");
        }

        var result = await drafting.DraftForSessionAsync(
            context.OfficeId, context.UserId, conversationId,
            ToolArgs.String(arguments, "title"), instruction!, ToolArgs.String(arguments, "petition_type"), ct);
        if (result is null)
        {
            return ToolResult.Error("Belge taslağı hazırlanamadı; daha sonra tekrar dene.");
        }

        var content = ToolArgs.Result(new
        {
            drafted = true,
            doc_id = result.DocId,
            title = result.Title,
            summary = result.Summary,
            note = "Taslak editörde avukatın onayına sunuldu. Belge içeriğini sohbete YAPIŞTIRMA; "
                + "avukata taslağın editörde incelemeye hazır olduğunu kısaca söyle.",
        });
        return new ToolResult(content) { Document = new DocumentRef(result.DocId, result.Title, result.Summary) };
    }
}
