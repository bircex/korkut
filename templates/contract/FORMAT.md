# Sözleşme Format Şartnamesi (Korkut AI)

> Kaynak: TBK (Türk Borçlar Kanunu) genel hükümler + isimli sözleşme tipleri, İş Kanunu, TTK,
> uygulama. `catalog.json` tiplerinin biçimsel kurallarını tanımlar; `_base.json` bu dizilime
> uyar. Dilekçeden farkı: sözleşme **iki taraf arası** bir metindir, mahkemeye hitap etmez.

## 1. Başlık
- Ortalı, büyük harf, sözleşme tipinin adı: `KONUT KİRA SÖZLEŞMESİ`, `GİZLİLİK SÖZLEŞMESİ (NDA)`.

## 2. Taraflar (Madde 1)
- Her taraf: **tam unvan / ad-soyad**, **TCKN (gerçek) veya VKN (tüzel)**, **tam adres**.
- Kısa tanım atanır: *"... bundan böyle \"KİRAYA VEREN\" olarak anılacaktır."* — metin boyunca bu
  kısaltma kullanılır.

## 3. Standart madde dizilimi (numaralı)
1. Başlık
2. `MADDE 1 - TARAFLAR`
3. `MADDE 2 - TANIMLAR` *(teknik terim varsa; yoksa atlanır)*
4. `MADDE 3 - KONU VE KAPSAM` — kapsam **ve kapsam dışı** birlikte
5. `MADDE 4 - TARAFLARIN HAK VE YÜKÜMLÜLÜKLERİ` — taraf bazında, alt numaralı (4.1, 4.2 …)
6. `MADDE 5 - BEDEL VE ÖDEME KOŞULLARI` — tutar (rakam+yazı), KDV, vade/yöntem, gecikme faizi
7. `MADDE 6 - SÜRE VE YENİLEME`
8. `MADDE 7 - FESİH` — haklı/haksız fesih, bildirim süresi (30/60/90 gün)
9. `MADDE 8 - GİZLİLİK` — kapsam, süre, istisna, ihlal sonucu
10. `MADDE 9 - MÜCBİR SEBEP` *(opsiyonel)*
11. `MADDE 10 - TEBLİGAT` *(opsiyonel)*
12. `MADDE 11 - UYUŞMAZLIKLARIN ÇÖZÜMÜ VE YETKİLİ MAHKEME` — yetkili mahkeme/icra veya tahkim,
    uygulanacak hukuk (Türk hukuku)
13. `MADDE 12 - YÜRÜRLÜK` — madde sayısı + nüsha adedi + tarih
14. İmzalar — her taraf için unvan + (İmza) bloğu

## 4. Tipe göre kritik maddeler
- **Kira:** bedel + artış oranı (TÜFE/yıllık), depozito, tahliye taahhüdü (ayrı), demirbaş listesi.
- **İş (İş K.):** ücret, çalışma süresi, deneme süresi, görev tanımı, fazla çalışma, gizlilik/rekabet.
- **Eser/yazılım:** teslim/kabul kriterleri, ayıp/garanti, fikri hak devri, bakım/SLA.
- **NDA:** gizli bilgi tanımı, istisnalar, süre (sözleşme sonrası da sürer), cezai şart.
- **Satış/devir:** mülkiyetin geçişi, ayıptan sorumluluk, teslim, bedel ve ödeme.
- **Hisse devri (SPA):** beyan ve tekeffüller, kapanış şartları, fiyat düzeltme.
- **Mal rejimi / boşanma protokolü:** TMK'ya uygun; mal rejimi **noterde/mahkemede** düzenlenir.

## 5. Şekil / geçerlilik notları
- Bazı sözleşmeler **resmî şekil** ister: taşınmaz satış vaadi ve mal rejimi sözleşmesi **noterde**;
  taşınmaz satışı **tapuda**. Korkut taslağı hazırlar, geçerlilik için şekil şartını **uyarır**.
- E-imza: 5070 s. Kanun — güvenli e-imza ıslak imza hükmündedir.
- Cezai şart (TBK m.179 vd.) ve gecikme faizi açıkça yazılmalı.

## 6. UYAP / dışa aktarım
Sözleşme dilekçe değildir; mahkeme sunumu gerekmez. Yine de editör DOCX/PDF (ve gerekirse UDF)
olarak dışa aktarır. Biçim (ortalı başlık, numaralı maddeler, imza blokları) korunur.

## Kaynaklar
- Sözleşme standart yapısı — https://norgay.com.tr/blog/sozlesme-nasil-yazilir
- Sözleşme hazırlarken dikkat — https://serkahukuk.com/sozlesme-hazirlarken-dikkat-edilecekler/
- Sözleşme türleri — https://kurekci.av.tr/sozlesme-turleri-nelerdir-cesitler-ve-ozellikler/
- Gizlilik sözleşmesi (NDA) — https://mihci.av.tr/gizlilik-sozlesmesi-ornegi/
- Kira sözleşmesi — https://www.ahmetalkan.av.tr/kira-sozlesmesi/
