using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization;

/// <summary>
/// The permission catalog with the labels the roles screen shows, in display order. Every key in
/// <see cref="Korkut.Core.Permissions.All"/> appears exactly once (a test pins it).
/// </summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionGroup> Groups =
    [
        new("Dosyalar",
        [
            new(Permissions.MattersRead, "Ekibinde olduğu dosyaları görür"),
            new(Permissions.MattersViewAll, "Bütün dosyaları görür", "Ekibinde olmasa da"),
            new(Permissions.MattersCreate, "Dosya açar", "Açan dosyanın sorumlusu olur"),
            new(Permissions.MattersEdit, "Dosya bilgilerini düzenler"),
            new(Permissions.MattersDelete, "Dosya siler"),
            new(Permissions.MattersManageTeam, "Dosya ekibini yönetir", "Sorumlusu olduğu dosyalarda"),
        ]),
        new("Sohbet",
        [
            new(Permissions.ChatUse, "Dosyalarında sohbet eder"),
            new(Permissions.ChatGeneral, "Kişisel sohbet açar", "Dosyaya bağlı olmayan, yalnızca kendisinin göreceği sohbet"),
        ]),
        new("Belgeler",
        [
            new(Permissions.DocumentsRead, "Belgeleri görür"),
            new(Permissions.DocumentsWrite, "Belge yükler ve taslak hazırlar"),
            new(Permissions.DocumentsDelete, "Belge siler"),
        ]),
        new("Ajanda",
        [
            new(Permissions.AgendaRead, "Duruşma, süre ve görevleri görür"),
            new(Permissions.AgendaWrite, "Duruşma, süre ve görev ekler, düzenler"),
        ]),
        new("Notlar ve evrak",
        [
            new(Permissions.NotesRead, "Notları ve evrakı görür"),
            new(Permissions.NotesWrite, "Not ve evrak ekler"),
        ]),
        new("Müvekkiller",
        [
            new(Permissions.ClientsRead, "Müvekkilin adını görür"),
            new(Permissions.ClientsContact, "İletişim bilgilerini görür", "Telefon, e-posta, adres"),
            new(Permissions.ClientsIdentity, "Kimlik bilgilerini görür", "TC kimlik no, doğum tarihi"),
            new(Permissions.ClientsWrite, "Müvekkil ekler, düzenler"),
            new(Permissions.ClientsViewAll, "Bütün müvekkil listesini görür", "Dosyası olmasa da"),
        ]),
        new("Mali",
        [
            new(Permissions.FinanceRead, "Fatura, masraf ve tahsilatları görür"),
            new(Permissions.FinanceWrite, "Fatura ve masraf girer"),
            new(Permissions.ReportsRead, "Büro panosunu görür"),
        ]),
        new("Araştırma",
        [
            new(Permissions.ResearchUse, "İçtihat ve mevzuat arar, karar kaydeder"),
        ]),
        new("Yönetim",
        [
            new(Permissions.MembersManage, "Üye davet eder, çıkarır, rol atar"),
            new(Permissions.RolesManage, "Rolleri düzenler"),
            new(Permissions.OfficeSettings, "Büro ayarlarını değiştirir", "Kişilik, bildirimler"),
            new(Permissions.IntegrationsManage, "Yapay zeka ve Telegram ayarlarını yönetir"),
            new(Permissions.AuditRead, "Erişim kayıtlarını görür"),
        ]),
    ];
}
