namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// İnfaz hesabı parametreleri: koşullu salıverilme oranları ve süreleri (5275 sK m.107-108,
/// 7242 sK ile değişik), denetimli serbestlik süreleri (m.105/A) ve 7571 sK m.27 ile yeniden
/// yazılan 5275 geçici m.10 pencere tarihleri (RG 25.12.2025, 11. Yargı Paketi).
/// </summary>
public static class SentenceExecutionRates
{
    public const string Kaynak =
        "5275 sK m.105/A (denetimli serbestlik), m.107 (koşullu salıverilme), m.108 (mükerrirler); 7242 sK; 7571 sK m.27 ile değişik 5275 geçici m.10 (RG 25.12.2025)";

    /// <summary>Süreli hapiste koşullu salıverilme oranı (pay, payda). Mükerrirlikte oran, suç tipine göre belirlenen orandan düşük olamaz.</summary>
    public static (int Numerator, int Denominator) KosulluSaliverilmeOrani(string offenseType, int recidivism)
    {
        var (numerator, denominator) = offenseType switch
        {
            "kasten_oldurme" => (2, 3),
            "cinsel" => (3, 4),
            "uyusturucu_ticareti" => (3, 4),
            "teror_orgutlu" => (3, 4),
            _ => (1, 2), // adi
        };

        var (recidivismNumerator, recidivismDenominator) = recidivism switch
        {
            1 => (2, 3),
            2 => (3, 4),
            _ => (0, 1),
        };

        // Kesir karşılaştırması: tekerrür oranı suç tipi oranından büyükse tekerrür oranı uygulanır.
        return recidivismNumerator * denominator > numerator * recidivismDenominator ? (recidivismNumerator, recidivismDenominator) : (numerator, denominator);
    }

    /// <summary>Müebbet cezalarda koşullu salıverilme için kurumda geçirilecek yıl (m.107/2-3; mükerrirlerde m.108/1-a,b).</summary>
    public static int MuebbetKosulluYil(string sentenceType, int recidivism) => (sentenceType, recidivism > 0) switch
    {
        ("agirlastirilmis_muebbet", false) => 30,
        ("agirlastirilmis_muebbet", true) => 39,
        (_, false) => 24,
        (_, true) => 33,
    };

    /// <summary>Normal rejimde denetimli serbestlik süresi, yıl (m.105/A; özel durumlar 7242 sK).</summary>
    public static int DenetimliSerbestlikYil(string specialCircumstance) => specialCircumstance switch
    {
        "cocuklu_kadin" => 2,   // 0-6 yaş çocuğu bulunan kadın hükümlü
        "agir_hasta_yasli" => 3, // maruz kaldığı ağır hastalık, engellilik veya kocama
        _ => 1,
    };

    /// <summary>Geçici m.10 kapsamı: suç tarihi bu günden önce ise Covid dönemi genişletilmiş rejim (30.03.2020).</summary>
    public static readonly DateOnly GeciciM10CovidSiniri = new(2020, 3, 30);

    /// <summary>Geçici m.10 kapsamı: suç tarihi bu gün ve öncesi ise düzenlemeden yararlanır (31.07.2023).</summary>
    public static readonly DateOnly GeciciM10SonSucTarihi = new(2023, 7, 31);

    /// <summary>
    /// 7571 ile değişik geçici m.10 uyarınca basitleştirilmiş denetimli serbestlik süresi (yıl):
    /// suç tarihi 30.03.2020'den önce ise 3 yıl, 30.03.2020 - 31.07.2023 arasında ise 2 yıl.
    /// Terör ve örgütlü suçlar ile geçici maddede sayılan istisna suçlar kapsam dışıdır.
    /// Gerçek düzenleme ek olarak açık kuruma 3 yıl erken ayrılma da içerir; bu sadeleştirme
    /// tool çıktısındaki uyarıda açıkça belirtilir.
    /// </summary>
    public static int? GeciciM10DenetimliSerbestlikYil(DateOnly offenseDate, string offenseType)
    {
        if (offenseType == "teror_orgutlu" || offenseDate > GeciciM10SonSucTarihi)
        {
            return null;
        }

        return offenseDate < GeciciM10CovidSiniri ? 3 : 2;
    }
}
