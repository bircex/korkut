---
layout: ../../layouts/DocLayout.astro
slug: telegram
---

Korkut'u Telegram'dan da kullanabilirsiniz: büronun kendi botuyla özel olarak yazışır, belge ve fotoğraf gönderir, hatırlatmaları alırsınız. Bir Telegram grubunu bir dosyaya bağlayıp dosyanın ekibiyle birlikte de çalışabilirsiniz. Web uygulamasındaki yetki kuralları Telegram'da aynen geçerlidir.

## Büro botunu kurmak (yönetici)

1. Telegram'da [@BotFather](https://t.me/BotFather) ile yeni bir bot oluşturun ve verdiği **anahtarı** (token) kopyalayın.
2. Korkut'ta **Ayarlar → Telegram** bölümüne anahtarı yapıştırın, Telegram üzerinden mesaj alışverişine onay verin ve botu etkinleştirin. Korkut anahtarı Telegram'a doğrulatır ve şifreleyerek saklar.

Bu ayar "Yapay zeka ve Telegram ayarlarını yönetir" izni gerektirir. Aynı ekranda bağlı üyeleri görebilir, bir üyenin bağlantısını kaldırabilirsiniz.

## Hesabınızı bağlamak (her üye)

**Ayarlar → Hesap** bölümünde **Telegram'ı bağla**'ya basın. Açılan bağlantı Telegram'da bota tek kullanımlık bir kodla başlar ve hesabınız bağlanır. Kodun süresi kısadır; süresi geçerse yenisini alın. Bundan sonra bota yazdığınız her mesaj, uygulamadaki yetkilerinizle sizin adınıza çalışır.

## Özel sohbette komutlar

| Komut | Ne yapar |
|---|---|
| `/dosyalar` | Görebildiğiniz dosyaları listeler; bulunduğunuz dosya işaretlidir |
| `/dosya <ad ya da esas no>` | O dosyaya geçer ve dosyada yeni bir sohbet başlatır |
| `/kisisel` | Kişisel sohbete döner ("Kişisel sohbet açar" izni gerekir) |
| `/yeni` | Bulunduğunuz yerde (aynı dosyada ya da kişisel) yeni bir sohbet başlatır |
| `/yardim` | Kısa kullanım bilgisi |

Dosya adı aramasında büyük küçük harf ve Türkçe karakter fark etmez ("kira tespit" yazmanız "KİRA TESPİT"i bulur). Birden fazla dosya eşleşirse liste gelir, tam adla ya da esas numarasıyla seçersiniz. Ekibinde olmadığınız bir dosyayı isterseniz cevap **"Bu dosya bulunamadı veya yetkiniz yok."** olur; dosyanın var olup olmadığı belli edilmez.

Bota belge veya fotoğraf da gönderebilirsiniz (Telegram'ın sınırı nedeniyle en fazla 20 MB); ne istediğinizi açıklamaya yazın.

## Grupları bir dosyaya bağlamak

1. Botu Telegram grubuna ekleyin ve **grupta yönetici yapın**. Yönetici olunca bot gruba katılanları ve ayrılanları görebilir.
2. "Dosya ekibini yönetir" iznine sahip ve dosyayı görebilen bir üye grupta `/baglan <ad ya da esas no>` yazar. Grup o dosyaya bağlanır ve dosyada ortak bir sohbet açılır.
3. Bağı kaldırmak için aynı yetkideki biri `/ayir` yazar. `/yeni` grupta yeni bir sohbet başlatır.

Bot grupta yalnızca kendisine yazıldığında cevap verir: bir komutla, `@botadı` ile anıldığında ya da mesajına yanıt verildiğinde. Grup kendi arasında konuşurken araya girmez.

### Grubun cevap alma şartları

Bot bir grupta dosya hakkında bilgi vermeden önce iki şeyi denetler:

1. **Yazan kişi** Korkut'a bağlı bir üye olmalı ve dosyanın ekibinde bulunmalı. Değilse cevap: **"Bu dosya için yetkiniz yok."**
2. **Gruptaki herkes** Korkut'a bağlı ve dosyanın ekibinde olan üyelerden oluşmalı. Telegram botlara grup üyelerinin listesini vermez; bu yüzden Korkut gruba katılan, ayrılan ve yazan herkesi izler ve gruptaki kişi sayısıyla karşılaştırır. Tanımadığı ya da yetkisi olmayan biri varsa kimseye içerik vermez: **"Bu grupta dosyaya yetkisi olmayan kişiler olduğu için içerik paylaşamıyorum."**

> Bot yönetici yapılmadan önce gruba katılmış kişileri bilemez. Böyle biri varsa, o kişi grupta bir kez yazana kadar bot içerik paylaşmaz.

Müvekkil iletişim ve kimlik bilgileri gruplarda, kimin sorduğundan bağımsız olarak **hiç paylaşılmaz**; bunlar için özel sohbeti kullanın.

## Hatırlatmalar

Bağlı üyeler kendi hatırlatmalarını özelden alır: dosyalarının süre ve duruşmaları ile kendilerine atanan görevler. Dosyaya bağlı bir grup varsa dosyanın süre ve duruşma hatırlatmaları gruba da gider; yine yalnızca gruptaki herkes dosyayı görebiliyorsa. Bkz. [Takvim, görevler ve hatırlatmalar](../ajanda/).
