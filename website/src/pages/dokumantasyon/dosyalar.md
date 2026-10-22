---
layout: ../../layouts/DocLayout.astro
slug: dosyalar
---

Korkut'ta her şey dosya etrafında döner: sohbetler, belgeler, duruşmalar, süreler, görevler, notlar ve mali kayıtlar bir dosyaya bağlanır ve o dosyanın ekibi tarafından görülür.

## Dosya açmak

**Dosyalar** ekranında **+ Yeni dosya** ile başlık, müvekkil, mahkeme, esas numarası, dava türü ve karşı tarafı girerek dosya açarsınız. Dosya açmak "Dosya açar" iznine bağlıdır; dosyayı açan kişi otomatik olarak dosyanın **sorumlusu** olur.

Dosya listesi aranabilir ve sayfalıdır; yalnızca görebildiğiniz dosyalar listelenir.

## Dosya sayfası ve sekmeleri

Bir dosyayı açtığınızda üstte sekmeler belirir. Her sekme yalnızca ilgili izne sahip olana görünür:

| Sekme | İçerik | Gereken izin |
|---|---|---|
| Özet | Dosya bilgileri, durum, duruşmalar ve süreler | Dosyayı görmek |
| Sohbetler | Dosyanın sohbetleri, yeni sohbet | Dosyalarında sohbet eder |
| Belgeler | Yüklenen belgeler ve taslaklar | Belgeleri görür |
| Ajanda | Dosyanın duruşma, süre, görev ve randevuları | Duruşma, süre ve görevleri görür |
| Notlar | Görüşme notları, yeni not | Notları ve evrakı görür |
| Müvekkil | Müvekkil kartı | Müvekkilin adını görür |
| Ekip | Dosya ekibi | Dosyayı görmek |
| Mali | Dosyanın faturaları, masrafları ve toplamları | Fatura, masraf ve tahsilatları görür |

İzin verilmeyen bir sekmeye doğrudan bağlantıyla gelinirse Özet açılır.

## Duruşmalar ve süreler

Özet sekmesinden **+ Duruşma** (tarih, saat, tür, yer) ve **+ Süre** (son gün, tür) eklersiniz. Süreler yaklaştığında ve duruşmalardan önce dosya ekibine hatırlatma gider. Ayrıntılar: [Takvim, görevler ve hatırlatmalar](../ajanda/).

## Dosya ekibi

Her dosyanın bir ekibi vardır ve **yalnızca ekipteki üyeler dosyayı ve dosyaya bağlı kayıtları görür**. "Bütün dosyaları görür" iznine sahip olanlar (varsayılan olarak yönetici) her dosyayı görür.

Ekip sekmesinde:

- **Ekibe ekle** ile büro üyelerinden birini seçip eklersiniz.
- **Sorumlu yap** ile bir üyeyi dosyanın sorumlusu yaparsınız; bir dosyanın birden fazla sorumlusu olabilir.
- **Çıkar** ile üyeyi ekipten çıkarırsınız; dosyayı hemen görmez olur.

Ekibi, "Dosya ekibini yönetir" iznine sahip olan **dosyanın sorumlusu** ya da bütün dosyaları gören biri değiştirebilir. Ekip değişiklikleri erişim kayıtlarına yazılır.

## Görmediğiniz dosyalar

Ekibinde olmadığınız bir dosya sizin için yoktur: listede görünmez, bağlantısıyla açılmaz ("bulunamadı"), takvimde, görevlerde ve aramada çıkmaz, asistan da onun hakkında bilgi vermez. Bu kural veritabanında da (satır düzeyi güvenlik) uygulanır.
