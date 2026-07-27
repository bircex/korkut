namespace Korkut.Core;

/// <summary>Built-in roles as explicit permission sets.</summary>
public static class Roles
{
    public const string OfficeOwner = "owner";
    
    public const string Lawyer = "lawyer";

    /// <summary>OfficeOwner holds every permission; Lawyer a case-work subset (no admin/delete).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> BuiltIn =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [OfficeOwner] = Permissions.All,
            [Lawyer] = new HashSet<string>
            {
                Permissions.MattersRead, Permissions.MattersWrite,
                Permissions.ClientsRead, Permissions.ClientsWrite,
                Permissions.DocumentsRead, Permissions.DocumentsWrite,
                Permissions.BillingRead, Permissions.BillingWrite,
            },
        };
}
