# Transactional E-posta Envanteri

Sistem TEK operatör SMTP hesabından gönderir (`KORKUT_SMTP_*`); büro bazlı e-posta entegrasyonu
yoktur. SMTP yapılandırılmamışsa `NullEmailSender` devreye girer: gönderim loglanır, akış asla
kırılmaz. Her gönderim **best-effort**tur — bir mail kesintisi daveti, kaydı, onayı veya parola
akışını bozamaz.

## Şablon

Tüm e-postalar tek HTML kabuğunu kullanır: `Korkut.Contract.Notifications.EmailTemplates.Render`
(başlık + düz-metin paragraflar + opsiyonel tek CTA düğmesi). Paragraflar HTML-encode edilir;
bağlantı yalnızca CTA üzerinden gider (gövdeye ham link/HTML enjekte edilemez). Görünüm: açık gri
zemin, beyaz kart, "Korkut" markası, koyu CTA düğmesi, "otomatik gönderildi" alt notu.

## Tetik listesi

| # | Tetik | Alıcı | Konu | CTA | Kod |
|---|-------|-------|------|-----|-----|
| 1 | Davet oluşturuldu | Davet edilen | "Korkut'a davet edildiniz" | Daveti kabul et → `/?invite=TOKEN` | `InviteService.SendInviteEmailAsync` |
| 2 | Yeni büro başvurusu (kayıt) | Başvuran | "Korkut başvurunuz alındı" | — | `OnboardingService.NotifySignupAsync` |
| 3 | Yeni büro başvurusu | Operatör (`KORKUT_ADMIN_NOTIFY_EMAIL`) | "Yeni büro başvurusu: {büro}" | Admin panelini aç | `OnboardingService.NotifySignupAsync` |
| 4 | Büro onaylandı (admin enable, disabled→enabled geçişi) | Büro sahibi | "Korkut büronuz onaylandı" | Giriş yap → `/giris` | `OfficeAdminService.SendApprovedEmailAsync` |
| 5 | Parola sıfırlama isteği | İsteyen (hesap varsa) | "Korkut parola sıfırlama" | Parolayı sıfırla → `/giris?reset=TOKEN` (1 saat, tek kullanımlık) | `AuthService.RequestPasswordResetAsync` |
| 6 | Parola değişti | Hesap sahibi | "Korkut hesabınızın parolası değişti" | — | `EmailSecurityNotifier.NotifyPasswordChangedAsync` |
| 7 | Süre, duruşma ya da ajanda hatırlatması zamanı geldi | Büronun bildirim adresi (Ayarlar → Bildirimler, yoksa büro sahibi; bildirimler kapalıysa gönderilmez) | "Hatırlatma: {başlık}" | Korkut'u aç → `/giris` | `ReminderService.RunOnceAsync` |

Enumeration güvenliği: 5 numara hesap olsa da olmasa da API 200 döner; mail yalnız hesap varsa
gider ve gönderim hatası da sessizce yutulur (davranış tek tip kalır).

## Bilerek GÖNDERİLMEYENLER (şimdilik)

- Davet kabul edildiğinde davet edene bildirim (opsiyonel; istenirse #1'in yanına eklenir).
- Admin 2FA/oturum olayları — admin konsolu tek kişilik; gerek görülmedi.

## Davet mekanizması (tek akış)

"Yeni üye ekle" = davet: Ayarlar → Üyeler'de **Ad Soyad + E-posta** girilir, davet oluşturulur;
bağlantı hem **e-postayla gider** hem **kopyalanabilir**. Bağlantıya tıklayan kendi parolasını
belirleyince **daveti oluşturan büroya** katılır (token büroyu adresler; davetle yeni büro açılmaz).
Ad-soyad davet satırında saklanır ve kabulde hesaba işlenir. Tek kullanımlık, 7 gün geçerli,
iptal edilebilir.
