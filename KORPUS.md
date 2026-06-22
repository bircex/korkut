# Korpüs Stratejisi — Research & Dilekçe Veri Erişimi

> Karar: **Şimdi RAG (sorgu-anında).** İleri için kademeli yol haritası aşağıda.
> Bağlam: KAYNAKLAR.md (kaynaklar), RAKIPLER.md §2b (veri kamuya açık).

## 0. Rate limit gerçeği (tasarımı belirleyen kısıt)
- **Bedesten API** (ölçülen, vendor 2026-05-08): **IP başına 30 sn'de 10 istek**
  (≈ 1 istek/3 sn steady; ≈ 20/dk; ≈ 28.800/gün teorik 7/24). 429 + `Retry-After` döner.
- Diğer uçlar (Yargıtay/Danıştay native, UYAP Emsal, mevzuat.gov.tr) için limit belgesiz →
  benzer ihtiyatla davran, ölçerek doğrula.
- **Kaba indirme matematiği:** 3.5M karar ÷ ~28.800/gün ≈ **120+ gün** (tek IP, sadece
  doküman). 10–15M → **yıllar**. → "Hepsini indir" gerçekçi değil; **odaklı** olmalı.
- **Mevzuat** on binler mertebesinde → **tamamı günler içinde aynalanabilir.**

## 1. İlkeler
- **RAG-first:** kaba eleme devletin aramasında (canlı), ince sıralama + sentez bizde.
- **Asla kaba kuvvet toplu indirme:** rate-limit + ToS. Crawl her zaman kibar (limit içi).
- **Atıf doğrulama zorunlu:** GPT'nin attığı her karar/madde API'den teyit edilir
  (uydurma atıf = 0). Kalite sıçramamız bu.
- **Kademeli:** kullanımdan biriken + odaklı index; "tüm Türkiye" değil "bizim pratiğimiz".

## 2. Faz A — RAG (ŞİMDİ uygulanacak)
Sorgu akışı (research ve dilekçe-atıf için aynı):
```
soru → (canlı) devlet aramasıyla top-N aday karar/mevzuat
     → küçük aday kümesini anlamca sırala (gerekirse on-the-fly embed)
     → GPT kaynaklı sentez (özet + atıflar)
     → ATIF DOĞRULAMA: her karar/madde API'den var mı diye teyit
     → Korkut yanıtı (yalnızca doğrulanmış atıflarla)
```
- **Tembel cache:** çekilen doküman + meta yerel DB'ye → tekrar çağrı azalır (rate-limit dostu).
- **Kibar rate limiter:** Bedesten 10/30s'e uy; 429'da `Retry-After` bekle; paralel
  tekrarlı çağrıdan kaçın (token-bucket — vendor'da hazır mantık var).
- Tool'lar: `tools/ictihat_ara`, `tools/mevzuat_ara`, `tools/karar_metin` (MIMARI.md §12).

## 3. Faz B — Odaklı yerel index (orta vade)
- **Mevzuatı TAM ayna:** küçük olduğu için tamamı yerel DB'ye + güncel tut → mevzuat
  sorguları tamamen yerel, hızlı, limitsiz.
- **İçtihatta odaklı index:** ofisin pratik alanlarını (örn. boşanma/icra/kira/ceza)
  hedefli + kibar gece crawl'la ve kullanımla biriken cache'ten büyüt → **pgvector** ile
  semantik arama.
- **Embedding:** içtihat/mevzuat KAMUYA AÇIK → bulut embedding KVKK-temiz (müvekkil verisi
  değil); veya GPU gelince yerel embedding (veri hiç çıkmaz). Bkz. KAYNAKLAR.md (yerel
  embedding seçeneği .env'de mevcut).
- Artımlı güncelleme: günlük yeni kararlar/Resmî Gazete → index'e ekle.

## 4. Faz C — Geniş korpüs (yalnızca gerekirse)
- Gerçek: tek IP'den 10M+ çekmek **yıllar**. Seçenekler:
  - (a) **Uzun-soluklu/çok-IP kibar crawl** (aylar-yıllar; ToS dikkat),
  - (b) **Lisanslı veri** (Lexpera/Kazancı) — en zengin + eski/dijitalleşmemiş içerik,
  - (c) **Odaklı kapsamla yetinmek** (çoğu ofis için yeterli).
- GPU-destekli yerel embedding + tam yerel semantik arama.
- Bu fazı GPU/yerel-model fazına bağla; gerekmedikçe açma.

## 5. Çapraz konular
- **Cache + artımlı güncelleme** (her fazda).
- **Atıf doğrulama** (her fazda — kalite çapası).
- **Rate-limit ölçümü:** limitleri periyodik doğrula (değişebilir), config'ten ayarla
  (`BEDESTEN_RATE_*`).
- **ToS/hukuki:** otomatik erişimde her portalın şartlarına saygı; UYAP (kendi dosyalar) ayrı.
- **Ölçeklenince DB:** Postgres + pgvector; doküman metni + vektör + meta.

## 6. Özet
| Faz | Ne | Ne zaman |
|-----|----|----|
| **A — RAG** | canlı arama + ince sıralama + sentez + atıf doğrulama + tembel cache | **şimdi** |
| **B — Odaklı index** | mevzuat tam ayna + pratik-alan içtihat index (pgvector) | orta vade |
| **C — Geniş korpüs** | uzun crawl / lisanslı veri + GPU embedding | gerekirse (GPU fazı) |
