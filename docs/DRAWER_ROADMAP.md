# Sağ Drawer Dönüşümü: İlgili Kararlar + Editör

Referans tasarım: sohbet ekranında asistan yanıtının üzerinde "1 arama · 13 kaynak" chip'i,
yanıtın altında "Kaynaklar (13)" düğmesi ve sağda içeriği iterek açılan bir "İlgili Kararlar (N)"
drawer'ı. Drawer'da her karar bir kart: mahkeme/daire + E./K. numarası başlıkta, "İçtihat" rozeti,
karar tarihi ve kısa özet, sağ üstte dışa açma oku. Aynı drawer deseni belge editörü için de
kullanılacak; editör artık tam sayfa ayrı bir pane değil, sohbetin yanında açılan drawer olacak.

## 1. Mevcut durum

### UI (korkut-ui)

- Sohbet tek dosyada: `src/components/Workspace.tsx`. Mesajlar düz balon listesi; kaynak,
  arama sayısı veya karar kartı gösterimi yok.
- SSE tüketimi `src/api.ts` içinde `sessionChatStream`: yalnızca `step` (etiket metni),
  `token`, `done` (`reply/title/open_document/pending_email`) ve `error` olaylarını tanır.
- Editör akışı: `/sohbet/:id/belge` URL'i `pane === "document"` yapar ve kanvasın tamamı
  `DocumentPanel` → `DocumentEditor` (CKEditor, A4 kanvas) ile değişir. Header'daki
  Sohbet/Editör anahtarı bu iki tam sayfa görünüm arasında gidip gelir; sohbet ve editör
  aynı anda görünmez.
- Belgeler listesi (`DocumentLibrary`) ve sohbet içi `open_document` sinyali de aynı
  `/sohbet/:id/belge` rotasına yönlendirir.
- Genel bir drawer bileşeni yok. Tek benzer desen mobilde overlay olarak açılan sol sidebar.

### Backend (korkut-backend)

- `search_decisions` aracı (`src/modules/Korkut.Tools/Law/SearchDecisionsTool.cs`) zaten
  normalize edilmiş yapısal sonuç üretiyor: `SourceResult(Source, Id, Title, Date, CaseNumber,
  DecisionNumber, Court, Chamber, Url, Snippet, ...)`. Ancak bu veri yalnızca modele giden
  araç çıktısı metnine gömülüyor; UI'a hiç ulaşmıyor.
- `fetch_document` aracı aynı `Source + Id` ikilisiyle kararın tam metnini getirebiliyor;
  yani karar detayını UI'da göstermek için veri katmanı hazır.
- SSE üretimi `Korkut.Agent.Host/Controllers/SessionsController.cs` → `ChatStream`:
  `step/token/done/error` çerçeveleri. Araç çağrısı adımları `ToolCallingLoopService`
  içinden yalnızca "Araç çalıştırılıyor: X" etiketi olarak akıyor.
- `ChatOutcome` sözleşmesi (`Korkut.Contract/Agent/Chat/ChatOutcome.cs`):
  `Reply, Title, OpenDocument, PendingEmail`. Kaynak alanı yok.
- `ConversationMessage` entity'sinde metadata kolonu yok; kaynaklar kalıcılaştırılmazsa
  sayfa yenilenince chip ve drawer içeriği kaybolur.

## 2. Boşluk analizi

| # | Boşluk | Katman |
|---|--------|--------|
| 1 | Araç döngüsü yapısal sonuçları dışarı taşımıyor (`ToolResult` yalnız metin) | Backend |
| 2 | SSE protokolünde kaynak olayı yok; `done` payload'ında kaynak listesi yok | Backend |
| 3 | Kaynaklar mesajla birlikte kalıcılaştırılmıyor (reload'da kaybolur) | Backend + DB |
| 4 | Karar tam metnini UI'ın doğrudan çekeceği bir endpoint yok (yalnız araç var) | Backend |
| 5 | Generic sağ drawer bileşeni yok | UI |
| 6 | Mesaj balonunda arama/kaynak chip'leri yok | UI |
| 7 | Editör drawer değil, tam sayfa pane; sohbetle yan yana duramıyor | UI |

## 3. Hedef mimari

### Veri modeli: `SourceRef`

Turdaki tüm hukuki aramaların birleşik kaynağı; içtihat ve mevzuat tek listede, `kind` ile ayrılır:

```json
{
  "kind": "ictihat | mevzuat",
  "source": "yargitay",
  "id": "…",
  "title": "Yargıtay 15. Ceza Dairesi 2013/24590 E. 2013/18178 K.",
  "court": "Yargıtay", "chamber": "15. Ceza Dairesi",
  "case_number": "2013/24590", "decision_number": "2013/18178",
  "date": "2013-11-20", "snippet": "…", "url": "…"
}
```

- Chip metni turun toplamından türer: "N arama · M kaynak" (N = search_decisions çağrısı
  sayısı, M = toplam benzersiz SourceRef).
- Drawer başlığı "İlgili Kararlar (K)"; K = `kind === "ictihat"` olanlar. Mevzuat hitleri
  ayrı bir bölümde ("Mevzuat") listelenebilir, ilk fazda yalnız içtihat yeterli.

### SSE + kalıcılık

- Yeni olay: `event: sources`, data `{ items: SourceRef[] }`. Tur bittiğinde (done'dan
  hemen önce) bir kez yayınlanır; `done` payload'ına da aynı liste eklenir
  (`ChatOutcome.Sources`), böylece stream olmayan yol (upload, cloud) da kapsanır.
- `ConversationMessage`'a nullable `SourcesJson` (jsonb) kolonu; yalnız assistant
  mesajlarında dolu. `GET /api/sessions/:id` mesajlarla birlikte döndürür.
- Migration notu: migrations tek InitialCreate'e squash edilmiş durumda ve her Up()
  sonunda el yazımı RLS güvenlik eki var. `conversation_messages` zaten tenant tablosu,
  yeni tablo gerekmiyor; yalnız kolon ekleme migration'ı yeterli.

### UI yerleşimi

- `RightDrawer`: masaüstünde içeriği iterek açılan (grid/flex kardeş, animasyonlu genişlik),
  mobilde overlay. Esc ile kapanır, focus trap, `aria-modal` yalnız overlay modunda.
- İki drawer tek altyapıyı kullanır ama aynı anda tek drawer açık olur:
  - Kararlar drawer'ı: mesaj-scoped, geçici state (URL'e yazılmaz). Genişlik ~440px.
  - Editör drawer'ı: URL'den türer (`/sohbet/:id/belge` korunur, deep-link ve Belgeler
    listesi girişi bozulmaz). Genişlik `min(900px, 65vw)`; A4 kanvas dar genişlikte
    ölçeklenir (`doc-canvas` responsive geçirilecek).
- Header'daki Sohbet/Editör anahtarı kalkar; yerine editör drawer toggle'ı
  (drawer açıkken vurgulu) gelir.

## 4. Roadmap

Her faz kendi başına merge edilebilir PR dilimi olacak şekilde sıralandı.

### Faz 1: UI drawer altyapısı
- `src/components/RightDrawer.tsx`: push (masaüstü) + overlay (mobil), Esc/kapama, başlık
  yuvası, genişlik prop'u.
- `Workspace.tsx` kanvasını `flex` kardeş yapısına hazırlama (chat kolonu + drawer kolonu).
- Vitest: açılış/kapanış, Esc, mobil overlay davranışı.

### Faz 2: Editörün drawer'a taşınması
- `pane === "document"` dalını kaldır; `/sohbet/:id/belge` artık "sohbet + editör drawer
  açık" demek. `DocumentPanel` RightDrawer içine taşınır, `key={currentId}` ve
  `refreshKey` davranışı korunur.
- Header anahtarı → tek "Editör" toggle düğmesi. `open_document` sinyali ve
  `DocumentLibrary.onOpen` aynı URL'e gitmeye devam eder.
- `doc-canvas`/`MarkdownEditor` dar genişlik uyumu (A4 sayfanın scale/scroll davranışı).
- E2e: sohbetten belge taslağı → drawer otomatik açılır; Belgeler listesinden açma;
  Back tuşu drawer'ı kapatıp sohbete döner.

### Faz 3: Backend kaynak yakalama ve taşıma
- `ToolResult`'a opsiyonel yapısal ek: `Artifacts` (ör. `IReadOnlyList<SourceRef>`), ya da
  `ToolResult.Ok(text, sources)` overload'u. `SearchDecisionsTool` (gerekirse
  `SearchWithinMevzuatTool`) sonuçlarını buraya koyar; model metni değişmez.
- `ToolCallingLoopService` tur boyunca artifact'ları biriktirir ve döndürür;
  `SessionChatService` benzersizleştirip (source+id) `ChatOutcome.Sources`'a yazar.
- `SessionsController.ChatStream`: `done`'dan önce `event: sources` çerçevesi.
- `ConversationMessage.SourcesJson` kolonu + migration (RLS eki değişmez) +
  `GET /api/sessions/:id` yanıtına `sources` alanı.
- Testler: loop artifact toplama, outcome'a taşıma, SSE çerçeve sırası, persist/reload.

### Faz 4: Kararlar drawer'ı UI
- `api.ts`: `sources` SSE olayı + `done.sources` + `SessionDetail.messages[].sources` tipleri
  (snake_case).
- Mesaj balonu üstünde "N arama · M kaynak" chip'i (akış sırasında step'lerden canlı
  sayaç, done ile kesinleşir); balon altında "Kaynaklar (M)" düğmesi.
- `RelatedDecisionsDrawer`: karar kartları (başlık, İçtihat/Mevzuat rozeti, tarih,
  snippet, dış link `url` varsa). Chip veya düğme tıklayınca ilgili mesajın kaynaklarıyla açılır.
- Eski sohbetler yeniden yüklendiğinde persist edilen kaynaklarla aynı deneyim.

### Faz 5: Karar detayı (drawer içi tam metin)
- Backend: `GET /api/legal/documents?source=…&id=…` (DocumentsRead yetkisi,
  `fetch_document` ile aynı client katmanı; Gateway rotası eklenir).
- UI: kartta dışa açma oku `url`'i yeni sekmede açar; kart gövdesine tıklayınca drawer
  içinde detay görünümü (geri okuyla listeye dönüş), tam metin + künye.

### Faz 6: Cila ve kapsam genişletme
- Mevzuat hitleri için drawer'da ayrı bölüm; RAG (`search_office_documents`) alıntılarının
  "Büro belgeleri" olarak eklenmesi.
- A11y turu (focus sırası, ekran okuyucu etiketleri), mobil ince ayar, koyu tema.
- Playwright e2e: arama yapan bir soru → chip → drawer → detay akışı (mock backend ile).

## 5. Risk kaydı (kod üzerinde doğrulanmış)

Her risk için kanıt ilgili dosyadan okunarak doğrulandı; "Mit." kolonu bağlayıcı karardır.

### R1: A4 kanvasın drawer genişliğine sığmaması. Etki: Yüksek, Olasılık: Kesin
`.doc-canvas .ck-editor__editable` sabit `width: 21cm` (~794px) + 2.2cm yatay padding kullanır
(`src/styles.css:91`); dar panelde bugün yatay scroll'a düşer. CSS `transform: scale()` çözüm
DEĞİLDİR: CKEditor balon panelleri (link, tablo, resim araç çubukları) konumu
`getBoundingClientRect` ile hesaplar ve ata elemandaki transform'u hesaba katmaz; seçim
overlay'leri ve sticky toolbar kayar. **Mit.:** Ölçekleme yok. Kesme noktalı mod: viewport
≥ ~1200px iken push drawer (editör `min(900px, 65vw)`, chat en az ~380px kalır); altında
editör drawer'ı overlay'e döner ve kanvası tam genişlik kullanır. 794px'in de altında mevcut
yatay scroll davranışı fallback olarak kalır.

### R2: Kaydedilmemiş değişiklik kaybı. Etki: Yüksek, Olasılık: Orta
`DocumentEditor` yalnız Kaydet düğmesi ve başlık blur'unda yazar; pane değişimi CKEditor'ü
uyarısız unmount eder. Bu bugün de var ama drawer kapatmayı kolaylaştırınca (Esc, dışarı
tıklama, Back) veri kaybı olasılığı büyür. **Mit.:** Faz 2 kapsamına dirty takibi girer:
`MarkdownEditor.onChange` ile son kaydedilenle karşılaştır; kirliyken kapatma isteğinde
onay sor ("Kaydedilmemiş değişiklikler var"), `beforeunload` guard'ı ekle. İkinci adım
olarak debounce'lu otomatik kaydetme (ör. 2 sn) değerlendirilir; onay diyaloğu ilk sürümde
zorunlu, autosave opsiyonel.

### R3: SSE sözleşme evrimi ve dağıtım sırası. Etki: Orta, Olasılık: Düşük
UI'daki `dispatch` bilinmeyen event'leri sessizce yok sayar (`api.ts:569-573`) ve `done`
payload'ına alan eklemek cast'i bozmaz; yani eski UI + yeni backend ve tersi güvenlidir.
**Mit.:** Yalnız additive değişiklik kuralı: yeni event (`sources`, `tool`), yeni alanlar
(`done.sources`); mevcut event'lerin şeması asla değişmez. İki tarafta da sözleşme testi
(backend: SSE çerçeve sırası testi; UI: bilinmeyen event yok sayma testi).

### R4: Canlı chip sayacı için `step` etiketi ayrıştırma. Etki: Orta, Olasılık: Kesin
Araç adımları UI'a yalnız Türkçe kopya olarak gider ("Araç çalıştırılıyor: search_decisions",
`ToolCallingLoopService.cs:54`); metin ayrıştırmak UI kopyasına bağımlılık yaratır.
**Mit.:** Loop'a `onStep/onToken` deseninin aynısı olan opsiyonel `onTool(name)` callback'i
eklenir; controller bunu yapısal `event: tool` `{name}` çerçevesine çevirir. UI canlı sayacı
`tool` olayından sayar (`search_decisions` = arama), `step` yalnız insan-okur etiket kalır.
İlk sürümde chip'in yalnız `done` sonrası kesinleşmesi kabul edilebilir bir ara durumdur.

### R5: Sözleşme değişikliğinin test yüzeyine yayılması. Etki: Orta, Olasılık: Düşük
`IAgentToolLoopService`'i tüketen tek yer `SessionChatService`; testler somut
`ToolCallingLoopService`'i test ediyor, arayüzün fake'i yok (grep ile doğrulandı). `ToolResult`
positional record; yeni üye eklemek mevcut `Ok/Error` fabrikalarını bozmamalı. **Mit.:**
Tüm genişletmeler opsiyonel/varsayılanlı: `ToolResult`'a init-only `Sources` özelliği
(fabrikalar değişmez, `Ok(text) with { Sources = … }`), `RunAsync`'e varsayılanı null yeni
parametre, `ChatReply`/`ChatOutcome`/`UploadOutcome`'a yeni alan. 423 testlik süit her fazda
yeşil kalmak zorunda; kırmızıya düşüren dilim bölünür.

### R6: Migration ve canlı veri. Etki: Orta, Olasılık: Düşük
Migration düzeni tek `InitialCreate` + el yazımı RLS eki; VPS auto-deploy canlı ortam demek.
`SourcesJson` kolonu nullable jsonb, satır-level RLS'i etkilemez. **Mit.:** Varsayılan yol
additive migration (`AddConversationMessageSources`); yalnız hiçbir ortamda veri yoksa
InitialCreate'e katlanır (tek-migration konvansiyonunu korumak için). Karar deploy anında
verilecek, roadmap additive varsayar.

### R7: Kaynak payload'ının şişmesi. Etki: Düşük-Orta, Olasılık: Orta
Bir turda 8 iterasyona kadar araç çağrısı, çağrı başına 10+ sonuç dönebilir; snippet'ler
uzun olabilir. **Mit.:** Persist ve SSE öncesi normalize: `source+id` ile tekilleştir
(model sırası korunur), mesaj başına en çok 40 kaynak, snippet 300 karaktere kırpılır.
Sınırlar `RuntimeConfig`'te sabitlenir, sessiz kırpma yok: aşımda liste sonuna
"+N kaynak daha" işareti konur.

### R8: Dış bağlantılar ve karar detayı endpoint'i. Etki: Düşük, Olasılık: Düşük
Kaynaklar kamu veritabanlarından gelir, PII yok; ama `url` dışa açılır ve yeni
`GET /api/legal/documents` endpoint'i sunucu tarafında dış HTTP çağrısı yapar. **Mit.:**
Linkler `target="_blank" rel="noopener noreferrer"`; snippet'ler markdown değil düz metin
olarak render edilir (React escape eder). Endpoint source'u mevcut enum whitelist'inden
doğrular, `DocumentsRead` yetkisi ister, Gateway'de Agent host rotasına eklenir; istek
başına zaman aşımı araç client'larındakiyle aynı kalır.

### R9: CKEditor mount maliyeti ve animasyon jank'i. Etki: Düşük-Orta, Olasılık: Orta
CKEditor ağır bir bileşen; genişlik animasyonu sürerken mount etmek reflow fırtınası yaratır.
**Mit.:** Editör drawer'ı genişlik animasyonu yapmaz (anında açılır, içerik kısa fade alır)
ya da editör `transitionend` sonrası mount edilir; hangisi diye Faz 2'de ölçülür. Kararlar
drawer'ı hafif olduğu için normal genişlik animasyonu kullanır. `prefers-reduced-motion`
ikisinde de animasyonu kapatır.

### R10: İki drawer'ın çakışması ve URL anlamı. Etki: Düşük, Olasılık: Kesin
Editör URL'den türer (`/sohbet/:id/belge`), kararlar geçici state; sohbet sırasında
`open_document` sinyali editörü otomatik açar. **Mit.:** Tek drawer kuralı state machine ile
zorlanır: `DrawerState = none | editor | sources(messageIdx)`. Kaynak chip'i editörü kapatır
(URL'i `/sohbet/:id`e çeker), `open_document` ve editör toggle'ı kararları kapatır. Back
tuşu editör drawer'ını kapatıp sohbete döner; kararlar drawer'ı history'e girmez.

### R11: Stream'siz yollar (upload, reconcile). Etki: Düşük, Olasılık: Kesin
Upload SSE kullanmaz; ayrıca stream yolunda ara-iterasyon metinleri de `onToken`'dan akar ve
UI final reply ile reconcile eder (mevcut davranış). **Mit.:** `UploadOutcome`'a da `Sources`
eklenir; UI chip/drawer'ı hem `sources` olayından hem HTTP yanıtındaki alandan aynı fonksiyonla
besler. Reconcile mantığına dokunulmaz.

## 6. Uygulama stratejisi

Profesyonel çerçeve: sözleşme önce, additive evrim, tek yönlü veri akışı, her fazda yeşil süit.

1. **Sözleşme önce (contract-first).** `SourceRef` DTO'su `Korkut.Contract`'a girer
   (Korkut.Tools zaten Contract'a referans veriyor, ek bağımlılık gerekmez); SSE olay şeması ve
   `done.sources` alanı dokümante edilir; UI tipleri (`api.ts`, snake_case) aynı PR'da mock
   fixture'larla eklenir. Backend ve UI dilimleri bu sabit sözleşmeye paralel ilerleyebilir.
2. **Veri akışı tek yönlü ve tek kaynaklı.** Araç → `ToolResult.Sources` → loop birikimi →
   `ChatReply.Sources` → `SessionChatService` normalize (tekille, kırp) → persist
   (`SourcesJson`) + SSE `sources` + `ChatOutcome.Sources`. UI hangi yoldan gelirse gelsin
   (stream, HTTP, reload) aynı `MessageSources` bileşenini besler.
3. **UI state machine.** `Workspace` içinde tek `DrawerState`; editör kolu URL'den türetilir
   (mevcut "URL tek gerçek kaynak" konvansiyonu korunur), kararlar kolu geçici state.
   `RightDrawer` sunumsal kalır: başlık, genişlik, mod (push/overlay) prop'tur; iş kuralı
   taşımaz.
4. **Erişilebilirlik baştan.** Push modunda `role="complementary"` + `aria-label`; overlay
   modunda dialog semantiği, focus trap ve Esc (mevcut `src/a11y` yardımcıları kullanılır).
   Mevcut a11y e2e spec'ine drawer senaryoları eklenir.
5. **Test stratejisi faz başına.** Backend: loop artifact birikimi ve normalize birim testleri,
   SSE çerçeve sırası entegrasyon testi, persist/reload testi. UI: vitest ile drawer state
   machine ve chip render'ı; Playwright'ta mock LLM stub'ıyla (tests/load'daki stub deseni)
   uçtan uca "soru → tool olayları → chip → drawer → detay" akışı.
6. **Dağıtım güvenliği.** Yalnız additive API değişikliği olduğundan backend önce, UI sonra
   yayınlanabilir; feature flag gerekmez. Migration additive ve geri alınabilir. Her faz
   bağımsız merge/deploy edilir; Faz 2 (editör) ile Faz 3-4 (kaynaklar) birbirini beklemez,
   yalnız Faz 1'e bağımlıdır.
