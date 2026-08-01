using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Services;

/// <summary>Office administration over the repository port. Returns Result. Enabling an office
/// (admin approval) also welcomes the owner by e-mail — best-effort, the approval never fails on
/// a mail outage; the owner lookup opens its own short tenant scope (RLS-safe).</summary>
public sealed class OfficeAdminService(
    IOfficeRepository repository,
    Microsoft.EntityFrameworkCore.DbContext db,
    Korkut.Contract.Notifications.IEmailSender emailSender,
    Korkut.Contract.Notifications.MailLinks mailLinks) : IOfficeAdminService
{
    public async Task<Result<IReadOnlyList<OfficeResponse>>> ListAsync(CancellationToken ct = default)
        => Result<IReadOnlyList<OfficeResponse>>.Ok((await repository.ListAsync(ct)).Select(ToResponse).ToList());

    public async Task<Result> SetDisabledAsync(Guid officeId, bool disabled, CancellationToken ct = default)
    {
        var office = await repository.FindForUpdateAsync(officeId, ct);
        if (office is null)
        {
            return Result.NotFound("Ofis bulunamadı.");
        }

        var wasDisabled = office.Disabled;
        if (disabled)
        {
            office.Disable();
        }
        else
        {
            office.Enable();
        }

        await repository.SaveChangesAsync(ct);
        if (!disabled && wasDisabled)
        {
            await SendApprovedEmailAsync(office.Id, office.Name, ct);
        }

        return Result.Ok();
    }

    private async Task SendApprovedEmailAsync(Guid officeId, string officeName, CancellationToken ct)
    {
        try
        {
            string? owner;
            await using (var scope = await db.BeginTenantScopeAsync(officeId, ct))
            {
                owner = await db.Set<User>().AsNoTracking()
                    .Where(u => u.OfficeId == officeId && u.Role == "owner")
                    .OrderBy(u => u.CreatedAt)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct);
                await scope.CommitAsync(ct);
            }

            if (owner is null)
            {
                return;
            }

            await emailSender.SendAsync(owner, "Korkut AI büronuz onaylandı",
                Korkut.Contract.Notifications.EmailTemplates.Render(
                    "Büronuz onaylandı",
                    [
                        "Merhaba,",
                        $"\"{officeName}\" bürosu için başvurunuz onaylandı. Artık giriş yapıp Korkut AI'yı kullanmaya başlayabilirsiniz.",
                        "Ayarlar bölümünden meslektaşlarınızı büronuza davet edebilirsiniz.",
                    ],
                    ("Giriş yap", mailLinks.LoginLink()), mailLinks.LogoUrl()), ct);
        }
        catch (Exception)
        {
            // best-effort: onay maili başarısızsa onayın kendisi geri alınmaz
        }
    }

    public async Task<Result<OfficeStatsResponse>> StatsAsync(CancellationToken ct = default)
    {
        var offices = await repository.ListAsync(ct);
        var disabled = offices.Count(o => o.Disabled);
        return Result<OfficeStatsResponse>.Ok(new OfficeStatsResponse(offices.Count, offices.Count - disabled, disabled));
    }

    private static OfficeResponse ToResponse(Office o) =>
        new(o.Id, o.Name, o.Disabled, o.RequireTwoFactor, o.CreatedAt);
}
