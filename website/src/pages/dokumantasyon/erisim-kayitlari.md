---
layout: ../../layouts/DocLayout.astro
slug: erisim-kayitlari
---

Erişim kayıtları, büroda hassas bilgiye kimin ne zaman baktığını ve yetkilerde neyin değiştiğini gösterir. **Ayarlar → Erişim kayıtları** ekranında, "Erişim kayıtlarını görür" iznine sahip olanlara (varsayılan olarak yalnızca yönetici) görünür.

## Neler kaydedilir

| Olay | Ne zaman |
|---|---|
| Müvekkil bilgisi görüntülendi | Bir müvekkilin iletişim veya kimlik bilgileri müvekkil kartında ya da dosyanın Müvekkil sekmesinde gösterildiğinde |
| Mali kayıtlar görüntülendi | Faturalar, masraflar veya mali özet açıldığında |
| Üyenin rolü değişti / Üye çıkarıldı | Ayarlar → Üyeler'de |
| Dosya ekibine eklendi / çıkarıldı / sorumlu değişti | Dosyanın Ekip sekmesinde |
| Rol oluşturuldu / izinleri değişti / silindi | Ayarlar → Roller'de |
| Erişim reddedildi | Bir üye izni olmayan bir işlemi denediğinde |
| İki adımlı doğrulama açıldı, sıfırlandı, kurtarma kodu kullanıldı | Hesap güvenliği olaylarında |

Her satırda zaman, kişi, olay ve ayrıntı görünür. Reddedilen erişimlerde hangi adrese istek yapıldığı yazar.

## Kayıtta değer değil, alan adı tutulur

Müvekkil bilgisi görüntülemelerinde kayda **hangi alanların** görüldüğü yazılır ("e-posta, telefon, kimlik no"), bilgilerin kendisi yazılmaz. Böylece erişim kayıtları, koruduğu verinin bir kopyasına dönüşmez.

## Süzmek ve geriye gitmek

**Tür** seçimiyle kayıtları müvekkil bilgisi, mali, dosya ekibi, üyeler, roller veya reddedilen erişimler olarak süzebilirsiniz. Liste en yeniden eskiye sıralıdır; **Daha eski kayıtlar** ile geriye gidilir.

## Kayıtlar değiştirilemez

Erişim kayıtları yalnızca eklenir; uygulama hesabının kayıtları düzenleme veya silme yetkisi yoktur.
