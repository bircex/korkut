namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// İşyeri kira stopajı (GVK m.94/5-a). Oran, Bakanlar Kurulu Kararı 2009/14592 ile %20 olarak
/// belirlendi (03.02.2009'dan beri) ve 2026 için değişmedi (değiştiren bir BKK/CB Kararı
/// bulunmuyor). Kanun maddesi kalıcı; oranın kendisi Kanun'a değil BKK'ya bağlı, teorik olarak
/// yıl içinde değişebilir — yeni bir karar çıkarsa bu sabit güncellenir.
/// </summary>
internal static class RentWithholding
{
    public const string Kaynak =
        "GVK m.94/5-a (stopaja tabi ödemeler); oran BKK 2009/14592 ile %20 (03.02.2009'dan beri, 2026'da değişmedi)";

    /// <summary>İşyeri kira stopaj oranı.</summary>
    public const decimal StopajOrani = 0.20m;
}
