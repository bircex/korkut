namespace Korkut.Application.Settings.Responses;

/// <summary>
/// Both style layers plus the overlay the assistant actually drafts with, and whether this
/// caller may change the office layer.
/// </summary>
public sealed record StyleResponse(
    StyleFieldsResponse Office, StyleFieldsResponse User, StyleFieldsResponse Effective, bool CanEditOffice);
