# Hesaplama Araçları Planı (calc tools)

> Durum: PLAN, 2026-08-02. Kaynak analiz: consultohukuk.com/araclar (8 araç) + hakhesap.com, imer.av.tr, iavukat.com, ictihatpro.com katalogları.
> Amaç: Ajanın sohbet içinden çağırabileceği, tarife/oran parametreleri koddan ayrı "constant" olarak tutulan hesaplama tool'ları.

## 1. Mevcut durum

- `Korkut.Tools` içinde 34 tool var (25 büro + 6 hukuk kaynağı + `search_office_documents`, `draft_document`, `remember`). **Hesaplama tool'u sıfır.** Döviz/TCMB istemcisi de yok.
- Tool'lar assembly-scan ile otomatik kaydolur (`AgentHostSetup.cs:84`); yeni tool = `Korkut.Tools` içinde `IAgentTool` implementasyonu, DI değişikliği gerekmez.
- Önerilen yerleşim: `src/modules/Korkut.Tools/Calc/` (tool'lar) + `src/modules/Korkut.Tools/Calc/Tariffs/` (parametre sabitleri).

## 2. Parametre (constant) tasarımı

Tarifeler zamana bağlı değişir; her sabit **dönem aralığıyla** tutulmalı, hesap tarihine göre doğru dönem seçilmeli:

```csharp
// Tariffs/LegalInterestRates.cs örneği
public static class LegalInterestRates
{
    // (başlangıç, bitiş dahil değil, yıllık %)
    public static readonly RatePeriod[] Yasal =
    [
        new(new(2006, 1, 1), new(2024, 6, 1), 9m),
        new(new(2024, 6, 1), new(2026, 7, 31), 24m),
        new(new(2026, 7, 31), null, 31m), // 7589 sK m.10: reeskont %38,75 x 0,80
    ];
}
```

Kurallar:
- Her tarife dosyasının başına kaynak (RG tarih/sayı) ve geçerlilik dönemi yorum olarak yazılır.
- Faiz gibi dönemsel hesaplar tek orana değil dönem tablosuna göre parça parça hesaplanır.
- Yeni tarife çıktığında sadece Tariffs dosyasına satır eklenir, tool kodu değişmez.
- İleride admin'den güncellenebilirlik istenirse Tariffs statikleri DB destekli bir `ITariffProvider` arkasına alınır; ilk fazda gerek yok.

## 3. Faz 1: Consulto paritesi (8 tool)

### 3.1 `calc_vekalet_ucreti` (AAÜT 2025-2026)

Girdi: `kategori` (enum), `dava_degeri` (nispi için TL).

AAÜT 2025-2026: RG 04.11.2025 / 33067, yürürlük 04.11.2025. 2026-2027 tarifesi HENÜZ YOK (Ekim-Kasım 2026'da beklenir).

Nispi dilim tablosu (konusu para olan davalar):

| Dilim | Oran |
|---|---|
| İlk 600.000 TL | %16 |
| Sonraki 600.000 TL | %15 |
| Sonraki 1.200.000 TL | %14 |
| Sonraki 1.200.000 TL | %13 |
| Sonraki 1.800.000 TL | %11 |
| Sonraki 2.400.000 TL | %8 |
| Sonraki 3.000.000 TL | %5 |
| Sonraki 3.600.000 TL | %3 |
| Sonraki 4.200.000 TL | %2 |
| 18.600.000 TL üstü | %1 |

Maktu ücretler (TL): icra takibi 9.000; tahliyeli icra takibi 20.000; icra mahkemesi duruşmalı 18.000, duruşmasız 11.000; sulh hukuk 30.000; sulh ceza/infaz hâkimliği 18.000; asliye mahkemeleri 45.000; tüketici 22.500; FSHM 55.000 (*); idare/vergi duruşmasız 30.000, duruşmalı 40.000 (*); ağır ceza 65.000; çocuk mahkemesi 45.000; çocuk ağır ceza 65.000; istinaf 22.000; Yargıtay duruşmasız 22.000, duruşmalı 55.000; Danıştay 55.000; AYM bireysel başvuru 55.000.

(*) FSHM ve idare-duruşmalı kalemlerinde ikincil kaynaklar çelişiyor; RG eki taranmış PDF'ten göz kontrolü şart: https://www.resmigazete.gov.tr/eskiler/2025/11/20251104-9-1.pdf

Kural: nispi ücret o davada maktu ücretin altına inemez; kısmi kabulde her iki taraf vekalet ücreti mantığı v2'de.

### 3.2 `calc_faiz` (yasal / avans / özel oran)

Girdi: `faiz_turu` (yasal | avans | ozel), `ana_para`, `baslangic`, `bitis`, `bitis_dahil` (bool), `ozel_oran`.
Yöntem: basit faiz, ACT/365, dönem tablosuna göre parçalı.

**DİKKAT: Yasal faiz 31.07.2026'da değişti.** 7589 sayılı Kanun m.10 (RG 31.07.2026 / 33326) 3095 m.1'i yeniden yazdı: oran artık "TCMB'nin önceki yıl 31 Aralık reeskont oranının %80'i". Yani her yıl 1 Ocak'ta (31 Aralık reeskontuna göre) otomatik güncellenecek.

Yasal faiz dönemleri:

| Dönem | Oran |
|---|---|
| 01.01.2006 - 31.05.2024 | %9 |
| 01.06.2024 - 30.07.2026 | %24 |
| 31.07.2026 - devam | **%31** (reeskont %38,75 x 0,80) |

Avans (ticari temerrüt, 3095 m.2/2) dönemleri:

| Yürürlük | Avans |
|---|---|
| 01.12.2023 | %41,75 |
| 23.12.2023 | %44,25 |
| 01.04.2024 | %51,75 |
| 28.12.2024 | %49,25 |
| 08.03.2025 | %44,25 |
| 17.09.2025 | %42,25 |
| 20.12.2025 - devam | **%39,75** |

Not: Consulto "01.06.2024 sonrası avans %53,25" gösteriyor; kaynaklarla doğrulanamadı (01.04.2024 itibarıyla %51,75). 2023 öncesi dönemler TCMB tebliğ arşivinden tamamlanmalı. Consulto ayrıca güncel avans indirimlerini (2025 sonrası) hiç işlememiş görünüyor; biz dönem tablosunu tam tutarak fark yaratırız.

### 3.3 `calc_kidem_tazminati`

Girdi: `ise_giris`, `isten_cikis`, `brut_maas`, `yemek`, `yol`, `diger` (aylık brüt yan haklar).
Formül: giydirilmiş brüt x yıl + kalan gün orantısı; tavan kontrolü; damga vergisi kesintisi.

Sabitler:

| Sabit | Değer | Dönem |
|---|---|---|
| Kıdem tavanı | **73.729,87 TL** | 01.07.2026 - 31.12.2026 |
| Kıdem tavanı | 64.948,77 TL | 01.01.2026 - 30.06.2026 |
| Damga vergisi | binde 7,59 | sabit |

Not: Consulto hâlâ 53.919,68 TL tavan kullanıyor (2025 değeri, GÜNCEL DEĞİL). Kıdemde gelir vergisi ve SGK kesintisi yok, sadece damga.

### 3.4 `calc_infaz`

Girdi: `suc_tarihi`, `dogum_tarihi`, `ceza_turu` (sureli | muebbet | agirlastirilmis_muebbet), `toplam_ceza` (yıl/ay/gün), `suc_tipi` (adi | kasten_oldurme | cinsel | uyusturucu_ticareti | teror_orgutlu), `tekerrur` (0|1|2), `infaz_baslangic`, `mahsup_gun`, `ozel_durum` (yok | 0-6_yas_cocuklu_kadin | agir_hasta_yasli).
Çıktı: koşullu salıverilme tarihi, denetimli serbestlik tarihi, bihakkın tahliye, "yatar".

Sabitler (5275 + 7242 + 7571):
- Koşullu salıverilme oranları: adi 1/2; kasten öldürme 2/3; cinsel + uyuşturucu ticareti 3/4; terör/örgütlü 3/4; 1. mükerrir 2/3; 2. mükerrir 3/4.
- Müebbet yatar 24 yıl, ağırlaştırılmış müebbet 30 yıl.
- Denetimli serbestlik: normal rejim 1 yıl (105/A); 0-6 yaş çocuklu kadın 2 yıl; ağır hasta/yaşlı 3 yıl.
- **7571 sayılı Kanun (RG 25.12.2025, 11. Yargı Paketi) geçici m.10'u yeniden yazdı:** suç tarihi 31.07.2023 ve öncesi ise genişletilmiş DS (30.03.2020 öncesi istisna dışı suçlarda 6 yıla, sonrası/istisna suçlarda 4 yıla kadar) + açık kuruma 3 yıl erken geçiş; terör/örgütlü ve KS geri alınanlar hariç. Suç tarihi 31.07.2023 sonrası: sadece normal rejim.
- Çocuk hükümlü: eğitimevi süreleri artırımlı sayılır (m.107/5 metninden teyit edilmeli).

### 3.5 `calc_harc` (ıslah dahil genel yargı harcı)

Consulto'da sadece ıslah harcı var; biz tek tool'da başvuru + peşin + ıslah/HMK 107 tamamlama harcını kapsayalım.

Girdi: `islem` (dava_acilis | islah | deger_artirimi | istinaf | temyiz | icra_takip), `dava_turu`, `eski_deger`, `yeni_deger` veya `dava_degeri`.

Sabitler (2026, Harçlar Tebliği Seri No 98, 01.01.2026'dan itibaren, artış %18,95):

| Kalem | Değer |
|---|---|
| Karar ve ilam harcı (nispi) | binde 68,31 |
| Peşin oran | 1/4 (ölüm/cismani zararda dava açılışında 1/20 uygulaması) |
| Başvurma harcı: sulh hukuk | 335,20 TL |
| Başvurma harcı: asliye/aile/iş/tüketici/idare | 732,00 TL |
| Başvurma harcı: icra dairesi | 732,00 TL |
| İstinaf başvurma | 2.002,00 TL |
| Temyiz başvurma | 3.608,50 TL |
| Maktu karar ve ilam harcı | 732,00 TL |
| Celse harcı | binde 2,27 (asgari 411,60 TL) |
| Vekaletname suret harcı | 104,00 TL |
| Keşif harcı | 5.188,00 TL |
| İcra peşin harç | binde 5 |
| İcra tahsil harcı | aşamaya göre %4,55 - %11,38 |
| AYM bireysel başvuru | 6.024,10 TL |

Islah/değer artırımı kuralı: `ek_harc = (yeni_deger - eski_deger) x 0,06831 x pesin_oran`.

### 3.6 `calc_arac_deger_kaybi` (SEDDK Genel Şartlar formülü)

Girdi: `piyasa_degeri`, `kilometre`, `hasar_boyutu` (buyuk | orta | kucuk | basit) veya `hasar_tutari`, `gecmis_hasar_sayisi`.

Resmi formül: `Toplam Değer Kaybı = Baz Değer Kaybı x Hasar Boyutu Katsayısı x Kilometre Katsayısı`, `Baz Değer Kaybı = piyasa değeri x %19`.

| Hasar boyutu | Katsayı |
|---|---|
| Büyük | 0,90 |
| Orta | 0,75 |
| Küçük | 0,50 |
| Basit | 0,25 |

| Kilometre | Katsayı |
|---|---|
| 0 - 14.999 | 0,90 |
| 15.000 - 29.999 | 0,80 |
| 30.000 - 44.999 | 0,60 |
| 45.000 - 59.999 | 0,40 |
| 60.000 - 74.999 | 0,30 |
| 75.000 - 149.999 | 0,20 |
| 150.000+ | 0,10 |

Not: km tablosunun ara basamakları Genel Şartlar ekinden teyit edilmeli. 2026'da SEDDK formülü değiştirmedi ama zorunlu standart ekspertiz şablonu genelgesi yayımladı. Consulto farklı (rayiç katsayısı 0,65'li) bir varyant kullanıyor; biz resmi Genel Şartlar formülünü esas alalım, çıktıya "mahkeme bilirkişi hesabı sapabilir" uyarısı koyalım.

### 3.7 `calc_bedensel_tazminat` (yaralanmalı trafik kazası)

Girdi: `cinsiyet`, `yas`, `maluliyet_orani`, `kusur_orani`, `aylik_net_gelir`, `gecici_is_goremezlik_ay`.

Aktüeryal sabitler:
- Mortalite: **TRH-2010** (PMF-1931 terk edildi, yerleşik Yargıtay içtihadı).
- Teknik faiz: **mahkeme modu %0** (progresif rant: %10 artırım + %10 iskonto). Sigorta/Genel Şart cetveli modu ayrı tutulmalı (AYM iptalleri sonrası mahkemeler %1,8'lik cetvelleri uygulamıyor; Consulto %1,65 kullanıyor, tartışmalı).
- Pasif dönem baz geliri: AGİ'siz net asgari ücret.
- Asgari ücret 2026 (tek dönem, tüm yıl): brüt 33.030,00 TL, net 28.075,50 TL, günlük brüt 1.101,00 TL. (Consulto 22.104,67 TL kullanıyor, 2025 değeri, GÜNCEL DEĞİL.)
- ZMSS kişi başı teminat limiti 2026: 3.600.000 TL.
- Aktif dönem sonu: 60 yaş (uygulama standardı); geçici iş göremezlikte %100 maluliyet varsayımı.

### 3.8 `calc_destekten_yoksunluk`

Girdi: müteveffa (`cinsiyet`, `yas`, `aylik_net_gelir`, `kusur_orani`) + hak sahipleri (`es` bool, `cocuklar[]` yaş listesi, `anne` bool, `baba` bool).

Sabitler: TRH-2010, teknik faiz mahkeme modunda %0, pasif baz AGİ'siz net asgari ücret (28.075,50 TL), destek payları (standart uygulama: eş 2 pay, çocuk 1 pay, müteveffa 2 pay havuz yöntemi), çocuk destek sonu 18 / lise 20 / üniversite 25, eşe yeniden evlenme ihtimali tablosu (AYİM/TRH pratiği, çocuk başına 5 puan indirim).

## 4. Faz 2: Bizde ve Consulto'da olmayan araçlar (öncelikli boşluk listesi)

İş hukuku:
1. `calc_ihbar_tazminati` (4857 m.17 süreleri: 6 aya kadar 2 hafta, 6-18 ay 4 hafta, 18-36 ay 6 hafta, 3 yıl üstü 8 hafta; gelir vergisi + damga kesintili)
2. `calc_fazla_mesai` (x1,5 hafta içi, x2 tatil; brüt/net)
3. `calc_yillik_izin_ucreti` (kıdeme göre 14/20/26 gün)
4. `calc_ise_iade_tazminati` (4-8 ay boşta geçen süre + işe başlatmama tazminatı)
5. `calc_brut_net_maas`

Ücret ve tarife:
6. `calc_arabuluculuk_ucreti` (2026 tarifesi: RG 26.12.2025 / 33119; saatlik genel 2 taraf 1.000 TL, ticari 1.500 TL'den başlar; anlaşmada nispi ilk 600.000 TL %6 tek arabulucu / %9 çoklu; asgari anlaşma ücreti ticari + ortaklığın giderilmesi 13.000 TL, diğerleri 9.000 TL; dava şartında ilk 2 saat Bakanlık bütçesinden. Taraf sayısı grid'i ADB'nin taranmış PDF'inden elle çıkarılmalı.)
7. `calc_smm` (serbest meslek makbuzu: KDV %20, stopaj %20, net-brüt)
8. `calc_gecikme_zammi` (6183 sayılı kanun, amme alacakları; aylık oran dönem tablosu)

Süre hesaplama:
9. `calc_sure` (istinaf 2 hafta, temyiz 2 hafta/15 gün, icra itiraz 7 gün, cevap 2 hafta; tebliğ tarihinden, tatil günü uzatma kuralı; adli tatil 20 Temmuz - 31 Ağustos)
10. `calc_zamanasimi` (TBK/TCK süreleri, kesme/durma olayları) - v2, içtihat gerektirir

Vergi ve harç:
11. `calc_tapu_harci` (binde 20 alıcı + binde 20 satıcı)
12. `calc_damga_vergisi` (sözleşme binde 9,48 vb. kalem tablosu)
13. `calc_veraset_intikal` (dilim tablosu + istisna tutarları)
14. `calc_kira_artis` (TÜFE 12 aylık ortalama; TCMB EVDS'den otomatik çekilebilir)

Diğer:
15. `calc_adli_para_cezasi` (gün x günlük miktar, hapse çevirme)
16. `calc_miras_pay` (saklı pay: altsoy 1/2, ana-baba 1/4, eş duruma göre) - v2
17. `calc_nafaka` (içtihat aralığı, kesin formül yok) - v3, düşük öncelik
18. `calc_is_kazasi_tazminati` (bedensel tazminatın SGK rücu farklı varyantı) - v2

## 5. Kur ve altın servisi (hesap girdisi + genel araç)

Yeni tool: `get_exchange_rates` (döviz + altın; hem ajan aracı hem faiz/tazminat hesaplarında girdi).

Kaynaklar (ücretsiz, resmi):
- **TCMB today.xml** (birincil, anahtarsız): `https://www.tcmb.gov.tr/kurlar/today.xml`; arşiv `https://www.tcmb.gov.tr/kurlar/YYYYMM/GGAAYYYY.xml`. Hafta içi ~15.30 güncellenir.
- **TCMB EVDS API** (ücretsiz anahtar, JSON): kur serileri `TP.DK.*`, altın serileri, reeskont/avans faiz serileri. Tarihsel veri buradan. Doküman: https://evds2.tcmb.gov.tr/index.php?/evds/userDocs
- Gram altın: resmi tek kaynak yok; `gram TL = (XAU/USD ons x USD/TRY) / 31,1035` ya da EVDS İAB serileri. Truncgil benzeri topluluk API'leri sadece çapraz doğrulama için.

Uygulama: `TcmbRatesClient` (named HttpClient, `bedesten` pattern'i kopyala) + günlük cache. Faiz tool'u ileride EVDS'den avans oranı serisini otomatik izleyebilir (yeni 3095 m.1 formülü yıllık otomatik değiştiği için değerli).

## 6. Uygulama notları

- Tool adları `calc_*` / `get_exchange_rates`; permission olarak yeni yetki açmaya gerek yok, hesaplamalar veri okumuyor: `RequiredPermission = null` (herkese açık) önerilir.
- Her tool çıktısına `tarife_donemi` ve `kaynak` alanı koy (LLM kullanıcıya "AAÜT 2025-2026'ya göre" diyebilsin).
- Her tarife dosyasında `RG tarih/sayı` yorumu zorunlu; tarife güncelleme PR'ları sadece Tariffs klasörüne dokunmalı.
- Test: her tool için unit test + bilinen örnek doğrulamaları (ör. Consulto/HakHesap ile çapraz sağlama).

## 7. Koda almadan önce göz kontrolü gerekenler

1. AAÜT maktu: FSHM (45k mi 55k mi) ve idare-duruşmalı (35k mi 40k mi); RG eki PDF taranmış görüntü, elle bakılacak.
2. Arabuluculuk saatlik ücret taraf-sayısı grid'i: ADB PDF'inden elle.
3. Avans faizi 2023 öncesi dönemleri: TCMB tebliğ arşivi.
4. Araç değer kaybı km katsayı tablosu ara basamakları: ZMSS Genel Şartları eki.
5. Çocuk hükümlü infaz katsayıları: 5275 m.107/5 metni.
