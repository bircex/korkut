---
layout: ../../layouts/DocLayout.astro
slug: roller-ve-yetkiler
---

Bir hukuk bürosunda herkesin her şeye erişmesi gerekmez. Korkut'ta her üyenin bir **rolü** vardır; rol, üyenin hangi **izinlere** sahip olduğunu belirler. Bunun üstüne **dosya ekibi** kuralı gelir: üyeler yalnızca ekibinde oldukları dosyaları görür.

## Üye davet etmek

**Ayarlar → Üyeler** ("Üye davet eder, çıkarır, rol atar" izni):

1. Kişinin adını, e-postasını ve rolünü girip **Davet oluştur**a basın.
2. Korkut davet bağlantısını e-postayla gönderir (e-posta sunucusu ayarlıysa); bağlantıyı kopyalayıp kendiniz de iletebilirsiniz.
3. Davetli bağlantıyı açar, kendi parolasını belirler ve büroya katılır.

Aynı ekrandan üyenin rolünü değiştirebilir veya üyeyi çıkarabilirsiniz. Büroda her zaman en az bir yönetici kalır.

## Hazır roller

| Rol | Özetle |
|---|---|
| **Yönetici** | Her şey. Bütün dosyaları, müvekkilleri ve mali kayıtları görür; ayarları ve rolleri yönetir. İzinleri değiştirilemez. |
| **Avukat** | Ekibindeki dosyalarda tam çalışma: dosya açar ve düzenler, sorumlusu olduğu dosyanın ekibini yönetir, sohbet ve kişisel sohbet, belge, ajanda, not, müvekkil (iletişim ve kimlik dahil), araştırma. Mali kayıtları görmez. |
| **Stajyer** | Ekibindeki dosyaları görür, sohbet eder, belge ve ajanda ekler, araştırma yapar. Not ekleyemez; müvekkil iletişim ve kimlik bilgilerini göremez. |
| **Sekreter** | Dosya açar ve düzenler, belge, ajanda ve not işlerini yürütür, bütün müvekkilleri ve bilgilerini görür. Sohbet kullanmaz. |
| **Muhasebe** | Fatura ve masrafları görür ve girer, büro panosunu ve bütün müvekkil listesini görür. Dosyaları görmez. |
| **Okuyucu** | Ekibindeki dosyaları okur, dosya sohbetlerinde sohbet eder ve araştırma yapar. Kişisel sohbet açamaz, müvekkil iletişim ve kimlik bilgilerini göremez, dosyaya kayıt ekleyemez. |

## Kendi rolünüzü tanımlamak

**Ayarlar → Roller** ekranı izinleri satır, rolleri sütun olarak gösteren bir tablodur ("Rolleri düzenler" izni). Hazır rollerin izinlerini değiştirebilir ya da **yeni rol** oluşturabilirsiniz (ör. "Danışman": yalnızca atandığı dosyaları okur ve sohbet eder). Değişiklik o roldeki herkes için **bir sonraki istekte** geçerli olur; yeniden giriş gerekmez. Kullanımda olan bir rol silinemez.

## İzinlerin tam listesi

| Grup | İzin | Açıklama |
|---|---|---|
| Dosyalar | Ekibinde olduğu dosyaları görür | |
| | Bütün dosyaları görür | Ekibinde olmasa da |
| | Dosya açar | Açan dosyanın sorumlusu olur |
| | Dosya bilgilerini düzenler | |
| | Dosya siler | |
| | Dosya ekibini yönetir | Sorumlusu olduğu dosyalarda |
| Sohbet | Dosyalarında sohbet eder | |
| | Kişisel sohbet açar | Yalnızca kendisinin göreceği, dosyaya bağlı olmayan sohbet |
| Belgeler | Belgeleri görür | |
| | Belge yükler ve taslak hazırlar | |
| | Belge siler | |
| Ajanda | Duruşma, süre ve görevleri görür | |
| | Duruşma, süre ve görev ekler, düzenler | |
| Notlar ve evrak | Notları ve evrakı görür | |
| | Not ve evrak ekler | |
| Müvekkiller | Müvekkilin adını görür | |
| | İletişim bilgilerini görür | Telefon, e-posta, adres |
| | Kimlik bilgilerini görür | T.C. kimlik no, doğum tarihi |
| | Müvekkil ekler, düzenler | |
| | Bütün müvekkil listesini görür | Dosyası olmasa da |
| Mali | Fatura, masraf ve tahsilatları görür | |
| | Fatura ve masraf girer | |
| | Büro panosunu görür | |
| Araştırma | İçtihat ve mevzuat arar, karar kaydeder | |
| Yönetim | Üye davet eder, çıkarır, rol atar | |
| | Rolleri düzenler | |
| | Büro ayarlarını değiştirir | Kişilik, bildirimler |
| | Yapay zeka ve Telegram ayarlarını yönetir | |
| | Erişim kayıtlarını görür | |

## Yetkiler her yerde aynı

Aynı kurallar web uygulamasında, asistanın araçlarında ve Telegram'da uygulanır. İzni olmayan bir işlem için düğme gösterilmez; doğrudan denenirse sunucu reddeder ve deneme [erişim kayıtlarına](../erisim-kayitlari/) yazılır. Dosya kapsamı ayrıca veritabanında satır düzeyi güvenlikle korunur.
