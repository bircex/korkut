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

## Akış (hedeflenen davranış)

1. Tip tespit edilir → `intake(<type_id>)` yüklenir.
2. Konuşmadan **eksik `sorular`** çıkarılır; varsa Korkut **tek mesajda** sorar (belgeyi yazmaz).
3. Cevaplar toplanınca `kosullar` değerlendirilir → ek sorular/bölümler/uyarılar devreye girer.
4. `zenginlestir` yönergeleriyle canlı mevzuat/emsal çekilir, atıflar **teyit** edilir.
5. Belge yazılır; `kontroller` bir son-checklist olarak editörde gösterilir.
