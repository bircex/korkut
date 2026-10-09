# Chat UX Yeniden Tasarımı: Claude/ChatGPT Sınıfı Akış + Editör Bileşenleşmesi

Hedef: Korkut sohbeti, modern LLM sohbet arayüzleriyle (Claude, ChatGPT) aynı mimaride çalışsın:
düşünme ayrı akar, araç adımları dürüst durum çipleriyle görünür, belge üretimi chat'e inline
kart olarak düşer ve editör bağlama göre drawer ya da tam sayfa açılır.

## 1. Bulgular (mevcut durumun analizi)

### B1 — `open_document` sinyali HİÇ bağlanmamış (ölü akış)
`SessionChatService.RunTurnAsync` her turda `OpenDocument: null` döndürüyor (koddaki not:
"wired in later brain slices" — hiç bağlanmamış). Yani model sohbette belge yazsa bile UI'a
"editörü aç" sinyali gitmiyor. Chat→belge zinciri kopuk.

### B2 — Taslaklar `pending`'e gidiyor, UI pending'i hiç göstermiyor → "içi dolmuyor"
`DraftingService` üretilen markdown'ı `StagePendingAsync` ile BEKLEYEN düzenleme olarak yazar
(insan onayı tasarımı: accept/revert endpoint'leri hazır). Ama `DocumentPanel` yalnız
`content_md` render ediyor; `pending` alanını okuyan/onaylatan hiçbir UI yok. Sonuç: taslak
üretilse de editör boş görünür. Ayrıca editörde "Korkut'a yazdır" (draft) tetikleyen bir UI da
yok — AI belge yazımı uçtan uca öksüz.

### B3 — Editör global bir header düğmesi; bağlam yok
"Editör" toggle'ı her sohbette görünüyor; belge olsun olmasın. Doğrusu: belge chat'te
üretildiğinde/güncellendiğinde inline bir belge kartı düşer (artifact deseni), karta tıklayınca
drawer açılır. Header'da düğme olmaz. Belgeler sekmesinde ise belge tam sayfa açılır.

### B4 — Sinyal düzeni ilkel
Bugünkü SSE: `step`(Türkçe serbest metin) / `token` / `tool` / `sources` / `done`. Mesaj-parça
modeli yok; düşünme ile cevap aynı balona akıyor; araç yaşam döngüsü (başladı→girdi→sonuç) yok;
reload'da araç izi/düşünme kaybolur (yalnız sources persist).

## 2. Best practice araştırması (özet)

- **Mesaj = tipli parça dizisi.** Vercel AI SDK v5/v6 standardı: her mesaj `text`, `reasoning`,
  `tool-*`, `source-*`, `data-*` parçalarının dizisi; SSE'de her parça start→delta→end yaşam
  döngüsüyle, ayırt edici `type` alanıyla akar. UI render'ı parça döngüsüdür. Bu, ChatGPT ve
  Claude'un da temel modelidir.
- **Düşünme ayrı ve varsayılan katlanmış.** Claude extended thinking / ChatGPT o-serisi deseni:
  cevabın üstünde "Düşünüyor…" bloğu; canlıda soluk metin akar, bitince "Düşündü · 12 sn"
  başlığıyla katlanır; tıklayınca açılır. Dürüst etiket ("Düşünüyor", "İçtihat aranıyor") kullan.
- **Araç adımları = progress-steps deseni.** Ara işler (arama, araç çağrısı) katlanabilir bir iz
  olarak gösterilir; varsayılan sakin, istenirse denetlenebilir. Her araç çağrısı kendi durumunu
  taşır: çalışıyor → sonuçlandı (ör. "yargitay araması · 12 sonuç").
- **Akış hijyeni**: token-token render, üretim sırasında görünür Durdur düğmesi, bitince
  kopyala/yeniden üret aksiyonları.
- **DeepSeek uyumu (sağlayıcımız)**: thinking modunda delta'lar `reasoning_content` ve `content`
  olarak AYRI gelir (önce reasoning akar) → reasoning parçasına birebir eşlenir. Kritik kural:
  V4'te araç çağrısı içeren turda `reasoning_content` bir SONRAKİ isteğe geri verilmek zorunda
  (yoksa 400) — tool loop'ta saklanmalı.

## 3. Hedef mimari

### Backend — SSE v2 (tipli parçalar)
`POST /sessions/:id/chat/stream` yeni sözleşme (eski step/token yerine):

```
start           {message_id}
reasoning-delta {text}                    // DeepSeek reasoning_content → buraya
text-delta      {text}                    // nihai cevap
tool-start      {call_id, name, label}    // "İçtihat aranıyor (yargitay)"
tool-result     {call_id, ok, summary}    // "12 sonuç" / hata özeti
data-sources    {items: SourceRef[]}      // mevcut sources çerçevesi bu şemaya taşınır
data-document   {doc_id, title, action}   // taslak oluştu/güncellendi → chat'e belge kartı
finish          {reply, title, sources, document}
error           {detail}
```

- `IAgentToolLoopService` callback'leri tek `onEvent(StreamPart)` arayüzüne toplanır (bugünkü
  onStep/onToken/onTool üçlüsü yerine) — sinyal işlemi tek tipte, genişletilebilir.
- Sağlayıcı katmanı: `ChatStreamEvent`'e `ReasoningDelta` eklenir; DeepSeek thinking mode açılır,
  reasoning tool-turlarında bir sonraki isteğe geri beslenir (V4 kuralı).
- **Persistans**: `chat_messages.parts_json` (yeni kolon): reasoning metni (kırpılmış, ör. 20k),
  araç izi özetleri, belge kartı referansı. `sources_json` mevcut haliyle kalır. Reload'da
  düşünme bloğu ve araç izi geri gelir.
- `open_document` bağlanır: agent belge yazdığında/`draft` aracını kullandığında `data-document`
  parçası + `finish.document` dolar (B1 fix).

### UI — parça render'ı
- `MessageParts` bileşeni: reasoning bloğu (katlanabilir, canlıda akan soluk metin), araç durum
  çipleri, metin (markdown), belge kartı, kaynak chip'i. Mesaj tipi `ChatMessage.parts[]` olur.
- **Belge kartı**: "📄 Boşanma Dilekçesi — taslak hazırlandı · Aç" → editör DRAWER'ı açılır.
  Header'daki Editör toggle'ı KALDIRILIR.
- **Editör bileşeni bağlamdan bağımsızlaşır** (`DocumentEditor` zaten öyle): Chat'te
  `RightDrawer` içinde; Belgeler sekmesinde `/belgeler/:id` rotasında TAM SAYFA.
- **Pending taslak UI'ı**: editörde "Korkut'un taslağı bekliyor" bandı + Kabul Et / Geri Al
  (backend accept/revert hazır) — kabulde içerik editöre işlenir (B2 fix).
- Durdur düğmesi (AbortController → fetch iptali), bitmiş mesajda kopyala.

## 4. Faz planı (her faz bağımsız deploy edilebilir)

| Faz | İş | Not |
|-----|----|----|
| A | Belge akışını diriltme: `open_document`/`data-document` bağla, pending kabul/geri-al UI'ı, chat'te belge kartı, header toggle'ın kaldırılması, Belgeler'de tam sayfa rota | B1+B2+B3; drawer altyapısı hazır |
| B | SSE v2 tipli parça protokolü + `onEvent` birleşik callback + UI parça render'ı | UI+backend aynı anda deploy (tek geçiş); `finish` eski `done` alanlarını taşır |
| C | Reasoning uçtan uca: DeepSeek thinking mode, reasoning-delta, katlanabilir Düşünüyor bloğu, `parts_json` persist, V4 geri-besleme kuralı | Maliyet notu: thinking token'ları governor'a sayılır |
| D | Cila: Durdur, kopyala/yeniden üret, araç çipi ayrıntıları, e2e senaryoları | |

## 5. Açık kararlar

- Reasoning persist edilsin mi, ne kadarı? (Öneri: evet, kırpılmış; kullanıcı sonradan "neden bu
  kararı verdi" diye bakabilmeli — Claude davranışı.)
- Belge kartı geçmiş mesajlarda da mı dursun? (Öneri: evet — `parts_json`'dan.)
- Eski sohbetler: parça verisi olmayan mesajlar düz metin olarak render edilmeye devam eder
  (geriye dönük dönüşüm yok).
