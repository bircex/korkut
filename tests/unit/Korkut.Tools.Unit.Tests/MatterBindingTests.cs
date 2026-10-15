using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

/// <summary>
/// A matter's conversation keeps its tools on that matter: an omitted matter_id means the bound
/// matter and another matter is refused. Outside a matter the argument is taken as given.
/// </summary>
public class MatterBindingTests
{
    private static readonly Guid Bound = Guid.NewGuid();

    private static AgentToolContext Context(Guid? matter) => new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>(), null, matter);

    private static System.Text.Json.JsonElement Args(Guid? matter)
        => ToolSchema.Parse(matter is { } id ? $$"""{"matter_id":"{{id}}"}""" : "{}");

    [Fact]
    public void An_omitted_matter_is_the_conversations_matter()
    {
        Assert.True(ToolArgs.TryMatter(Args(null), Context(Bound), out var matter, out var refusal));
        Assert.Equal(Bound, matter);
        Assert.Null(refusal);
    }

    [Fact]
    public void The_same_matter_is_allowed()
    {
        Assert.True(ToolArgs.TryMatter(Args(Bound), Context(Bound), out var matter, out _));
        Assert.Equal(Bound, matter);
    }

    [Fact]
    public void Another_matter_is_refused()
    {
        Assert.False(ToolArgs.TryMatter(Args(Guid.NewGuid()), Context(Bound), out var matter, out var refusal));
        Assert.Null(matter);
        Assert.True(refusal!.IsError);
        Assert.Equal(ToolArgs.OtherMatter, refusal.Content);
    }

    [Fact]
    public void A_personal_conversation_takes_the_argument()
    {
        var other = Guid.NewGuid();
        Assert.True(ToolArgs.TryMatter(Args(other), Context(null), out var matter, out _));
        Assert.Equal(other, matter);

        Assert.True(ToolArgs.TryMatter(Args(null), Context(null), out var none, out _));
        Assert.Null(none);
    }
}
