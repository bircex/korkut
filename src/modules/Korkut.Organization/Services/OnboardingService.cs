using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization.Services;

/// <summary>
/// Onboarding over the provisioner port. For the Google path the identity is not taken from the
/// request body: the caller passes the e-mail and subject Google verified (a signed, short-lived
/// ticket). For local registration the body carries the e-mail + password (hashed here). Either way
/// the office is created disabled and waits for a platform admin.
/// </summary>
public sealed class OnboardingService(
    IOfficeProvisioner provisioner,
    IPasswordHasher passwordHasher,
    Korkut.Contract.Notifications.IEmailSender emailSender,
    IEnvironment env) : IOnboardingService
{
    /// <summary>Başvuru sonrası bildirimler (best-effort): başvurana "alındı", operatöre
    /// (KORKUT_ADMIN_NOTIFY_EMAIL) onay bekleyen yeni büro haberi. Mail kesintisi kaydı bozmaz.</summary>
    private async Task NotifySignupAsync(string applicantEmail, string officeName, CancellationToken ct)
    {
        try
        {
            await emailSender.SendAsync(applicantEmail, "Korkut başvurunuz alındı",
                Korkut.Contract.Notifications.EmailTemplates.Render(
                    "Başvurunuz alındı",
                    [
                        "Merhaba,",
                        $"\"{officeName}\" bürosu için kaydınızı aldık. Başvurunuz kısa süre içinde incelenecek; onaylandığında e-postayla haber vereceğiz.",
                        "Onaydan sonra Google hesabınızla (veya belirlediğiniz parolayla) giriş yapabilirsiniz.",
                    ]), ct);
        }
        catch (Exception)
        {
            // best-effort
        }

        var adminEmail = env.Get("KORKUT_ADMIN_NOTIFY_EMAIL");
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        try
        {
            await emailSender.SendAsync(adminEmail, $"Yeni büro başvurusu: {officeName}",
                Korkut.Contract.Notifications.EmailTemplates.Render(
                    "Onay bekleyen yeni büro",
                    [
                        $"Büro: {officeName}",
                        $"Başvuran: {applicantEmail}",
                        "Admin panelinden inceleyip etkinleştirebilirsiniz.",
                    ],
                    ("Admin panelini aç", "https://admin.korkutai.com/")), ct);
        }
        catch (Exception)
        {
            // best-effort
        }
    }

    public async Task<Result<DemoRequestResponse>> RequestOfficeAsync(
        string email, string googleSub, DemoRequestRequest request, CancellationToken ct = default)
    {
        var provisioned = await provisioner.ProvisionAsync(email, googleSub, request.OfficeName!.Trim(), ct);
        // The e-mail or subject already has an office; creating a second one would strand it.
        if (provisioned is null)
        {
            return Result<DemoRequestResponse>.Conflict("Bu hesap için zaten bir büro mevcut.");
        }

        await NotifySignupAsync(email, request.OfficeName!.Trim(), ct);
        return Result<DemoRequestResponse>.Ok(new DemoRequestResponse(true));
    }

    public async Task<Result<DemoRequestResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Block throwaway / temp-mail providers so a signup uses a real inbox.
        if (DisposableEmailPolicy.IsDisposable(request.Email))
        {
            return Result<DemoRequestResponse>.BadRequest("Geçici e-posta adresleriyle kayıt yapılamaz. Kurumsal veya kalıcı bir e-posta kullanın.");
        }

        var provisioned = await provisioner.ProvisionLocalAsync(
            request.Email!.Trim(), passwordHasher.Hash(request.Password!),
            request.OfficeName!.Trim(), request.FullName!.Trim(), ct);
        if (provisioned is null)
        {
            return Result<DemoRequestResponse>.Conflict("Bu e-posta için zaten bir hesap mevcut.");
        }

        await NotifySignupAsync(request.Email!.Trim(), request.OfficeName!.Trim(), ct);
        return Result<DemoRequestResponse>.Ok(new DemoRequestResponse(true));
    }
}
