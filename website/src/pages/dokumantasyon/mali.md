---
layout: ../../layouts/DocLayout.astro
slug: mali
---

**Mali** ekranı büronun faturalarını, masraflarını ve tahsilat durumunu tek yerde gösterir. Mali kayıtlar en hassas bilgiler arasında olduğundan ayrı izinlere bağlıdır: görmek "Fatura, masraf ve tahsilatları görür", girmek "Fatura ve masraf girer". Varsayılan olarak yalnızca **yönetici** ve **muhasebe** bu izinlere sahiptir.

## Özet

Ekranın üstünde dört toplam görünür: **kesilen** faturalar, **tahsil edilen**, kalan **bakiye** ve toplam **masraf**.

## Faturalar

**+ Fatura** ile tutar, KDV oranı, müvekkil, dosya, açıklama ve tarih girersiniz. Bir fatura **Taslak**, **Gönderildi**, **Tahsil edildi** veya **İptal** durumunda olabilir; ödenen faturayı listede **Tahsil edildi** ile işaretlersiniz.

## Masraflar

**+ Masraf** ile harç, bilirkişi ücreti, tebligat, keşif gibi giderleri tutar, tür, açıklama, tarih ve dosyayla birlikte kaydedersiniz.

## Dosya bazında

Dosya sayfasının **Mali** sekmesi o dosyanın faturalarını, masraflarını ve toplamlarını gösterir; bu sekme de yalnızca mali izni olanlara görünür.

## Kayıt altına alınır

Mali kayıtların her görüntülenmesi ve izni olmayan birinin mali kayıtlara erişme denemesi [erişim kayıtlarına](../erisim-kayitlari/) yazılır.
