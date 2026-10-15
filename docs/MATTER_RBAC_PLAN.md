# Dosya merkezli yapı ve yetki (RBAC) planı

Durum: taslak, karar bekliyor (2026-10-15). Kararlar §11'de.

## 1. Hedef

Korkut bir avukatlık bürosunda çalışır ve büroda herkesin her şeye yetkisi yoktur. Bu plan üç şeyi birlikte getirir.

1. **Dosya merkezli yapı (Codex gibi):** her şey bir dosyanın altında yaşar. Sohbetler, belgeler, duruşmalar, süreler, görevler, notlar, evrak, dosyanın müvekkili ve mali kayıtları buna dahildir. Sol menü bir dosya listesidir ve her dosyanın altında sohbetleri görünür.
2. **İzin tabanlı roller:** bir rol, izinlerin adlandırılmış bir kümesidir. Hazır roller gelir, yönetici yenisini tanımlayabilir.
3. **Dosya kapsamı:** kimse büronun tamamını görmez. Bir üye yalnızca ekibinde olduğu dosyaları ve o dosyalara bağlı kayıtları görür. Yönetici her şeyi görür. Mali kısımları yalnızca mali izni olan görür.

Bir işlemin yapılabilmesi için iki koşulun birlikte sağlanması gerekir:

> **İzin** (rolden gelir: "müvekkil kimlik bilgisini görebilir mi?") **ve** **kapsam** (dosya ekibinden gelir: "bu dosyayı görebilir mi?").

Örnek: "Okuyucu" rolündeki bir stajyer bir dosyanın ekibinde olabilir. O dosyanın sohbetlerini ve belgelerini görür ve sohbet edebilir. Ama müvekkilin TC kimlik numarasını, telefonunu ve adresini görmez, mali sekmeyi hiç görmez.

## 2. Bugünkü durum (kısa)

Ayrıntılı tarama 2026-10-15'te yapıldı.

- **İzin kataloğu:** 11 izin var (`Korkut.Core/Permissions.cs`). Roller sabit, `owner` ve `lawyer` (`Roles.cs`). `lawyer` rolünde mali okuma ve yazma da var.
- **Özel roller kullanılmıyor:** `office_custom_roles` tablosu var ama girişte hiç okunmuyor (`AccessTokenAuthHandler` yalnızca yerleşik rolü açıyor). Rol değiştirme ucu her metni kabul ediyor.
- **Kapsam yok:** her servis, her araç, RAG ve RLS yalnızca büroya (`office_id`) bakıyor. Bugün her üye:
  - her dosyayı görüyor,
  - her üyenin sohbetini okuyup yazabiliyor,
  - her müvekkilin kimlik bilgisini görüyor (dosya detayına müvekkil kartı gömülü).
- **Dosyada ekip kavramı yok.** `Matter`'da oluşturan, sorumlu ya da ekip alanı yok.
- **Sohbetler büro geneline açık.** `Conversation.MatterId` var ama ne listeleme ne de sohbet turu bunu kullanıyor.
- **Ajan araçları yalnızca izne bakıyor.** Dosyaya bakmıyor; `get_matter` herhangi bir dosyayı açıyor.
- **RAG belge parçalarında dosya bilgisi yok.** Arama büronun bütün belgelerinde yapılıyor.
- **Telegram yalnızca özel sohbetleri yanıtlıyor.** Sohbet hiçbir dosyaya bağlanmıyor.
- **Açık uç:** `PATCH /api/matters/{id}`'de yetki niteliği yok ve hiçbir yerde fallback policy yok. İşaretsiz her uç anonim kalıyor.

## 3. Roller ve izinler

### 3.1 İzin kataloğu

İzinler gruplara ayrılır. Sistem koda gömülü bu listeyi bilir; rol ekranı bu listeden seçtirir.

| Grup | İzin | Ne verir |
|---|---|---|
| Dosya | `matters.view_all` | Ekibinde olmasa bile bütün dosyaları görür (yönetici) |
| | `matters.create` | Yeni dosya açar (açan otomatik olarak dosyanın sorumlusu olur) |
| | `matters.edit` | Görebildiği dosyanın bilgilerini düzenler, kapatır, yeniden açar |
| | `matters.delete` | Dosya siler |
| | `matters.manage_team` | Görebildiği dosyanın ekibine üye ekler veya çıkarır |
| Sohbet | `chat.use` | Görebildiği dosyalarda sohbet eder |
| | `chat.general` | Dosyaya bağlı olmayan kişisel sohbet açar (araştırma, hesap makineleri) |
| Belge | `documents.read`, `documents.write`, `documents.delete` | Görebildiği dosyaların belgeleri ve taslakları |
| Ajanda | `agenda.read`, `agenda.write` | Duruşma, süre, görev ve randevu (görebildiği dosyalarda) |
| Not / evrak | `notes.read`, `notes.write` | İletişim notları ve gelen/giden evrak |
| Müvekkil | `clients.read` | Müvekkilin adını ve türünü görür |
| | `clients.contact` | İletişim bilgisi: telefon, e-posta, adres |
| | `clients.identity` | Kimlik bilgisi: TC kimlik no, doğum tarihi |
| | `clients.write` | Müvekkil oluşturur veya düzenler |
| | `clients.view_all` | Dosyası olmasa da bütün müvekkil listesini görür |
| Mali | `finance.read`, `finance.write` | Fatura, masraf, tahsilat (dosyada mali sekme dahil) |
| Rapor | `reports.read` | Büro geneli pano |
| Araçlar | `research.use` | İçtihat ve mevzuat arama, Kararlarım |
| Yönetim | `members.manage` | Üye davet etme, çıkarma, rol atama |
| | `roles.manage` | Rol tanımlama |
| | `office.settings` | Büro ayarları: kişilik, bildirimler |
| | `integrations.manage` | Yapay zeka ve Telegram ayarları |
| | `audit.read` | Erişim kayıtları |

Hesap makineleri izin gerektirmez; dosya verisine dokunmazlar.

### 3.2 Hazır roller (öneri, düzenlenebilir)

| İzin | Yönetici | Avukat | Stajyer | Sekreter | Muhasebe | Okuyucu |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| Bütün dosyalar (`matters.view_all`) | ✓ | | | | | |
| Dosya açma / düzenleme | ✓ | ✓ / ✓ | | ✓ / ✓ | | |
| Dosya silme | ✓ | | | | | |
| Dosya ekibini yönetme | ✓ | ✓ | | | | |
| Dosyada sohbet | ✓ | ✓ | ✓ | | | ✓ |
| Kişisel sohbet | ✓ | ✓ | ✓ | | | |
| Belge okuma / yazma | ✓ | ✓ / ✓ | ✓ / ✓ | ✓ / ✓ | | ✓ / |
| Ajanda okuma / yazma | ✓ | ✓ / ✓ | ✓ / ✓ | ✓ / ✓ | | ✓ / |
| Not ve evrak okuma / yazma | ✓ | ✓ / ✓ | ✓ / | ✓ / ✓ | | ✓ / |
| Müvekkil adı | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Müvekkil iletişim / kimlik | ✓ | ✓ / ✓ | | ✓ / ✓ | | |
| Bütün müvekkiller | ✓ | | | ✓ | ✓ | |
| Mali okuma / yazma | ✓ | | | | ✓ / ✓ | |
| Rapor | ✓ | | | | ✓ | |
| Araştırma | ✓ | ✓ | ✓ | | | ✓ |
| Yönetim izinleri | ✓ | | | | | |

`korkut-admin` Yönetici rolündedir. Yönetici rolü silinemez, izinleri daraltılamaz ve büroda en az bir yönetici kalmak zorundadır.

### 3.3 Veri modeli

- **`roles`:** id, office_id, key, ad, açıklama, `permissions text[]`, `is_system` ve oluşturma bilgisi. Bugün kullanılmayan `office_custom_roles` tablosu bu yapıya dönüştürülür.
- **`users.role`:** metin anahtarı yerine `role_id` (FK) olur. Mevcut `owner` Yönetici'ye, `lawyer` Avukat'a çevrilir.
- **İzinlerin çözülmesi:** her istekte veritabanındaki role bakılır (kısa önbellekle). Rol değişince ya da rolün izinleri değişince üyenin `security_stamp`'i döner; böylece eski token'lar geçersiz olur ve izin değişikliği hemen etkiye girer. Token'a izin listesi gömülmez; yalnızca rol kimliği taşınır.
- **Davetler:** `Invite.Role` da `role_id` olur.

## 4. Dosya kapsamı

### 4.1 Dosya ekibi

- **`matter_members`:** office_id, matter_id, user_id, `is_lead` (sorumlu avukat), added_by, added_at. Benzersizlik (matter_id, user_id) üzerindedir.
- **Dosyayı açan:** otomatik olarak sorumlu üye olur.
- **Ekibi yönetme:** `matters.manage_team` izni olan, görebildiği dosyanın ekibini yönetir.
- **Görünürlük kuralı:** bir üye bir dosyayı ancak `matters.view_all` izni varsa ya da dosyanın ekibindeyse görür.

### 4.2 Dosyaya bağlı kayıtlar

Dosyaya bağlı her kayıt dosyanın görünürlüğünü devralır, ayrıca ilgili izni ister.

| Kayıt | Görünür, eğer |
|---|---|
| Duruşma, süre | dosya görünür + `agenda.read` |
| Görev / randevu (`tasks`) | dosya görünür + `agenda.read`; **ya da** kayıt bana atanmış **ya da** benim oluşturduğum. Dosyasız kayıtlar yalnızca sahibine ve atanana görünür, yönetici hepsini görür. |
| Not, evrak | dosya görünür + `notes.read` |
| Belge, taslak, belge sürümleri | dosya görünür + `documents.read`. Dosyasız belge yalnızca yükleyene görünür. |
| Sohbet | §5 |
| Fatura, masraf | yalnızca `finance.read` (dosya ekibinde olmak yetmez) |
| Müvekkil | `clients.view_all` **ya da** müvekkilin görünür en az bir dosyası var. Alanlar izne göre maskelenir: ad için `clients.read`, iletişim için `clients.contact`, kimlik için `clients.identity`. |
| Taraflar | dosya görünür |

Gerekli şema değişiklikleri:
- `tasks.created_by` eklenir.
- `documents.matter_id` gerçekten doldurulur; bugün hep boş kalıyor, bağ yalnızca `document_relations` üzerinden kuruluyor.
- `generated_documents` sohbet üzerinden dosyaya bağlanır.
- Dosyaya bağlı tüm tablolarda (office_id, matter_id) indeksi bulunur.

### 4.3 Uygulama katmanında uygulama

- **Erişim servisi:** `IAccessScope`, isteğin başında bir kez hesaplanır. Üç şey verir:
  - **İzinler:** üyenin rolünden gelen izin kümesi.
  - **Dosya erişimi:** `CanSeeAllMatters`, ya da tümü değilse `VisibleMatterIds`.
  - **Yardımcılar:** `CanSee(matterId)` ve `Require(matterId, permission)`.
- **Liste sorguları:** her lookup ve repository listesi kapsamı parametre olarak alır. "Office'teki tüm X" sorgusu kalmaz. Değişecek yerler:
  - matter, hearing, deadline, task, note ve evrak listeleri,
  - document ve draft listeleri,
  - calendar, reports ve reminders aggregate'leri.
- **Tekil erişimler:** başkasının dosyasındaki bir kaydın kimliği bilinse bile 404 döner. 403 dönülmez, çünkü 403 kaydın varlığını sızdırır.
- **Maskeleme:** müvekkil maskelemesi DTO eşlemesinde yapılır. Maskelenen alan `null` olarak ve bir "gizli" işaretiyle döner; UI "yetkiniz yok" gösterir.
- **Eksik yetki niteliği:** fallback policy eklenir; her uç varsayılan olarak oturum ister. İşaretsiz uçlar (bugün `PATCH /api/matters/{id}`) bu adımda kapanır.

### 4.4 Veritabanı katmanında ikinci savunma (RLS)

Uygulama katmanında unutulan bir filtre veri sızdırmasın diye, dosyaya bağlı tablolara RLS'te kapsam koşulu da eklenir.

- **Oturum bilgisi:** her transaction'da büroya ek olarak `app.current_user` ve `app.all_matters` (`'1'`/`'0'`) ayarlanır.
- **Dosya görünürlük fonksiyonu:** `korkut.can_see_matter(matter_id)`, SECURITY DEFINER ve STABLE. `app.all_matters = '1'` ise ya da `matter_members` içinde (matter_id, current_user) satırı varsa doğru döner.
- **Dosyaya bağlı tablolar:** politika `office_id = … AND (matter_id IS NULL AND <sahiplik koşulu> OR korkut.can_see_matter(matter_id))` olur.
- **Arka plan işleri:** hatırlatmalar, indeksleme ve Telegram dinleyicisi açıkça "sistem kapsamı"nda çalışır (`app.all_matters = '1'`). Sonrasında bir üye adına iş yapılıyorsa o üyenin kapsamına geçer.
- **Sıralama:** bu adım en son gelir; uygulama katmanı ve testler oturduktan sonra yapılır. Performansı ölçülerek açılır.

## 5. Sohbetler dosyanın altında

- **Dosya sohbetleri:** sohbet `matter_id` ile açılır. Ekipte olan ve `chat.use` izni taşıyan herkes dosyanın sohbetlerini görür ve okur (karar §11-1).
- **Kişisel sohbetler:** `chat.general` izniyle dosyasız sohbet açılır. Bu sohbetler yalnızca sahibine görünür; araştırma ve hesap için kullanılır.
- **Sohbeti taşıma:** kişisel bir sohbet sonradan bir dosyaya taşınabilir. Bu bir kez yapılır ve geri alınamaz, çünkü sohbet o andan sonra ekibe açılır.
- **Ekipten çıkarılma:** ekipten çıkarılan üye dosyanın sohbetlerini artık göremez, kendi yazdıkları dahil.
- **API:**
  - `GET /api/matters/{id}/sessions` dosyanın sohbetlerini listeler.
  - `POST /api/matters/{id}/sessions` dosyada yeni sohbet açar.
  - `GET /api/sessions?scope=personal` kişisel sohbetleri listeler.
  - Mevcut `/api/sessions/{id}` uçları sohbetin görünürlüğünü kontrol eder.

### 5.1 Ajan ve araçlar

Ajan, sohbet eden üyenin yetkisinden fazlasını asla göremez.

- **Dosya sohbetinde:** dosyanın bilgisi sistem istemine eklenir. Araçlar varsayılan olarak o dosyaya kilitlidir: `list_tasks`, `list_deadlines`, `add_note`, belge arama ve diğerleri bu dosyayla sınırlıdır. Başka bir dosyaya ait soru gelirse ajan "bu sohbet X dosyasına ait" der.
- **Kişisel sohbette:** araçlar üyenin kapsamıyla çalışır, yani görebildiği dosyalarla.
- **`list_matters` ve `get_matter`:** yalnızca görünür dosyaları döndürür.
- **Müvekkil araçları:** maskeli DTO döndürür. Ajan maskelenmiş bilgiyi tahmin edemez ve "bu bilgiyi görmeye yetkiniz yok" der.
- **Mali araçlar ve `office_overview`:** yalnızca ilgili izinle görünür.
- **Araç listesi:** bugün olduğu gibi izne göre süzülür; artık kapsam da aracın içinde uygulanır.
- **RAG:** bilgi parçalarına `matter_id` eklenir (dosyasız belgeler için `owner_id`). Arama kapsamla süzülür; dosya sohbetinde yalnızca o dosyanın belgeleri aranır.

## 6. Arayüz (Codex benzeri)

- **Sol menü:**
  - **Dosyalar ağacı:** arama kutusu ve "Yeni dosya" düğmesi (izin varsa). Her dosya açılınca altında sohbetleri ve "Yeni sohbet" görünür. Sabitlenen ve son kullanılan dosyalar üstte durur.
  - **Kişisel sohbetler:** ayrı bir başlık altında.
  - **Büro bölümleri:** Takvim, Görevler, Müvekkiller, Mali, Raporlar, Karar Arama ve Hesap Makineleri ikincil menüde. İzni olmayan bölüm hiç görünmez.
- **Dosya sayfası sekmeleri:**
  - Özet
  - Sohbetler
  - Belgeler
  - Ajanda (duruşma, süre, görev)
  - Notlar ve evrak
  - Taraflar
  - Müvekkil (alanlar maskeli)
  - Ekip (`matters.manage_team` ile düzenlenir)
  - Mali (yalnızca `finance.read` ile)
- **Genel Takvim ve Görevler:** yalnızca kapsamdakileri gösterir. Görevlerde "Bana atananlar", "Oluşturduklarım" ve "Dosyalarımdakiler" süzgeçleri bulunur.
- **Ayarlar:**
  - **Roller:** izin matrisi, satırda izin ve sütunda rol, onay kutularıyla. Hazır roller kopyalanarak yeni rol yapılabilir.
  - **Üyeler:** rol atama, üyenin dosyaları.
- **Yetkisiz bağlantılar:** yetkisiz bir dosyaya derin bağlantıyla gelen "Dosya bulunamadı" görür.

## 7. Telegram

Telegram aynı kuralları uygular.

### 7.1 Özel sohbet

- **Yeni sohbet:** bağlı üyenin yeni sohbeti kişisel sohbet olarak başlar (`chat.general` gerekir).
- **Dosyaya geçiş:** `/dosya <ad ya da esas no>` dosyaya geçer ve o dosyada yeni bir sohbet açar. `/dosyalar` görebildiği dosyaları listeler, `/kisisel` kişisel sohbete döner.
- **Yetkisiz dosya:** üyenin görmediği bir dosya istenirse yanıt "Bu dosya için yetkiniz yok." olur. Dosyanın varlığı ima edilmez: "bulunamadı veya yetkiniz yok".

### 7.2 Gruplar

- **Gruba bağlama:** bot gruba eklenir. Ekip yönetme izni olan bir üye `/baglan <dosya>` ile grubu bir dosyaya bağlar. Bir grup tek bir dosyaya bağlı olur ve bağ `telegram_groups` (chat_id, matter_id, bağlayan) tablosunda tutulur.
- **Grubun cevap alma şartları:** grup ancak şu koşulların hepsi sağlanınca cevap alır:
  1. yazan kişi Korkut'a bağlı bir üyedir ve dosyayı görebilir (yoksa "Bu dosya için yetkiniz yok"),
  2. gruptaki **herkes** Korkut'a bağlı ve dosyanın ekibinde olan üyedir.
- **Yetkisiz biri gruptaysa:** bot "Bu grupta dosyaya yetkisi olmayan kişiler olduğu için içerik paylaşamıyorum." der ve kimseye içerik vermez.
- **Grup üyelerini bilmek:** Bot API grup üyelerinin listesini vermez. Kural şöyle uygulanır:
  - **Üyelik değişikliklerini izleme:** bot grupta yönetici yapılır, `chat_member` güncellemeleriyle katılanları ve ayrılanları izler.
  - **Sayıyı karşılaştırma:** her mesajda `getChatMemberCount` sonucu, bilinen yetkili üye sayısıyla (bot dahil) karşılaştırılır. Sayı tutmazsa, yani tanınmayan biri varsa, içerik verilmez.
  - **Yetki kaybı:** bir üye dosya ekibinden çıkarılırsa ya da yetkisi düşerse grup yeniden "paylaşılamaz" durumuna geçer.
- **Grup sohbetinde hassas veri:** müvekkil iletişim ve kimlik bilgisi gruplarda hiç verilmez. Gruptaki herkesin bu izni olsa bile özel sohbet önerilir (karar §11-6).
- **Hatırlatmalar:** dosya ekibine gider. Bağlı bir grup varsa gruba da gider; görevler atanan kişiye gider.

## 8. Denetim

- **Kayıt:** `audit_events` şu olayları kaydeder:
  - müvekkil kimlik ve iletişim bilgisi görüntüleme,
  - mali kayıt erişimi,
  - rol ve izin değişikliği,
  - ekip değişikliği,
  - reddedilen erişim denemeleri.
- **Görüntüleme:** "Erişim kayıtları" ekranından izlenir (`audit.read`).

## 9. Fazlar

Her faz ayrı PR(lar)dır. Her faz kendi testleriyle gelir ve çalışır durumda bırakır.

| Faz | İçerik |
|---|---|
| **0. Acil güvenlik** | Fallback policy (varsayılan oturum zorunlu), `PATCH /api/matters/{id}` yetkisi, işaretsiz uçların taranması |
| **1. Roller** | İzin kataloğu, `roles` tablosu ve hazır roller, `users.role_id` dönüşümü, izinlerin her istekte DB'den çözülmesi, rol değişince stamp dönmesi, Ayarlar → Roller ekranı, üyeye rol atama |
| **2. Dosya ekibi ve kapsam** | `matter_members`, `IAccessScope`, bütün servis, lookup ve endpoint'lerin kapsamla çalışması (dosya, ajanda, not, evrak, belge, takvim, rapor, hatırlatma alıcıları), müvekkil maskeleme, mali kapısı, dosya ekibi ekranı |
| **3. Sohbetler dosyada** | Dosya sohbetleri ve kişisel sohbetler, görünürlük, taşıma, yeni API'ler |
| **4. Ajan ve RAG** | Araçların kapsamla çalışması, dosya sohbetinde dosyaya kilitlenme, maskeli araç sonuçları, bilgi parçalarına `matter_id`, yeniden indeksleme |
| **5. Arayüz** | Dosya ağacı ve dosya altında sohbetler, dosya sayfası sekmeleri, izne göre menü, kapsamlı Takvim ve Görevler |
| **6. Telegram** | `/dosya`, `/dosyalar` ve `/kisisel` komutları, gruplar (`/baglan`), grup üyelik kontrolü, yetki mesajları, hatırlatmaların ekibe gitmesi |
| **7. RLS ve denetim** | `app.current_user` ve `app.all_matters`, `can_see_matter`, dosyaya bağlı tablolarda kapsamlı RLS, denetim kayıtları ekranı |

**Yetki matris testi:** her endpoint ve her ajan aracı için bir test tablosu tutulur. Tablo her hazır rolü iki durumda dener: kişi dosyanın ekibinde ve ekibinde değil. Beklenen sonuç izin verilir, 404 ya da alan maskeli olur. Faz 2'den başlar, her fazda büyür ve CI'da çalışır. Yeni bir uç ya da araç bu tabloya girmeden birleştirilemez.

## 10. Mevcut verinin taşınması

Lokal kurulum şu an boş, yalnızca `korkut-admin` var. Yine de dönüşüm her kurulumda çalışacak şekilde yazılır.

- `owner` Yönetici olur, `lawyer` Avukat olur.
- Mevcut dosyaların ekibi boş başlar; bu dosyaları yalnızca yönetici görür. Bir defalık seçenek olarak "mevcut her dosyaya bütün avukatları ekle" sunulur (karar §11-7).
- Dosyasız mevcut sohbetler oluşturanın kişisel sohbeti olur. Oluşturanı bilinmeyenler yöneticiye kalır.
- Oluşturanı bilinmeyen görevler atanan kişiye ve yöneticiye görünür.

## 11. Karar bekleyen sorular

1. **Dosya sohbetleri paylaşımlı mı?**
   - Öneri: ekipteki herkes dosyanın bütün sohbetlerini okur ve yazar.
   - Alternatif: herkesin kendi sohbeti olur; diğerleri yalnızca okur, ya da hiç göremez.
2. **Kim dosya açar ve ekibi kim yönetir?**
   - Öneri: dosyayı Yönetici, Avukat ve Sekreter açar. Ekibi Yönetici ile dosyanın sorumlu avukatı yönetir.
3. **Müvekkil listesi kimde?**
   - Öneri: tam liste Yönetici, Sekreter ve Muhasebe'de. Avukat yalnızca kendi dosyalarının müvekkillerini görür.
4. **Kişisel (dosyasız) sohbet olsun mu?** Öneri: evet, yalnızca sahibine görünür. Araştırma ve hesaplama için.
5. **Mali bilgiyi kim görür?**
   - Öneri: yalnızca Yönetici ve Muhasebe.
   - Açık nokta: avukatlar kendi dosyalarının masraflarını girebilsin mi (yazma var, okuma yok)?
6. **Telegram gruplarında müvekkil iletişim ve kimlik bilgisi hiç verilmesin mi?** Öneri: evet, yalnızca özel sohbette verilsin.
7. **Mevcut dosyalara ekip:** dönüşümde dosyalar boş ekiple mi kalsın (yalnızca yönetici görür), yoksa bütün avukatlar mı eklensin?
8. **Hazır roller:** §3.2'deki altı rol ve dağılım uygun mu, eklenecek ya da çıkarılacak rol var mı (örneğin "Ortak avukat")?
