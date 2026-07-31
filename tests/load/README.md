# Korkut yük testi (load / benchmark)

Çok-tenant eşzamanlılık altyapısını (bağlantı havuzu, per-office concurrency, tenant izolasyonu,
tool loop, doküman/OCR ağır işleri) **k6** ile stres altında ölçer ve benchmark çıktısı kaydeder.

Senaryo: **N tenant** (varsayılan 10), her VU bir tenant'a login olur, session açar ve ağırlıklı
bir iş karışımı koşar:

| İş | Ağırlık | Ne yapar |
|---|---|---|
| RAG chat | %70 | Mock LLM üzerinden **tam tool loop** (araç çağrısı + gerçek DB'li tool + cevap) |
| Doküman upload | %20 | `/api/sessions/{id}/upload` → metin çıkarma pipeline'ı |
| OCR | %10 | OCR host'a doğrudan **gerçek Tesseract** ile taranmış görsel |

## Neden mock LLM?

200 chat × 8'e kadar iterasyon = on binlerce model çağrısı. Gerçek DeepSeek'e vurmak (a) rate-limit
(429), (b) ciddi maliyet, (c) benchmark'ın DeepSeek'i ölçmesi demek. Bunun yerine
[`mock-llm/`](mock-llm/app.py) anında yanıt veren, tool-call üreten OpenAI-uyumlu bir stub; agent'ın
`DEEPSEEK_BASE_URL`'i buna yönlendirilir. Böylece **bizim** sistemi ölçeriz. `MOCK_LATENCY_MS`
(varsayılan 300ms) model düşünme süresini taklit eder, eşzamanlılık gerçekten örtüşür.

## Gerçek OCR

OCR host normalde scaffold (`DisabledOcrService` → 503). Load stack'te `KORKUT_OCR_ENGINE=tesseract`
ile [`TesseractOcrService`](../../src/modules/Korkut.Documents/Services/TesseractOcrService.cs)
devreye girer; imaj `tesseract` + `tesseract-ocr-tur/eng` + `poppler-utils` içerir
(bkz. [`docker/Dockerfile.ocr`](../../docker/Dockerfile.ocr)). k6 compose ağında çalıştığı için
internal `ocr:8080/extract`'a doğrudan (access token'ı `Authorization: Bearer` ile) basar.

## Çalıştırma

Önce kök stack ayakta olmalı (`docker compose up -d`). Sonra:

```bash
# Kablolamayı doğrulayan küçük koşu (2 tenant, 5 VU, 20s)
tests/load/run.sh --smoke

# Tam koşu (varsayılan 10 tenant, 200 VU, 2dk)
tests/load/run.sh

# Parametreli
N_TENANTS=10 VUS=200 DURATION=3m RAMP=30s tests/load/run.sh
```

`run.sh` sırayla: load stack'i kurar (mock-llm + agent→mock + gerçek OCR) → fixtures üretir →
tenant'ları seed'ler → k6'yı ağ içinde koşar → özeti `tests/load/results/summary-<ts>.json`'a yazar.

## Ölçülenler

- Operasyon bazında latency (`rag_chat_ms`, `upload_ms`, `ocr_ms` — p50/p90/p95/p99).
- Throughput (`rag_chat_ok`, `upload_ok`, `ocr_ok` sayaçları), hata oranı (`op_errors`, `http_req_failed`).
- Threshold'lar (bkz. `k6/rag-load.js` `options.thresholds`) — aşılırsa k6 non-zero döner (CI'da gate).

Grafana LGTM stack'te olduğu için k6'yı Prometheus remote-write'a da akıtabilirsiniz
(`K6_PROMETHEUS_RW_SERVER_URL` + `-o experimental-prometheus-rw`); şu an JSON özet yeterli.

## İlk koşu bulguları (10 tenant × 200 VU, 30s)

| İş | Sonuç |
|---|---|
| RAG chat | **1383 başarılı**, ~18/s, p95 **1.55s**, hata ~%0 — bağlantı-havuzu/tenant-eşzamanlılık işi 200 VU'da tutuyor |
| Upload | **429 başarılı**, p95 1.54s, hata ~%0 |
| OCR | **darboğaz**: 46 denemeden 10 başarılı, gerisi 60s timeout |

**Ana bulgu:** Gerçek Tesseract **CPU-bound**; tek OCR container yük altında **%1400 CPU**'ya (14 çekirdek) çıkıp ~10-20 eşzamanlı istekte doyuyor, fazlası timeout. Chat/upload hattı (optimize ettiğimiz kısım) ise 200 VU'da rahat ölçekleniyor. **Sonuç:** ağır OCR için OCR host **yatay ölçek (replica) veya kuyruk** ister; `W_OCR` ağırlığını düşürerek de gerçekçi orana çekebilirsiniz.

## Teardown / normal stack'e dönüş

Load overlay'i kaldırıp normal stack'e dönmek için (agent tekrar DeepSeek'e, OCR tekrar scaffold'a):

```bash
docker compose up -d agent ocr        # overlay olmadan → normal env
# mock-llm'i durdurmak için:
docker compose -f docker-compose.yml -f tests/load/docker-compose.load.yml stop mock-llm
```

## Bilinçli kararlar / sınırlar

- **Framework k6** (k8s değil): k8s orkestrasyon; k6 load-testing için. Docker ile koşar.
- **Per-office concurrency**: `KORKUT_OFFICE_CONCURRENCY` (varsayılan 4) tenant başına eşzamanlı
  model çağrısını sınırlar. 10 tenant × 20 VU'da chat'ler governor'da kuyruğa girer — bu gerçekçi;
  daha yüksek ham throughput için tenant sayısını artırın ya da cap'i yükseltin.
- **Yatay ölçek**: tek replica'da concurrency cap per-replica. Çok replica'da dağıtık cap gerekir
  (deferred).
- **fixtures/** ve **tenants.json** ve **results/** git'e girmez (bkz. `.gitignore`), üretilir.
