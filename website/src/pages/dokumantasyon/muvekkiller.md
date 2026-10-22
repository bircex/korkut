---
layout: ../../layouts/DocLayout.astro
slug: muvekkiller
---

Müvekkil kartı bir kişinin veya şirketin bilgilerini tek yerde tutar: ad ya da unvan, T.C. kimlik numarası, doğum tarihi, telefon, e-posta ve adres. Müvekkil dosyalara bağlanır ve dosyanın Müvekkil sekmesinde de görünür.

## Kim hangi müvekkili görür

- "Bütün müvekkil listesini görür" iznine sahip olanlar (varsayılan olarak yönetici, sekreter ve muhasebe) bütün müvekkilleri görür.
- Diğerleri yalnızca ekibinde oldukları dosyaların müvekkillerini ve kendi ekledikleri müvekkilleri görür.

## İletişim ve kimlik bilgileri ayrı izinlere bağlıdır

| Bilgi | İzin |
|---|---|
| Ad ve unvan | Müvekkilin adını görür |
| Telefon, e-posta, adres | İletişim bilgilerini görür |
| T.C. kimlik no, doğum tarihi | Kimlik bilgilerini görür |

İzni olmayan kişi bu alanlarda **"Görme yetkiniz yok"** görür. Asistan da aynı kurala uyar: izniniz yoksa sohbette size bu bilgileri vermez. Telegram gruplarında iletişim ve kimlik bilgileri hiç paylaşılmaz.

Müvekkil bilgisini düzenleyen biri, göremediği alanların üzerine yazamaz; o alanlar olduğu gibi korunur.

## Kayıt altına alınır

Bir müvekkilin iletişim veya kimlik bilgilerinin görüntülenmesi [erişim kayıtlarına](../erisim-kayitlari/) yazılır. Kayda bilgilerin kendisi değil, hangi alanların görüntülendiği geçer.

## Bulut modelinde maskeleme

Bulut bir yapay zeka modeli kullanıyorsanız, modele giden metindeki kimlik numarası, isim, telefon, e-posta ve IBAN'lar önce takma değerlerle değiştirilir. Yerel modelde metin zaten makineden çıkmaz.
