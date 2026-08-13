namespace Korkut.Contract.Agent.Model;

/// <summary>
/// One chat provider this build ships an adapter for: the router key (<see cref="Name"/>, matching
/// <see cref="IChatProviderService.Name"/>), a display label, and the environment variable its API
/// key is read from. The key VALUE never appears in this contract — only which variable holds it —
/// so the admin console can report "configured / not configured" without ever seeing a secret.
/// </summary>
public sealed record ChatProviderDescriptor(string Name, string Label, string? ApiKeyEnvVar, bool IsLocal);

/// <summary>
/// The provider vocabulary shared by the model layer (which registers the adapters) and the admin
/// console (which lets an operator point catalog entries at them). One list, so a provider can
/// never be routable-but-invisible or listed-but-unroutable.
/// </summary>
public static class ChatProviders
{
    public const string Claude = "claude";
    public const string DeepSeek = "deepseek";
    public const string Gemini = "gemini";
    public const string OpenAi = "gpt";
    public const string Groq = "groq";
    public const string Ollama = "ollama";

    public static readonly IReadOnlyList<ChatProviderDescriptor> All =
    [
        new(DeepSeek, "DeepSeek", "DEEPSEEK_API_KEY", IsLocal: false),
        new(Gemini, "Google Gemini", "GEMINI_API_KEY", IsLocal: false),
        new(Claude, "Anthropic Claude", "ANTHROPIC_API_KEY", IsLocal: false),
        new(OpenAi, "OpenAI", "OPENAI_API_KEY", IsLocal: false),
        new(Groq, "Groq", "GROQ_API_KEY", IsLocal: false),
        // Local backends authenticate by network reachability, not a key.
        new(Ollama, "Yerel (Ollama)", null, IsLocal: true),
    ];

    public static ChatProviderDescriptor? Find(string? name) =>
        name is null ? null : All.FirstOrDefault(p => p.Name == name);
}
