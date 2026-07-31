// Korkut multi-tenant load scenario.
//
// Spreads VUs across the seeded tenants (each VU owns one tenant), logs in once, opens a session,
// then repeats a weighted mix of heavy operations: RAG chat (full tool loop through the mock LLM),
// document upload (extraction pipeline), and real Tesseract OCR (direct to the internal OCR host).
// Per-operation latency/throughput/error metrics are recorded and summarised for benchmarking.
//
// Run via tests/load/run.sh (which builds the load stack, seeds tenants, and invokes k6 on the
// stack network). Tunables via env: VUS, DURATION, RAMP, W_CHAT, W_UPLOAD, W_OCR.

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';
import { SharedArray } from 'k6/data';
import exec from 'k6/execution';

const BASE = __ENV.BASE_URL || 'http://gateway:8080';
const OCR = __ENV.OCR_URL || 'http://ocr:8080';
const VUS = parseInt(__ENV.VUS || '200', 10);
const DURATION = __ENV.DURATION || '2m';
const RAMP = __ENV.RAMP || '30s';

// Operation mix (weights; normalised at runtime). OCR is real Tesseract (CPU-bound, single host),
// so it is kept a small slice of the mix — raise W_OCR only when stressing OCR specifically.
const W_CHAT = parseInt(__ENV.W_CHAT || '77', 10);
const W_UPLOAD = parseInt(__ENV.W_UPLOAD || '20', 10);
const W_OCR = parseInt(__ENV.W_OCR || '3', 10);

const tenants = new SharedArray('tenants', () => JSON.parse(open('/load/tenants.json')));
const scanImg = open('/load/fixtures/scan.png', 'b');
const contractTxt = open('/load/fixtures/contract.txt', 'b');

const chatMs = new Trend('rag_chat_ms', true);
const uploadMs = new Trend('upload_ms', true);
const ocrMs = new Trend('ocr_ms', true);
const opErrors = new Rate('op_errors');
const chatOk = new Counter('rag_chat_ok');
const uploadOk = new Counter('upload_ok');
const ocrOk = new Counter('ocr_ok');

const QUERIES = [
  'birine dalyarak demek hakaret suçu mudur, emsalleri çıkar',
  'kira tespit davasında yetkili mahkeme ve süreler neler',
  'iş kazası tazminatında ispat yükü kimde, Yargıtay ne diyor',
  'tapu iptal ve tescil davasında iyiniyetli üçüncü kişi koruması',
  'boşanmada tedbir nafakası hangi tarihten başlar',
];

export const options = {
  scenarios: {
    rag: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: RAMP, target: VUS },
        { duration: DURATION, target: VUS },
        { duration: '15s', target: 0 },
      ],
      gracefulRampDown: '15s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    op_errors: ['rate<0.05'],
    rag_chat_ms: ['p(95)<20000'],
    ocr_ms: ['p(95)<30000'],
  },
};

// Log in each tenant ONCE up front and share its access token across that tenant's VUs (Bearer on
// every request). Logging in per VU would have many VUs authenticate as the same user and rotate
// each other's tokens (→ 401); one token per tenant avoids that and matches how a real client holds
// a session. The handler accepts Authorization: Bearer, so no cookie jar is involved.
export function setup() {
  const authed = [];
  for (const t of tenants) {
    const res = http.post(`${BASE}/api/auth/login`, JSON.stringify({ email: t.email, password: t.password }), {
      headers: { 'Content-Type': 'application/json' },
    });
    const token = res.cookies['korkut_at'] ? res.cookies['korkut_at'][0].value : null;
    if (!token && __ENV.DEBUG_ERR) console.error(`login ${t.email} -> ${res.status}`);
    authed.push({ email: t.email, token });
  }
  return { authed };
}

let session = null;  // per-VU: created once, reused

function bearer(token, json) {
  const h = { Authorization: `Bearer ${token}` };
  if (json) h['Content-Type'] = 'application/json';
  return h;
}

function ensureSession(token) {
  const res = http.post(`${BASE}/api/sessions`, null, { headers: bearer(token), tags: { op: 'session_create' } });
  const ok = check(res, { 'session created': (r) => r.status === 201 || r.status === 200 });
  if (ok) {
    try { session = JSON.parse(res.body).id; } catch (e) { session = null; }
  } else if (__ENV.DEBUG_ERR) {
    console.error(`session ${res.status}: ${String(res.body).slice(0, 120)}`);
  }
}

function ragChat(token) {
  const q = QUERIES[Math.floor(Math.random() * QUERIES.length)];
  const res = http.post(`${BASE}/api/sessions/${session}/chat`, JSON.stringify({ text: q }), {
    headers: bearer(token, true), timeout: '60s', tags: { op: 'rag_chat' },
  });
  chatMs.add(res.timings.duration);
  const ok = check(res, { 'chat 200': (r) => r.status === 200 });
  opErrors.add(!ok);
  if (ok) chatOk.add(1);
  else if (__ENV.DEBUG_ERR) console.error(`chat ${res.status}: ${String(res.body).slice(0, 120)}`);
}

function uploadDoc(token) {
  const res = http.post(`${BASE}/api/sessions/${session}/upload`, {
    file: http.file(contractTxt, 'sozlesme.txt', 'text/plain'),
    text: 'Bu belgeyi özetle ve riskleri çıkar.',
  }, { headers: bearer(token), timeout: '60s', tags: { op: 'upload' } });
  uploadMs.add(res.timings.duration);
  const ok = check(res, { 'upload 200': (r) => r.status === 200 });
  opErrors.add(!ok);
  if (ok) uploadOk.add(1);
  else if (__ENV.DEBUG_ERR) console.error(`upload ${res.status}: ${String(res.body).slice(0, 120)}`);
}

function ocrExtract(token) {
  const res = http.post(`${OCR}/extract`, {
    file: http.file(scanImg, 'scan.png', 'image/png'),
  }, { headers: bearer(token), timeout: '60s', tags: { op: 'ocr' } });
  ocrMs.add(res.timings.duration);
  const ok = check(res, { 'ocr 200': (r) => r.status === 200 });
  opErrors.add(!ok);
  if (ok) ocrOk.add(1);
  else if (__ENV.DEBUG_ERR) console.error(`ocr ${res.status}: ${String(res.body).slice(0, 120)}`);
}

export default function (data) {
  const t = data.authed[(exec.vu.idInTest - 1) % data.authed.length];
  if (!t || !t.token) { sleep(1); return; }
  if (!session) ensureSession(t.token);
  if (!session) { sleep(1); return; }

  const roll = Math.random() * (W_CHAT + W_UPLOAD + W_OCR);
  if (roll < W_CHAT) ragChat(t.token);
  else if (roll < W_CHAT + W_UPLOAD) uploadDoc(t.token);
  else ocrExtract(t.token);

  sleep(Math.random() * 1.5);
}
