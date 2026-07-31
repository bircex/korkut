# Load baseline (kaydedilmiş benchmark)

Ham per-run özetleri `results/` altında (git'e girmez). Bu dosya, referans koşuların insan-okur
kaydı — regresyon karşılaştırması için baz alın.

Ortam: tek makine (Docker Desktop, ~14 çekirdek), mock LLM (`MOCK_LATENCY_MS=300`), gerçek Tesseract
OCR, `KORKUT_OFFICE_CONCURRENCY=4`, pgbouncer `DEFAULT_POOL_SIZE=50`.

## Koşu A — dengeli karışım (referans baseline)

`N_TENANTS=10 VUS=200 DURATION=30s RAMP=15s` · ağırlık chat 77 / upload 20 / OCR 3 · 2026-07-31

| Metrik | Değer |
|---|---|
| Toplam iterasyon | 3621 (**0 interrupted**) |
| Hata oranı | **%0.00** (op_errors 0/3621, http_req_failed 0/3831) |
| Throughput | ~61 req/s, ~58 iterasyon/s |
| RAG chat | 2826 ok · **45/s** · p95 **2.54s** · med 2.09s |
| Upload | 688 ok · 11/s · p95 2.54s |
| OCR (gerçek Tesseract) | 107 ok · p95 **341ms** · med 266ms |

Sonuç: chat/upload hattı 200 VU'da **%0 hata** ile ölçekleniyor. p95 ~2.5s'in sürücüsü per-office
concurrency cap (4) — ofis başına 20 VU, 4 eşzamanlı model çağrısı → kuyruk (beklenen, sağlıklı).

## Koşu B — OCR stresi (darboğaz teşhisi)

`N_TENANTS=10 VUS=200 DURATION=30s` · ağırlık chat 70 / upload 20 / **OCR 10**

| İş | Sonuç |
|---|---|
| chat | 1383 ok, p95 1.55s, ~%0 hata |
| upload | 429 ok, ~%0 hata |
| **OCR** | 46 denemeden 10 ok, gerisi **60s timeout** |

Sonuç: gerçek Tesseract **CPU-bound**; tek OCR container yük altında **%1400 CPU** (14 çekirdek) →
~10-20 eşzamanlıda doyar, fazlası timeout. **Öneri:** ağır OCR için OCR host **yatay ölçek (replica)
veya kuyruk**; aksi halde `W_OCR`'ı düşük tut (koşu A).
