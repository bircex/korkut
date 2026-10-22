---
layout: ../../layouts/DocLayout.astro
slug: belgeler
---

Büroya gelen ve büronun hazırladığı belgeler Korkut'ta iki yerde durur: **yüklenen belgeler** (dilekçe örnekleri, sözleşmeler, bilirkişi raporları, taramalar) ve asistanın ürettiği **taslaklar**. Taslaklar için bkz. [Dilekçe ve sözleşme taslakları](../dilekce-ve-sozlesme/).

## Belge yüklemek

Dosya sayfasının **Belgeler** sekmesindeki **Belge yükle** ile ya da bir sohbete ek olarak belge yüklersiniz. Desteklenen biçimler PDF, DOCX, TXT, PNG ve JPG'dir. Yüklemek "Belge yükler ve taslak hazırlar" iznine bağlıdır.

## Güvenlik taraması ve OCR

Her yüklenen belge önce karantinaya alınır ve taranır. Belgenin durumu listede görünür:

| Durum | Anlamı |
|---|---|
| Yükleniyor | Dosya aktarılıyor |
| Tarama bekliyor | Güvenlik taraması sürüyor |
| İşleniyor (OCR) | Taranmış belgenin metni okunuyor |
| Hazır | Önizlenebilir ve indirilebilir |
| Metin çıkarılamadı | Belge hazır, ancak içinden metin okunamadı |
| Reddedildi | Tarama belgeyi güvensiz buldu; açılamaz |

Önizleme ve indirme yalnızca taramadan geçmiş belgelerde mümkündür.

## Dosyaya, müvekkile, tarafa bağlamak

Bir belgeyi **Bağla** ile bir dosyaya, müvekkile veya karşı tarafa bağlarsınız. Belge, bağlı olduğu dosyanın ekibine görünür hale gelir. Ekibinde olmadığınız bir dosyaya belge bağlayamazsınız.

## Kim hangi belgeyi görür

- Belgeyi yükleyen kişi her zaman görür.
- Belge bir dosyaya (doğrudan ya da dosyanın bir tarafı üzerinden) bağlıysa o dosyanın ekibi görür.
- Bütün dosyaları gören biri her belgeyi görür.

Silmek ayrıca "Belge siler" iznine bağlıdır.

## Belge içinde arama

Ayarlar'da bir belge arama (embedding) modeli seçtiyseniz, yüklenen belgelerin içeriği aranabilir hale gelir ve asistan cevaplarını bu belgelere dayandırabilir. Asistan yalnızca sizin görebildiğiniz belgelerde arar; bir dosya sohbetinde ise yalnızca o dosyanın belgelerinde. Bkz. [Yapay zeka modelleri](../yapay-zeka/).
