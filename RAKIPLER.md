# Rakipler — Türkiye Hukuk Yapay Zekâ Ürünleri

> Durum: **Ön-inceleme** (hızlı tarama). Kullanıcı talebi: bunları rakip olarak kaydet;
> **detaylı araştırma SONRA yapılacak** (bkz. §3). Hedef: bu ürünlerin kalitesinin
> **çok ötesine** geçmek.
>
> Not: Bizim arayüzümüz bunlar gibi **web chat** değil — **Mattermost-native** (mobil,
> ekip kanalları + DM), tek kimlik Korkut, **veri ofiste** (self-hosted). Bu, çoğu rakibe
> karşı yapısal bir fark (KVKK/veri egemenliği + ekip iş akışı).

## 1. Rakip listesi (ön-inceleme)

| # | Ürün | Geliştirici | Ne yapıyor | Fiyat (görünen) |
|---|------|-------------|-----------|-----------------|
| 1 | [LawChat](https://www.lawchat.com.tr/) | LawChat (TR) | Dilekçe üretimi, emsal analizi, UYAP entegrasyonu, mobil app | gizli ("ücretsiz dene") |
| 2 | [De Jure AI](https://www.dejure.ai/) | De Jure | Derin araştırma, dilekçe/sözleşme, UYAP/UETS, mahkeme takibi | **açık: 1.500–6.800₺/ay**, yıllık 60–80K₺ |
| 3 | [LexChat](https://lexchat.ai/) | UC Yazılım (İst., 2025) | **Agentic**: otonom ajanlar, dava dosyası yönetimi, 10M+ karar, belge üretimi (tracked changes, UDF) | gizli |
| 4 | [FulLegal](https://www.fullegal.com/hukukcu-yapay-zeka) | FulLegal Yazılım | İçtihat/mevzuat Q&A, belge analizi (20 evrak), UYAP masaüstü ajan, dilekçe | gizli (Pro/Ultra/Max) |
| 5 | [HukukChat](https://hukukchat.com/) | **Opex AI Labs** | Fine-tuned hukuk LLM'leri, çok-katmanlı mimari, mevzuat/içtihat/doküman analizi | gizli |
| 6 | [Apilex](https://www.apilex.ai/) | Av. Cebrail Ergül + ekip | Semantik karar arama, projeler, **Agent Beta** (derin araştırma); TR+FR+DE | gizli (giriş sonrası) |
| 7 | [LawyerTeam](https://lawyerteam.pro/) | 1999'dan beri + Dece Software | Tam **ofis otomasyonu** + AI: 15M karar, 20K mevzuat, 200+ dosya formatı, icra/strateji | gizli |
| 8 | [hukuk.chat (VeriUs)](https://verius.com.tr/tr/urunlerimiz/hukuk.chat) | **VeriUs** | Türk hukuku AI asistanı (app: ai.hukuk.chat); soru-cevap, belge | gizli |
| 9 | [Lexpera LEXI](https://www.lexpera.com.tr/ayrintilar/lexi) | Lexpera | LEXPERA içeriği (1.6M+ karar) üstüne AI asistan; **beta** | gizli (beta) |
| 10 | [Mevzuat AI](https://mevzuat.ai/tr/) | (belirsiz) | **AI Agent** mimarisi: resmi DB'lerden mevzuat/içtihat, belge analizi, dilekçe | gizli |
| 11 | [Ayboğa AI](https://ayboga.av.tr/ayboga-ai/) | Ayboğa Avukatlık (Ankara) | 3.5M+ emsal, mevzuat arama, dilekçe/sözleşme üretimi + analiz | gizli |
| 12 | [hukukchat.com](https://hukukchat.com/) → bkz. #5 | — | (Opex; #5 ile aynı) | — |

> Ön-incelemede ek olarak görülen oyuncular (araştırmaya dahil edilebilir): **YasalZeka**,
> **Kılavuz** (kilavuztek.com), **Lawantra**, **LegalEngine**, **Sinerji Hukuk Asistanı**.

## 2. Ortak desenler (sektör "table stakes")
Neredeyse hepsinde olan: **semantik emsal karar arama**, **dilekçe/sözleşme üretimi**,
**mevzuat/içtihat Q&A (kaynak atıflı)**, **belge/UYAP analizi**, doküman yönetimi.
Birçoğu "**agentic**" iddiasında (LexChat, mevzuat.ai, Apilex). Veri ölçeği reklamı:
3.5M–15M karar. **Hemen hepsi web-app chat UI + bulut SaaS, tek kullanıcı odaklı.**

### Bizim ayrışma noktalarımız (çok daha ileride olmak için)
- **Veri egemenliği:** Self-hosted, veri ofiste kalır → rakiplerin bulut SaaS'ına karşı
  güçlü KVKK/gizlilik avantajı.
- **Ekip iş akışı:** Mattermost (kanal + DM, çok kullanıcı) → tekil web chat'lerin ötesi.
- **Proaktiflik:** Sadece soru-cevap değil; **duruşma hatırlatma + takvim + cron** ile
  öngörülü asistan.
- **İnsan onayı yerleşik:** Onaysız kalıcı işlem yok (doğal dil onay diyaloğu).
- **Açık veri katmanı:** yargi-mcp/mevzuat-mcp + Bedesten API (rakiplerle aynı kaynaklar).
- **Sağlayıcı-bağımsız + kendi framework:** GPT↔Claude↔yerel; kilit yok.

## 3. Detaylı araştırma — BEKLİYOR (kullanıcı talebi: "daha sonra")
Her rakip için derinlemesine incelenecek (deep-research):
- Tam özellik matrisi + bizimkiyle karşılaştırma
- Fiyatlandırma katmanları (gizli olanlar için kayıt/teklif)
- Hangi LLM/altyapı, veri kaynakları, ölçek iddialarının doğruluğu
- Gizlilik/KVKK duruşu (bulut mu, veri nerede), UYAP entegrasyon derinliği
- Güçlü/zayıf yönler, kullanıcı yorumları, pazar konumu
- Bizim "çok daha ileride" olmamız için somut fırsat alanları
> Hazır olunca `deep-research` ile tek tek veya toplu çalıştırılabilir.
