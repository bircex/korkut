# Denkleştirici Adalet İlkesi — Hesaplama Aracı Planı

> Durum: PLAN, 2026-08-07. Onay bekliyor, kod yazılmadı.
> Kaynak analiz: Yargıtay içtihat taraması (HGK, 2. HD, 3. HD, 8. HD, 13. HD), doktrin makaleleri, TCMB EVDS dokümantasyonu. Bkz. [CALC_TOOLS_PLAN.md](CALC_TOOLS_PLAN.md) — bu plan onun bir alt-fazı sayılabilir ama mimari olarak yeni bir dış-veri istemcisi gerektirdiği için ayrı dosyada tutuluyor.

## 1. Kavram: tek isim, iki farklı hesap

"Denkleştirici adalet ilkesi" Yargıtay'da **iki ayrı bağlamda** karşımıza çıkıyor. Bunları tek bir tool'da karıştırmak yanlış sonuç üretir; iki ayrı mod (ileride iki ayrı tool) gerekiyor.

| | **Mod A — Katkı payı / değer artış payı** | **Mod B — Sebepsiz zenginleşmede bedel iadesi** |
|---|---|---|
| Alan | Aile hukuku: eşler arası mal rejimi tasfiyesi | Borçlar hukuku: geçersiz (adi yazılı/tapuda yapılmamış) taşınmaz satış vaadi sözleşmesinin iadesi |
| Dayanak | 743 s. eMK (2002 öncesi, mal ayrılığı) / 4721 s. TMK m.202, 227, 230 (2002 sonrası, edinilmiş mallara katılma) | TBK m.77-78 (BK m.77-78), sebepsiz zenginleşme |
| İçtihat | HGK, 2. HD (2021 sonrası), eski 8. HD | 3. HD, 13. HD, HGK 30.04.2019 E.2017/1752 K.2019/499; HGK 30.04.2025 (taşınmaz oranı revizyonu) |
| Yöntem | **Oranlama** (katkı oranı sabit tutulur, güncel rayiç değere uygulanır) | **Sepet hesabı** (TÜFE + altın + döviz + [2025 sonrası] taşınmaz oranlarının ortalaması) |
| Dış veri ihtiyacı | Yok (bilirkişi rayiç değeri kullanıcıdan girilir) | **Var**: TÜFE endeksi, gram altın, USD/TRY — tarihsel seri |
| Dava hacmi / öncelik | Yüksek (Korkut kullanıcılarının günlük işi) | Orta |

Diğer alanlarda (miras tenkis, kira tespiti, işçi/nafaka) kavram doktrinsel olarak "denkleştirici adalet" adıyla anılmıyor; oralarda kendi formülleri var (tenkis oranı, TÜFE 12 aylık ortalama — zaten `TufeTwelveMonthAverage` ile karşılanıyor). Kapsam dışı bırakıldı.

**Öncelik sırası: Mod A → Mod B.** Mod A hiç dış veri gerektirmediği için hemen (Faz 1) uygulanabilir; Mod B yeni bir EVDS istemcisi gerektirdiği için Faz 2.

## 2. Mod A — `calc_katki_payi_alacagi`

### 2.1 Formül

```
katki_orani = katki_tutari / malin_katki_anindaki_deger
denklestirilmis_katki_payi = katki_orani × malin_guncel_rayic_degeri
```

Birden fazla katkı kalemi varsa **her kalem kendi tarihindeki mal değerine ayrı ayrı oranlanır**, sonra oranlar toplanır — kalemleri doğrudan toplayıp tek seferde oranlamak hatalıdır:

```
toplam_oran = Σ (katki_tutari_i / malin_o_tarihteki_degeri_i)
sonuc = toplam_oran × guncel_rayic_deger
```

### 2.2 1.1.2002 ayrımı (otomatik tetiklenmeli)

| Dönem | Rejim | Alacak adı | Değerleme tarihi | Faiz başlangıcı |
|---|---|---|---|---|
| 1.1.2002 öncesi edinim | Mal ayrılığı (743 s. eMK) | Katkı payı alacağı | Dava tarihi | Dava/ıslah tarihi |
| 1.1.2002 sonrası edinim | Edinilmiş mallara katılma (TMK m.202, 227, 230) | Değer artış payı alacağı | Karar (tasfiye) tarihi | Karar tarihi |

Aynı evlilikte iki dönemden de edinim varsa, tool bunları **ayrı ayrı** hesaplamalı (tek akışta karıştırmamalı) — girdi olarak `edinim_tarihi` alınıp rejim otomatik seçilmeli, kullanıcı elle seçmek zorunda kalmamalı.

### 2.3 Girdi / çıktı şeması (taslak)

```
Request:
  edinimler: [
    { katki_tutari, katki_tarihi, malin_o_tarihteki_degeri, aciklama? }
  ]
  malin_guncel_rayic_degeri
  degerleme_tarihi   // dava veya karar tarihi
  ayni_katki_var_mi   // bool, uyarı tetikler

Response:
  edinim_donemi: "2002_oncesi" | "2002_sonrasi" | "karisik"
  alacak_turu: "katki_payi_alacagi" | "deger_artis_payi_alacagi"
  kalem_bazli_oranlar: [{ katki_tutari, o_tarihteki_deger, oran }]
  toplam_oran
  denklestirilmis_alacak
  faiz_baslangic_tarihi_onerisi
  uyarilar: [...]
  kaynak
```

### 2.4 Zorunlu uyarılar (evtry `calc_nafaka` tarzı disclaimer)

- Malın güncel rayiç değeri bilirkişi (inşaat mühendisi / SPK lisanslı gayrimenkul değerleme uzmanı) tespitine dayanmalıdır; tool bu değeri hesaplamaz, sadece oranlamayı yapar.
- Katkı miktarı ispatlanamıyorsa hakim hakkaniyete göre bir oran takdir edebilir (TMK m.4); bu durumda `katki_orani` alanı doğrudan kullanıcı tarafından girilebilmeli (manuel oran modu).
- Ayni katkı (işçilik, malzeme) varsa katkı anındaki parasal karşılığın tespiti bilirkişi işidir; tool sadece girilen rakamla oranlama yapar.
- Mal elden çıkmışsa (satılmış/yanmış vb.) güncel rayiç değer yerine emsal/son satış bedeli kullanılmalı — bu da bilirkişi/manuel girdi.

## 3. Mod B — `calc_denklestirici_sepet` (sebepsiz zenginleşmede bedel iadesi)

### 3.1 Formül (sepet hesabı)

```
tufe_orani      = TUFE(degerleme_tarihi) / TUFE(odeme_tarihi)
altin_orani     = gram_altin(degerleme_tarihi) / gram_altin(odeme_tarihi)
usd_orani       = usd_try(degerleme_tarihi) / usd_try(odeme_tarihi)
tasinmaz_orani  = tasinmaz_rayic(degerleme_tarihi) / tasinmaz_rayic(odeme_tarihi)  // opsiyonel, bkz. 3.2

ortalama_oran = ortalama(secili_gostergeler)
guncellenmis_bedel = odenen_bedel × ortalama_oran
```

### 3.2 HGK 30.04.2025 revizyonu

Taşınmazın kendisi de artık "yatırım aracı" sayılarak sepete girebiliyor, **ancak** taşınmazın güncel rayiç bedelinin doğrudan (oran almadan) esas alınması hatalı bulundu — yalnızca değer **artış oranı** sepete eklenir. `tasinmaz_orani` bu yüzden opsiyonel bir gösterge olarak modellenmeli; kullanıcı hangi göstergelerin sepete gireceğini seçebilmeli (sabit kod olarak gömülmemeli), çünkü somut olayın dava tarihine göre uygulanacak içtihat değişiyor.

### 3.3 Girdi / çıktı şeması (taslak)

```
Request:
  odenen_bedel
  odeme_tarihi
  degerleme_tarihi          // dava/karar tarihi
  tasinmaz_rayic_odeme_ani? // opsiyonel, girilirse tasinmaz_orani sepete dahil edilir
  tasinmaz_rayic_guncel?
  gostergeler: ["tufe","altin","usd"] + opsiyonel "tasinmaz"

Response:
  gosterge_oranlari: [{ gosterge, deger_odeme_ani, deger_guncel, oran }]
  ortalama_oran
  guncellenmis_bedel
  kullanilan_veri_tarihleri  // TÜFE/altın/döviz'in hangi ayın/günün verisi olduğu şeffaf gösterilmeli
  uyarilar
  kaynak
```

## 4. Dış veri mimarisi — birden fazla client gerekiyor

Mevcut `Korkut.Calculators/Services/Rates/TcmbRatesClient.cs` sadece **güncel/yakın tarihli döviz bülteni** çekiyor (`/kurlar/today.xml` + günlük arşiv, API key'siz) — TÜFE endeksi veya altın fiyatı **yok**. Mevcut `TufeTwelveMonthAverage.cs` da yalnızca kira artışı için "12 aylık ortalama %" değerini elle güncellenen bir tablo olarak tutuyor; bu, Mod B'nin ihtiyacı olan **ham TÜFE endeks seviyesi** (herhangi iki tarih arasında oran almak için) değil. Mod B, gelişigüzel geçmiş tarihler (dava konusu 5-15 yıl öncesine gidebilir) arasında oran istediği için elle tutulan tablo yerine **canlı, sorgulanabilir bir seriye** ihtiyaç duyuyor — bu yüzden yeni bir istemci şart:

1. **`EvdsClient` (yeni, öncelikli)** — TCMB EVDS API (`evds2.tcmb.gov.tr`), ücretsiz kayıt + API key. Çekilecek seriler:
   - TÜFE genel endeks: `TP.FG.J0`
   - Gram altın / has altın: EVDS "Kıymetli Madenler" kataloğundan tam kod teyit edilmeli (`TP.MK.*` aralığı)
   - USD/TRY: `TP.DK.USD.A` (zaten `TcmbRatesClient` günceli veriyor ama EVDS geçmiş tarihli tek sorguda seri döndürür, günlük XML arşivini tek tek gezmekten daha verimli)
   - Not: API key header'da gönderilmeli (5 Nisan 2024 sonrası query string değil).
2. **Mevcut `TcmbRatesClient`** — çapraz doğrulama / EVDS erişilemezse yedek (güncel tarih için).
3. **TÜİK açık veri portalı (`data.tuik.gov.tr`)** — EVDS'e alternatif/doğrulama, API'si daha az olgun; birincil kaynak olarak önerilmiyor.
4. **Üçüncü parti agregatörler (Truncgil vb.)** — resmi değil, bilirkişi raporunda referans gösterilemez; en fazla "hızlı taslak" modunda, açık uyarıyla kullanılabilir. Faz 1'de dahil edilmemeli.

Bu, `Korkut.LawClients/Services/Sources/*Client.cs` altındaki 12 kaynak istemcisiyle aynı desen: her dış veri kaynağı kendi `ISourceClient`-benzeri sınıfında izole, `Korkut.Calculators/Services/Rates/` altına eklenir, DI'da assembly-scan ile otomatik kaydolur.

## 5. Ortak tuzaklar / edge case'ler

- **2005 TL/YTL dönüşümü**: 2005 öncesi tarihli tutarlarda ham TL rakamları 6 sıfır farkıyla hatalı karşılaştırılabilir. EVDS/TÜİK serileri bunu kendi içinde tutarlı veriyor ama kullanıcı elle tarihsel tutar girerse birim doğrulaması (TL mi, YTL/güncel TL mi) zorunlu — girişte yıl < 2005 ise uyarı tetiklenmeli.
- **Faiz başlangıcı**: Mod A'da katkı payı alacağında dava tarihi, değer artış payında karar tarihi; yanlış seçim faiz hesabını da bozar (bkz. `calc_faiz` ile birlikte kullanım).
- **Malın mevcut olmaması**: Mod A'da mal elden çıkmışsa güncel rayiç yerine emsal/son satış bedeli — bilirkişi/manuel.
- **Sepet gösterge seçimi (Mod B)**: sabit kod olarak gömülmemeli, kullanıcı seçmeli (bkz. §3.2).
- **Ayni katkı**: iki modda da tool sadece oranlama/endeksleme yapar, katkının parasal karşılığının tespiti bilirkişi işidir — arayüzde net şekilde belirtilmeli.

## 6. Yol haritası

**Faz 1 — Mod A (`calc_katki_payi_alacagi`), dış veri yok, hemen başlanabilir**
1. `Korkut.Contract.Calculators` içine `ContributionShareRequest`/`Response` (veya Türkçe isimlendirme, mevcut konvansiyona bak) ekle.
2. `Korkut.Tools/Calc/` içine `CalcKatkiPayiAlacagiTool.cs` (agent tool) + `CalculatorService.cs`'e karşılık gelen metod.
3. 1.1.2002 ayrımı mantığı + çoklu katkı kalemi oranlama.
4. Unit testler: tek kalem, çoklu kalem, 2002 öncesi/sonrası, manuel oran girişi, mal elden çıkmış senaryosu.
5. Doktrin/içtihat örnekleriyle çapraz doğrulama (elle hesaplanmış 2-3 örnek).

**Faz 2 — `EvdsClient` altyapısı**
1. TCMB EVDS'te API key başvurusu (kullanıcı/ekip tarafından yapılmalı — kayıt formu kişisel bilgi istiyor, ben bunu sizin adınıza dolduramam).
2. `EvdsClient`: TÜFE endeks + altın + döviz seri sorgusu, tarih aralığı desteği, hata/timeout yönetimi (`TcmbRatesClient` ile aynı desen: `HttpRequestException`/`TaskCanceledException` → `BadRequestException`).
3. Cache stratejisi (günlük/aylık; TÜFE ayda bir açıklanıyor, altın/döviz günlük).

**Faz 3 — Mod B (`calc_denklestirici_sepet`)**
1. Gösterge seçilebilir sepet hesabı + opsiyonel taşınmaz oranı (HGK 2025 revizyonu).
2. Kullanılan veri tarihlerinin şeffaf gösterimi (LLM/kullanıcı hangi ayın TÜFE'sinin kullanıldığını görebilmeli).
3. Unit + entegrasyon testleri (EVDS mock).

**Göz kontrolü / onay gereken noktalar (koda geçmeden önce)**
1. EVDS API key başvurusu — hesap sahibi tarafından yapılmalı.
2. Gram altın için EVDS'teki tam seri kodu — kataloğdan teyit edilmeli, tahmini kod kullanılmamalı.
3. Mod A/B tool isimlendirmesi ve tek tool + mod parametresi mi, iki ayrı tool mu olacağı — kullanıcı kararı.

## 7. Kaynaklar

- https://www.hukukihaber.net/denklestirici-adalet-ilkesine-yargitay-hukuk-genel-kurulunun-degisen-bakis-acisi
- https://barandogan.av.tr/blog/medeni-hukuk/katilma-katki-payi-deger-artis-payi-alacagi-mal-paylasimi-davasi.html
- https://nurogluhukuk.com/denklestirici-adalet-ilkesi-bilirkisi-raporu-tazminat-hesabi-satis-bedeli-odeme-tarihi-hesaplanmasi-rayic-deger-alim-gucu-tasinmaz/
- https://www.hukukihaber.net/enflasyonist-ortamda-denklestirici-adalet
- http://dnblegal.com.tr/tr/sebepsiz-zenginlesme-davalarinda-%E2%80%9Cdenklestirici-adalet-ilkesi%E2%80%9D-ve-uygulanmasi/
- https://kazanci.com.tr/gunluk/3hd-2017-13225.htm
- https://evds2.tcmb.gov.tr/index.php (EVDS ana sayfa)
- https://evds3.tcmb.gov.tr/tumSeriler/2501/bie_dkdovytl
- https://karakavuz.com/t-c-merkez-bankasi-evds-kayit-olma-ve-api-kullanma/
- https://herkesicin.tcmb.gov.tr/wps/wcm/connect/ekonomi/hie/icerik/enflasyon+hesaplayici (yalnızca web, API yok — çapraz doğrulama referansı)
- https://data.tuik.gov.tr/
- https://oksuz.av.tr/katki-payi-alacagi/
- https://www.2mhukuk.com/katki-payi-deger-artis-payi-ve-katilma-alacagi/
