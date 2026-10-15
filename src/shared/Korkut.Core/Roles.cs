namespace Korkut.Core;

/// <summary>
/// The roles every office starts with, as permission sets. They are stored per office and the
/// office may change them (except the owner's, which always holds everything) or add its own;
/// these sets are the defaults, and what a role grants when its row has not been created yet.
/// </summary>
public static class Roles
{
    /// <summary>Yönetici: runs the office and sees every matter. The installation admin has it.</summary>
    public const string OfficeOwner = "owner";

    /// <summary>Avukat.</summary>
    public const string Lawyer = "lawyer";

    /// <summary>Stajyer.</summary>
    public const string Intern = "intern";

    /// <summary>Sekreter.</summary>
    public const string Secretary = "secretary";

    /// <summary>Muhasebe.</summary>
    public const string Accounting = "accounting";

    /// <summary>Okuyucu.</summary>
    public const string Reader = "reader";

    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [OfficeOwner] = "Yönetici",
        [Lawyer] = "Avukat",
        [Intern] = "Stajyer",
        [Secretary] = "Sekreter",
        [Accounting] = "Muhasebe",
        [Reader] = "Okuyucu",
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> BuiltIn =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [OfficeOwner] = Permissions.All,
            [Lawyer] = new HashSet<string>
            {
                Permissions.MattersRead, Permissions.MattersCreate, Permissions.MattersEdit, Permissions.MattersManageTeam,
                Permissions.ChatUse, Permissions.ChatGeneral,
                Permissions.DocumentsRead, Permissions.DocumentsWrite,
                Permissions.AgendaRead, Permissions.AgendaWrite,
                Permissions.NotesRead, Permissions.NotesWrite,
                Permissions.ClientsRead, Permissions.ClientsContact, Permissions.ClientsIdentity, Permissions.ClientsWrite,
                Permissions.ResearchUse,
            },
            [Intern] = new HashSet<string>
            {
                Permissions.MattersRead,
                Permissions.ChatUse, Permissions.ChatGeneral,
                Permissions.DocumentsRead, Permissions.DocumentsWrite,
                Permissions.AgendaRead, Permissions.AgendaWrite,
                Permissions.NotesRead,
                Permissions.ClientsRead,
                Permissions.ResearchUse,
            },
            [Secretary] = new HashSet<string>
            {
                Permissions.MattersRead, Permissions.MattersCreate, Permissions.MattersEdit,
                Permissions.DocumentsRead, Permissions.DocumentsWrite,
                Permissions.AgendaRead, Permissions.AgendaWrite,
                Permissions.NotesRead, Permissions.NotesWrite,
                Permissions.ClientsRead, Permissions.ClientsContact, Permissions.ClientsIdentity, Permissions.ClientsWrite,
                Permissions.ClientsViewAll,
            },
            [Accounting] = new HashSet<string>
            {
                Permissions.ClientsRead, Permissions.ClientsViewAll,
                Permissions.FinanceRead, Permissions.FinanceWrite, Permissions.ReportsRead,
            },
            [Reader] = new HashSet<string>
            {
                Permissions.MattersRead,
                Permissions.ChatUse,
                Permissions.DocumentsRead,
                Permissions.AgendaRead,
                Permissions.NotesRead,
                Permissions.ClientsRead,
                Permissions.ResearchUse,
            },
        };
}
