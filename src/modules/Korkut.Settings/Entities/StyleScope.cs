namespace Korkut.Settings.Entities;

/// <summary>The layers a drafting style can be set at.</summary>
public static class StyleScope
{
    /// <summary>The office default, shared by everyone; its profile carries no user.</summary>
    public const string Office = "office";

    /// <summary>One user's personal layer, overlaid on the office default.</summary>
    public const string User = "user";
}
