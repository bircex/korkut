namespace Korkut.Core;

/// <summary>
/// The fixed permission catalog. Authorization is by permission, never by role name
/// checked ad hoc — roles are just named permission sets. Deny-by-default: a
/// permission not granted is denied. What a permission covers is further narrowed by
/// scope: most of them apply only to the matters the member works on (see
/// docs/MATTER_RBAC_PLAN.md).
/// </summary>
public static class Permissions
{
    // Matters (dosyalar).
    public const string MattersRead = "matters.read";
    public const string MattersViewAll = "matters.view_all";
    public const string MattersCreate = "matters.create";
    public const string MattersEdit = "matters.edit";
    public const string MattersDelete = "matters.delete";
    public const string MattersManageTeam = "matters.manage_team";

    // The assistant.
    public const string ChatUse = "chat.use";
    public const string ChatGeneral = "chat.general";

    // Documents, drafts and their versions.
    public const string DocumentsRead = "documents.read";
    public const string DocumentsWrite = "documents.write";
    public const string DocumentsDelete = "documents.delete";

    // Hearings, deadlines, tasks and appointments.
    public const string AgendaRead = "agenda.read";
    public const string AgendaWrite = "agenda.write";

    // Communication notes and incoming/outgoing correspondence (evrak).
    public const string NotesRead = "notes.read";
    public const string NotesWrite = "notes.write";

    // Clients: the name, then contact details and identity details separately.
    public const string ClientsRead = "clients.read";
    public const string ClientsContact = "clients.contact";
    public const string ClientsIdentity = "clients.identity";
    public const string ClientsWrite = "clients.write";
    public const string ClientsViewAll = "clients.view_all";

    // Money and the office dashboard.
    public const string FinanceRead = "finance.read";
    public const string FinanceWrite = "finance.write";
    public const string ReportsRead = "reports.read";

    // Case-law and legislation research, saved decisions.
    public const string ResearchUse = "research.use";

    // Administration.
    public const string MembersManage = "members.manage";
    public const string RolesManage = "roles.manage";
    public const string OfficeSettings = "office.settings";
    public const string IntegrationsManage = "integrations.manage";
    public const string AuditRead = "audit.read";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        MattersRead, MattersViewAll, MattersCreate, MattersEdit, MattersDelete, MattersManageTeam,
        ChatUse, ChatGeneral,
        DocumentsRead, DocumentsWrite, DocumentsDelete,
        AgendaRead, AgendaWrite,
        NotesRead, NotesWrite,
        ClientsRead, ClientsContact, ClientsIdentity, ClientsWrite, ClientsViewAll,
        FinanceRead, FinanceWrite, ReportsRead,
        ResearchUse,
        MembersManage, RolesManage, OfficeSettings, IntegrationsManage, AuditRead,
    };
}
