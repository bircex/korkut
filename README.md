# Korkut

Bir avukatlık ofisinin günlük işlerini (duruşma hatırlatma, dilekçe taslağı, hukuki
araştırma) **Mattermost** üzerinden yürüten, **veriyi ofiste tutan** asistan. Kullanıcı
tek kimlikle — **Korkut** — konuşur; içeride uzmanlık modülleri paralel çalışır.

> ⚠️ Bu repo özeldir. Müvekkil verisi (`data/`), sırlar (`.env`) ve yedekler **asla**
> repoya girmez (bkz. `.gitignore`).

## Dokümanlar
- **[PLAN.md](PLAN.md)** — yol haritası, ilkeler, fazlar, teknoloji, kapsam kararları
- **[MIMARI.md](MIMARI.md)** — teknik tasarım: süreç topolojisi, mesaj akışı, core,
  veri modeli, Korkut'un iç yapısı, **tools katmanı (§12)**, onay akışı
- **[KAYNAKLAR.md](KAYNAKLAR.md)** — hukuki veri kaynakları ve erişim (içtihat/mevzuat)
- **[GUVENLIK.md](GUVENLIK.md)** — tek-kapı Mattermost, mobil, 2FA, ağ sertleştirme

## Özet mimari
- **Arayüz:** self-hosted Mattermost (tek kapı, mobil, 2FA zorunlu); kanal + DM
- **Korkut:** tek kimlik; içeride uzmanlık modülleri — yönlendirme, duruşma, dilekçe, araştırma (paralel)
- **Model:** bulut = GPT (rumuzlanmış metinle); yerel model donanım gelince
- **Yetenekler:** `tools/` altında, CLI'dan çalışan, Korkut'a configure edilen araçlar
- **Veri:** Postgres + `data/dosyalar/`; hepsi sunucuda

## Durum
Tasarım tamam (PLAN/MIMARI/KAYNAKLAR/GUVENLIK). Sıradaki: **Faz 0** (Linux kurulum).
