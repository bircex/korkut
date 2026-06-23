# Dilekçe Format Şartnamesi (Korkut AI)

> Kaynak: HMK m.119 (dava dilekçesi), HMK m.129 (cevap dilekçesi), İYUK m.3 (idari dava),
> İİK matbu takip örnekleri. Bu dosya `catalog.json`'daki tiplerin **biçimsel** kurallarını
> tanımlar; `<id>.json` şablonları bu dizilime uyar. Detay/araştırma için bkz. "Kaynaklar".

## 1. Mahkeme başlığı (hitap)
- Büyük harf, datif ek + kesme işareti: `İZMİR NÖBETÇİ ASLİYE HUKUK MAHKEMESİ'NE`
- İlk dava açılışında genelde `NÖBETÇİ` eklenir (tevzi). Cevap/ara dilekçelerde gerçek mahkeme
  adı + **ESAS NO** yazılır: `... 3. ASLİYE HUKUK MAHKEMESİ'NE \n DOSYA NO: 2026/123 E.`
- İdari yargı: `... İDARE MAHKEMESİ BAŞKANLIĞI'NA`, `... VERGİ MAHKEMESİ BAŞKANLIĞI'NA`,
  `DANIŞTAY BAŞKANLIĞI'NA` (İYUK m.3 — imzalı dilekçeyle açılır).
- "Gönderilmek üzere" hâli (yetkili yer farklıysa): `... NÖBETÇİ ... MAHKEMESİ'NE \n Gönderilmek üzere \n ... NÖBETÇİ ... MAHKEMESİ'NE`

## 2. HMK m.119 — dava dilekçesi zorunlu unsurları
| Bent | Unsur | Eksikse |
|---|---|---|
| a | Mahkemenin adı | **Süre verilmez → dava açılmamış sayılır** |
| b | Davacı/davalı ad-soyad-adres | 1 hafta kesin süre |
| c | Davacı T.C. kimlik no | 1 hafta |
| ç | Kanuni temsilci / vekil bilgileri | 1 hafta |
| d | Konu + (malvarlığı davalarında) dava değeri | **Süre verilmez** |
| e | Vakıaların sıra no'lu açık özetleri | **Süre verilmez** |
| f | Her vakıanın hangi delille ispatlanacağı | **Süre verilmez** |
| g | Dayanılan hukuki sebepler | **Süre verilmez** |
| ğ | Açık talep sonucu | 1 hafta |
| h | İmza | 1 hafta |

**Kritik (a, d, e, f, g):** Korkut bu alanları asla boş/eksik bırakmamalı; eksikse kullanıcıya
sormalı. Diğerleri eksik kalsa düzeltilebilir ama yine de istenir.

## 3. Standart bölüm dizilimi ve etiketler
Sıra (etiket varyasyonları `|` ile):
1. Mahkeme başlığı (§1)
2. `DAVACI`  — Ad Soyad (T.C. ...), adres
3. `VEKİLİ`  — Av. Ad Soyad, baro/sicil, adres *(varsa)*
4. `DAVALI`  — Ad Soyad, adres *(bilinmiyorsa "adres araştırması talep olunur")*
5. `KONU | DAVA KONUSU` — tek cümle talep özeti + (gerekiyorsa) `Dava Değeri`/`Harca esas değer`
6. `AÇIKLAMALAR | OLAYLAR` — vakıalar **sıra numarasıyla** (1., 2., 3. ...), kronolojik
7. `HUKUKİ NEDENLER | HUKUKİ SEBEPLER` — dayanılan mevzuat (HMK, TMK, TBK, İİK ... ilgili m.)
8. `DELİLLER | HUKUKİ DELİLLER` — her vakıaya bağlı; "ve her türlü yasal delil" ibaresi
9. `SONUÇ VE İSTEM | SONUÇ VE TALEP | NETİCE-İ TALEP` — numaralı talepler + **yargılama gideri ve
   vekâlet ücretinin karşı tarafa yükletilmesi** talebi
10. Tarih (sağ üst veya sonuç altı)
11. İmza bloku: `Davacı | Davacı Vekili \n Av. Ad Soyad`

## 4. Cevap dilekçesi (HMK m.129) sapmaları
- Başlık: gerçek mahkeme + `DOSYA NO: .../... E.`
- `CEVAP VEREN (DAVALI)` → `VEKİLİ` → `DAVACI` → `KONU` (cevaplarımızın sunumu)
- **Önce usule ilişkin itirazlar** (görev, yetki, derdestlik, husumet, zamanaşımı vb.),
  **sonra esasa cevap** (`CEVAPLARIMIZ | ESASA CEVAPLARIMIZ`).
- Süre: dava dilekçesinin tebliğinden **2 hafta** (ek süre HMK m.127 ile 1 ay).
- Talep: `SONUÇ VE İSTEM` → "davanın reddi" + gider/vekâlet ücreti.

## 5. Kanun yolu dilekçeleri
- İstinaf/temyiz: başlıkta hem **karar veren mahkeme** hem **gönderileceği BAM/Yargıtay** belirtilir;
  `... BÖLGE ADLİYE MAHKEMESİ İLGİLİ HUKUK DAİRESİ'NE \n Sunulmak üzere \n ... MAHKEMESİ'NE`.
- Zorunlu: `KARAR` (esas/karar no, tarih), `İSTİNAF/TEMYİZ NEDENLERİ`, `SONUÇ VE İSTEM` (kaldırma/bozma).
- Süre tutum dilekçesi: gerekçeli karar tebliğinden önce süreyi korur; gerekçeli kararı bekler.

## 6. İcra dilekçeleri (serbest metin DEĞİL — matbu form)
İcra takip talepleri İİK'nin resmî **"Örnek No"** matbu formlarıyla açılır; alan doldurma mantığı:
- `Örnek No:1` — İlamsız takip (genel haciz yolu) ödeme emri
- `Örnek No:7` — Kambiyo senetlerine özgü takip
- `Örnek No:10` — İlamlı takip icra emri
- `Örnek No:13` — Kira/tahliye
Bu tiplerde Korkut serbest paragraf değil **form alanları** (alacaklı, borçlu, alacak miktarı,
faiz başlangıcı, dayanak belge) doldurur.

## 7. UYAP / UDF notu
Mahkemeye sunum **UDF** (UYAP Doküman Formatı) ile yapılır; başlık/bölümler düz paragraf
stilleriyle taşınır (bkz. `DOCUMENT_EDITOR.md §7`). Biçim (kalın etiketler, ortalı başlık) UDF
paragraf stillerine map'lenir.

## Kaynaklar
- HMK m.119 — https://barandogan.av.tr/blog/mevzuat/hmk-madde-119-dava-dilekcesinin-icerigi.html
- HMK m.129 — https://barandogan.av.tr/blog/mevzuat/hmk-madde-129-cevap-dilekcesinin-icerigi
- Dava dilekçesi içeriği (HMK 119) — https://minvalhukuk.com/hmk-maddeleri/hmk-madde-119-dava-dilekcesinin-icerigi/
- Dava dilekçesi hazırlama — https://www.safsoz.av.tr/dava-dilekcesi-nasil-hazirlanir
- Dava/cevap dilekçesi (m.119–126) — https://www.ekinhukuk.com.tr/dava-ve-cevap-dilekcesi-hmk-m-119-m-126/
- İdari dava dilekçesi (İYUK m.3) — https://kadimhukuk.com.tr/makale/idari-dava-dilekcesi-ornegi-iyuk-3-madde/
