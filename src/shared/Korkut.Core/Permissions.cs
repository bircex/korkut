namespace Korkut.Core;

/// <summary>
/// The fixed permission catalog. Authorization is by permission, never by role name
/// checked ad hoc — roles are just named permission sets. Deny-by-default: a
/// permission not granted is denied.
/// </summary>
public static class Permissions
{
    public const string MattersRead = "matters.read";
    public const string MattersWrite = "matters.write";
    public const string MattersDelete = "matters.delete";
    public const string ClientsRead = "clients.read";
    public const string ClientsWrite = "clients.write";
    public const string DocumentsRead = "documents.read";
    public const string DocumentsWrite = "documents.write";
    public const string MembersManage = "members.manage";
    public const string OfficeSettings = "office.settings";
    public const string BillingRead = "billing.read";
    public const string BillingWrite = "billing.write";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        MattersRead, MattersWrite, MattersDelete, ClientsRead, ClientsWrite,
        DocumentsRead, DocumentsWrite, MembersManage, OfficeSettings, BillingRead, BillingWrite,
    };
}
