---
layout: ../../layouts/DocLayout.astro
slug: yapay-zeka
---

Korkut'un sohbet, araştırma özeti ve taslak üretimi bir büyük dil modeliyle çalışır. Hangi modelin kullanılacağına büro karar verir. Bütün model çağrıları kurulumla birlikte gelen yapay zeka geçidinden (llmr) geçer; sağlayıcı anahtarları orada şifreli saklanır ve tarayıcıya hiçbir zaman geri gönderilmez.

Bu ayarlar **Ayarlar → Yapay zeka** bölümündedir ve "Yapay zeka ve Telegram ayarlarını yönetir" izni gerektirir.

## Yerel model: hiçbir metin dışarı çıkmaz

LM Studio, Ollama veya OpenAI uyumlu başka bir sunucuyla modeli kendi makinenizde çalıştırabilirsiniz. Bu durumda sohbet ve taslak metinleri makinenizden hiç çıkmaz.

1. Modeli LM Studio veya Ollama'da yükleyin ve yerel sunucuyu başlatın.
2. **Ayarlar → Yapay zeka → Sağlayıcı ekle** bölümünde türü **OpenAI-compatible endpoint** seçin.
3. Adres olarak `http://host.docker.internal:<port>/v1` girin. LM Studio için genellikle `1234`, Ollama için `11434`.
4. Erişim türü olarak **self-hosted** seçin ve **Test et** ile bağlantıyı doğrulayın.
5. **Modeller** bölümünde sohbet modelini seçip kaydedin.

> Korkut'un sistem istemi ve araç tanımları uzundur. Yerel modeli en az **16k bağlam** (context length) ile yükleyin. Araç çağırmayı destekleyen bir model seçin (ör. Qwen 2.5 Instruct ailesi).

## Bulut modeli

OpenAI, Anthropic veya Google Gemini için türü seçip API anahtarınızı girin, **Test et** ile doğrulayıp sohbet modelini seçin. Bulut modeli kullanıldığında modele giden metin önce bir rumuzlama katmanından geçer: T.C. kimlik numaraları, isimler, telefon, e-posta ve IBAN'lar takma değerlerle değiştirilir.

> Rumuzlama en iyi çaba ile çalışır, bir garanti değildir. Bulut sağlayıcısına veri gönderimi KVKK md. 9 kapsamında yurt dışı aktarım sayılabilir; sorumluluk büroya aittir. Bkz. [KVKK notu](../../kvkk/).

## Belge arama modeli (embedding)

Yüklediğiniz belgelerin içinde arama yapılabilmesi için ayrıca bir **embedding modeli** seçin (ör. LM Studio'da `nomic-embed-text`). Bu isteğe bağlıdır. Embedding modeli seçilmeden önce yüklenen belgeler, yeniden indekslenene kadar aramada görünmez; modeli sonradan değiştirirseniz de yeniden indeksleme gerekir.

## Kullanım ve maliyet

Aynı ekranın altında son 30 günün model bazında kullanımı (istek, token ve varsa maliyet) görünür. Yerel modellerde maliyet sıfırdır.
