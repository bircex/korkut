# Competitors — Turkey Legal Artificial Intelligence Products

> Status: **Preliminary review (ön-inceleme)** (quick scan). User request: record these as competitors;
> **detailed research to be done LATER** (see §3). Goal: to go **far beyond** the
> quality of these products.
>
> ⚠️ **Korkut is NON-COMMERCIAL (ticari değil)** — an internal tool for our own office. For this reason **price/market share
> are secondary**; our main axis of comparison is **quality (kalite)**, especially **research (legal research)** and
> **petition writing**. The prices below are for reference only.
>
> Note: Our interface is not a **web chat** like these — it is **Mattermost-native** (mobile,
> team channels + DM), single identity Korkut, **data in the office** (self-hosted).

## 1. Competitor list (preliminary review)

| # | Product | Developer | What it does | Price (visible) |
|---|------|-------------|-----------|-----------------|
| 1 | [LawChat](https://www.lawchat.com.tr/) | LawChat (TR) | Petition generation, precedent analysis, UYAP integration, mobile app | undisclosed ("try for free") |
| 2 | [De Jure AI](https://www.dejure.ai/) | De Jure | Deep research, petition/contract, UYAP/UETS, court tracking | **disclosed: 1.500–6.800₺/month**, annual 60–80K₺ |
| 3 | [LexChat](https://lexchat.ai/) | UC Yazılım (Istanbul, 2025) | **Agentic**: autonomous agents, case file management, 10M+ decisions, document generation (tracked changes, UDF) | undisclosed |
| 4 | [FulLegal](https://www.fullegal.com/hukukcu-yapay-zeka) | FulLegal Yazılım | Case law (içtihat)/legislation (mevzuat) Q&A, document analysis (20 documents), UYAP desktop agent, petition | undisclosed (Pro/Ultra/Max) |
| 5 | [HukukChat](https://hukukchat.com/) | **Opex AI Labs** | Fine-tuned legal LLMs, multi-layered architecture, legislation/case law/document analysis | undisclosed |
| 6 | [Apilex](https://www.apilex.ai/) | Att. Cebrail Ergül + team | Semantic decision search, projects, **Agent Beta** (deep research); TR+FR+DE | undisclosed (after login) |
| 7 | [LawyerTeam](https://lawyerteam.pro/) | Since 1999 + Dece Software | Full **office automation** + AI: 15M decisions, 20K legislation, 200+ file formats, enforcement/strategy | undisclosed |
| 8 | [hukuk.chat (VeriUs)](https://verius.com.tr/tr/urunlerimiz/hukuk.chat) | **VeriUs** | Turkish law AI assistant (app: ai.hukuk.chat); Q&A, documents | undisclosed |
| 9 | [Lexpera LEXI](https://www.lexpera.com.tr/ayrintilar/lexi) | Lexpera | AI assistant on top of LEXPERA content (1.6M+ decisions); **beta** | undisclosed (beta) |
| 10 | [Mevzuat AI](https://mevzuat.ai/tr/) | (unclear) | **AI Agent** architecture: legislation/case law from official DBs, document analysis, petition | undisclosed |
| 11 | [Ayboğa AI](https://ayboga.av.tr/ayboga-ai/) | Ayboğa Avukatlık (Ankara) | 3.5M+ precedents, legislation search, petition/contract generation + analysis | undisclosed |
| 12 | [hukukchat.com](https://hukukchat.com/) → see #5 | — | (Opex; same as #5) | — |

> Additional players seen during the preliminary review (may be included in research): **YasalZeka**,
> **Kılavuz** (kilavuztek.com), **Lawantra**, **LegalEngine**, **Sinerji Hukuk Asistanı**.

## 2. Common patterns (industry "table stakes")
Present in almost all of them: **semantic precedent decision (emsal karar) search**, **petition/contract generation**,
**legislation/case law Q&A (with source citations)**, **document/UYAP analysis**, document management.
Many claim to be "**agentic**" (LexChat, mevzuat.ai, Apilex). Data-scale advertising:
3.5M–15M decisions. **Almost all of them are web-app chat UI + cloud SaaS, single-user focused.**

### Our differentiation points (to be far ahead)
- **Data sovereignty (veri egemenliği):** Self-hosted, data stays in the office → a strong KVKK/privacy (gizlilik)
  advantage against competitors' cloud SaaS.
- **Team workflow:** Mattermost (channels + DM, multi-user) → beyond single web chats.
- **Proactivity:** Not just Q&A; a foresightful assistant with **hearing reminders (duruşma hatırlatma) + calendar + cron**.
- **Built-in human approval:** No persistent action without approval (natural-language approval dialog).
- **Open data layer:** our own thin clients over the public APIs (Bedesten API + portals) — same sources as competitors.
- **Provider-independent + own framework:** GPT↔Claude↔local; no lock-in.

## 2b. "Millions of documents" claims — where does the data come from? (critical)
**Almost all** of the competitors' claims of "3.5M / 10M / 15M decisions, 20K legislation" come **from publicly
available government sources** — that is, the endpoints in KAYNAKLAR.md that **we also access**:
- **UYAP Emsal + Bedesten API** (Yargıtay/Danıştay/court of appeals), **karararama.yargitay/danistay**,
  **mevzuat.gov.tr** → anonymous, millions of records.
- The competitors have **fetched this once (ingest) and indexed it into their own DBs** (embedding for
  semantic search). The difference in numbers = how much they fetched; **the data is not proprietary**.
- **The only exception is Lexpera/Kazancı:** editorial + licensed content (old/non-digitized decisions,
  books/articles) → cannot be collected from public sources; this is the real "moat".

**What this means for us:** No need to "find" the data — it is publicly available and we have it too. Catching up to the
competitor level = **fetching the public corpus into a local DB and building a semantic index** (with local
embedding, without data leaving the office). This is the real lever for research + petition quality.
Decision (research/petition module): **live API, local index, or hybrid** → decided after §3.

## 3. Detailed research — PENDING (user request: "later")
To be examined in depth for each competitor (deep-research):
- Full feature matrix + comparison with ours
- Pricing tiers (registration/quote for undisclosed ones)
- Which LLM/infrastructure, data sources, accuracy of scale claims
- Privacy/KVKK stance (cloud or not, where the data is), UYAP integration depth
- Strengths/weaknesses, user reviews, market position
- Concrete opportunity areas for us to be "far ahead"
> When ready, it can be run with `deep-research` one by one or in bulk.
