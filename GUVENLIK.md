# Güvenlik Tasarımı

> Hedef: Sisteme **tek kapı Mattermost**, mobilden erişim, **2FA zorunlu**, başka hiçbir
> yerden erişim yok. Müvekkil verisi ofiste kalır (bkz. PLAN.md ilkeleri, MIMARI.md §5).

## 1. İlke: Tek kapı = Mattermost
- İnternete açık **tek servis Mattermost** (HTTPS / 443). Başka hiçbir şey dışarıdan
  erişilemez.
- `asistan` (Python), Postgres, `tools/`, OpenAI çağrıları → hepsi **localhost / iç ağ**,
  firewall arkasında. Kullanıcı bunların hiçbirine doğrudan ulaşamaz.
- Önde bir **reverse proxy** (Caddy/nginx) yalnızca Mattermost'u yayınlar; **otomatik TLS**
  (Let's Encrypt). Mobil uygulama geçerli sertifika ister.

```
İnternet ──TLS/443──> reverse proxy ──> Mattermost ──localhost──> asistan ─┬─> Postgres
                                                                            ├─> tools/
                                                                            └─> OpenAI (rumuzlu, tek dış çağrı)
  (diğer her port: yalnızca localhost'a bind, dışarıdan KAPALI)
```

## 2. Ağ sertleştirme
- **Firewall (ufw):** yalnızca 443 açık. Diğer her şey kapalı.
- **Port binding:** Postgres (5432), Mattermost iç (8065), `asistan` → sadece
  `127.0.0.1`'e bind; dışarıya açılmaz.
- **SSH (yönetim):** parola kapalı, **yalnızca anahtar**; ve sadece yönetici IP / VPN'e
  açık (mümkünse tamamen kapalı, konsol/VPN ile yönet). [KARAR-G2]
- **fail2ban** + kaba-kuvvet koruması.

## 3. Kimlik & MFA (UI'da ön planda)
- **MFA/2FA zorunlu (TOTP):** Mattermost System Console → *Enforce Multi-factor
  Authentication*. İlk girişte kullanıcıya kurulum **dayatılır** (Google Authenticator/
  Authy). MFA'sız giriş yok.
- **Açık kayıt KAPALI:** *Enable Open Server = false*. Hesaplar yalnızca **davetle/admin**
  tarafından açılır. E-posta doğrulama zorunlu.
- **Güçlü parola politikası:** min. uzunluk + büyük/küçük/sembol; giriş denemesi limiti.
- **Oturum:** makul süre + idle timeout. Mobilde uygulama içi **PIN/biyometrik kilit**.

## 4. Mobil erişim
- Mattermost mobil uygulaması; sunucu URL'si = bizim alan adımız (TLS şart).
- **Push bildirim gizliliği [KARAR-G1]:** MM mobil push varsayılan olarak Mattermost'un
  barındırdığı push proxy'sinden (HPNS) geçer → bildirim **içeriği** 3. tarafa gidebilir.
  Hukuk bürosu için kabul edilemez. Seçenekler:
  - **(a) id-only push (önerilen):** bildirimde içerik yok ("yeni mesajınız var");
    uygulama açılınca içerik sunucudan çekilir → veri sızmaz, kurulum kolay.
  - **(b) Kendi push proxy'ni self-host et:** tam kontrol, daha çok kurulum.
  - En az **(a)** uygulanacak.
- (Opsiyonel) **MDM** ile yalnızca onaylı cihazlara kurulum.

## 5. Veri güvenliği
- **Disk:** LUKS tam disk şifreleme (PLAN.md Faz 0). DB **yedekleri de şifreli**.
- **Bulut'a giden her şey rumuzlu** (redaction, MIMARI.md §5). id-only push seçilirse
  **tek dış çağrı OpenAI** olur, o da rumuzlu.
- Gizli anahtarlar (.env) repoya girmez; sunucuda dosya izinleriyle korunur.

## 6. İzleme
- Mattermost erişim/denetim logları; başarısız giriş uyarıları.
- (Compliance export Enterprise özelliği — şimdilik gerek yok.)

## 7. İzin matrisi
| Yön | İzin |
|-----|------|
| İnternet → Mattermost 443 (TLS) | ✅ tek kapı |
| İnternet → diğer her şey | ❌ kapalı |
| Mobil → Mattermost (MFA + TLS) | ✅ |
| asistan ↔ Mattermost / Postgres / tools | ✅ yalnızca localhost |
| asistan → OpenAI (rumuzlu) | ✅ tek dış çağrı |
| Yönetim SSH | anahtar + VPN/IP (veya kapalı) |

## 8. Kararlar
- **KARAR-G1 — Push gizliliği:** id-only (a) mı, self-host proxy (b) mi? (Varsayılan: a)
- **KARAR-G2 — SSH:** Yönetim için kısıtlı SSH (anahtar + IP/VPN) mi, tamamen kapalı +
  VPN-only mi?
