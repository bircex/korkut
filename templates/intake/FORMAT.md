# İntake (Soru-Kontrol) Format Şartnamesi (Korkut AI)

> Bu dosya, bir dilekçe/sözleşme tipi için Korkut'un **belgeyi yazmadan ÖNCE** ne soracağını,
> hangi koşullara göre dallanacağını, neleri kontrol edeceğini ve içeriği nasıl
> zenginleştireceğini tanımlar. `catalog.json` tipin *kimliğini*, `_base.json`/`FORMAT.md`
> *biçimini* tanımlar; bu dosya ise tipin **olgusal/hukuki muhakemesini** tanımlar.
>
> Her tip için `templates/intake/<type_id>.json` dosyası. Tüm alanlar Türkçedir; `<type_id>`
> `catalog.json`'daki `id` ile birebir aynıdır. Dosya yoksa Korkut eski tek-atışlık davranışına
> döner (eksikleri `[köşeli parantez]` ile bırakır).

## Üst düzey alanlar

| Alan | Tip | Açıklama |
|---|---|---|
| `tip` | string | `catalog.json` id'si (zorunlu). |
| `ad` | string | İnsan-okur başlık (bilgi amaçlı). |
| `dayanak` | string[] | Tipin ana mevzuat dayanağı (ör. `"TMK m.598"`). Doğrulama bunları teyit eder. |
| `sorular` | Soru[] | Yazmadan önce sorulacak alanlar (intake). |
| `kosullar` | Kosul[] | Cevaba göre dallanma: ek soru / ek bölüm / uyarı. |
| `kontroller` | string[] | Yazıldıktan sonra yapılacak gözden geçirme/usul kontrolleri (checklist). |
| `zenginlestir` | Zenginlestir | İçeriği güçlendirmek için canlı araştırma yönergeleri. |

## `Soru`

```jsonc
{
  "id": "muris_ad",            // benzersiz anahtar (koşullarda referans verilir)
  "soru": "Muris (vefat eden) ad-soyadı?",
  "tip": "metin",              // metin | tarih | sayi | evet_hayir | secim
  "secenekler": ["..."],       // yalnızca tip=secim ise
  "zorunlu": true,             // false ise sorulur ama boş geçilebilir
  "neden": "HMK m.119/d konu"  // niçin gerekli — hem avukata ipucu hem modele bağlam
}
```

## `Kosul`

`eger` basit bir ifadedir: `<soru_id> <op> <değer>`, op ∈ `==, !=, >, <, >=, <=`.
Tarihlerde `gun_farki(<soru_id>) > 90` gibi yardımcılar kullanılabilir (runtime hesaplar).

```jsonc
{
  "eger": "vasiyetname_var == evet",
  "sor": ["vasiyet_acildi_mi"],         // tetiklenirse sorulacak EK sorular (id listesi)
  "ekle": "Vasiyetnamenin açılması/tenkis ihtimaline değin bir paragraf ekle",
  "uyari": "Vasiyetname veraset ilamında saklı tutulur (TMK m.598/son)."
}
```

## `Zenginlestir`

```jsonc
{
  "mevzuat_dogrula": ["TMK m.495-501", "TMK m.598"],  // canlı mevzuat.gov.tr ile teyit edilecek maddeler
  "emsal_ara": "veraset ilamı görevli mahkeme",        // search_yargitay/emsal sorgusu
  "vurgula": ["zümre sistemi ve pay oranları"]          // açıklamalarda öne çıkarılacak noktalar
}
```

## Miras: deliller / görev / yetki / süre (`dava-workflow.json`)

Bu dosyaya **delilleri, görevli mahkemeyi, yetkiyi veya süreleri YAZMA.** Bunlar arketip
düzeyinde `templates/dava-workflow.json` içinde bir kez tanımlıdır ve tipe **miras** kalır.
Çözüm zinciri:

```
type_id → catalog.json'daki kategori → dava-workflow.kategori_eslesme → dava_turu
        → dava_turleri[dava_turu].{deliller, gorevli_mahkeme, yetki, dava_sarti, kritik_sureler}
```

Yani `intake/<id>.json` **ince** kalır: yalnızca o tipe **özel** `sorular` / `kosullar` /
`kontroller` / `zenginlestir`. Delil soruları (`deliller[].soru`) ve görev/süre uyarıları
runtime'da arketipten otomatik eklenir. Tipe özgü ek bir delil varsa (ör. FSH'de tescil
belgesi) onu yine de bu dosyada `sorular`'a ekleyebilirsin; çakışma olursa intake önceliklidir.

> `kategori_eslesme`'de `null` olan kategoriler (Kanun Yolları, AYM/AİHM) miras almaz —
> o tiplerde delil/süre tamamen tipe özel intake'te tanımlanmalıdır.

## Akış (hedeflenen davranış)

1. Tip tespit edilir → `intake(<type_id>)` yüklenir; arketip mirası (deliller/görev/süre) çözülür.
2. Konuşmadan **eksik `sorular` + miras `deliller[].soru`** çıkarılır; Korkut **tek mesajda** sorar
   (belgeyi yazmaz). `zorunlu` deliller önce; yoksa `celp=true` ise mahkemeden istenebileceği belirtilir.
3. Cevaplar + delil envanteri toplanınca `kosullar` değerlendirilir → ek soru/bölüm/uyarı devreye girer.
4. `zenginlestir` yönergeleriyle canlı mevzuat/emsal çekilir, atıflar **teyit** edilir.
5. Belge yazılır; her vakıa bir delile bağlanır (`deliller[].ispat`); `kontroller` + HMK m.119
   kritik bent taraması son-checklist olarak editörde gösterilir.
