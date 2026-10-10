"""Mock OpenAI-compatible chat endpoint for load testing.

Stands in for DeepSeek/OpenAI so a load run measures OUR infrastructure (connection pooling,
per-office concurrency, tenant isolation, the tool loop, upload/OCR) instead of a third-party
rate limiter. Speaks just enough of the OpenAI chat-completions schema for
OpenAiCompatibleChatProviderService: POST /chat/completions, streaming (SSE) and non-streaming,
with tool-calls.

Behaviour: on a turn that offers tools and has NO prior tool result, it returns ONE tool call
(exercises the tool loop + a real DB-backed tool run); once a tool result is present it returns a
short final answer. A small configurable latency (MOCK_LATENCY_MS) simulates model think-time so
concurrency actually overlaps. No third-party dependencies (Python stdlib only).
"""

import hashlib
import json
import os
import random
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

LATENCY_MS = int(os.environ.get("MOCK_LATENCY_MS", "300"))
PROMPT_TOKENS = int(os.environ.get("MOCK_PROMPT_TOKENS", "1200"))
COMPLETION_TOKENS = int(os.environ.get("MOCK_COMPLETION_TOKENS", "180"))
MODEL = os.environ.get("MOCK_MODEL", "deepseek-chat")

FINAL_TEXT = (
    "Kısa değerlendirme: sorduğunuz konuda ilgili Yargıtay içtihadı ve mevzuat taranarak "
    "aşağıdaki sonuca varıldı. Bu bir TASLAK değerlendirmedir; nihai karar avukatındır.\n\n"
    "- İlgili daire kararları emsal alınmıştır.\n- Somut olayın koşullarına göre değişebilir."
)

# The tool the mock asks for each turn: LOCAL, DB-backed, tenant-scoped tools only (never
# search_decisions, which would hit the real external Bedesten API — rate-limited ~10/30s and
# unsuitable for a throughput benchmark). Rotates DB WRITES (create_client/create_matter → INSERT +
# RLS) and reads (list_*), so a run stresses the write path + per-tool tenant scope + connection pool.
# Weighted toward writes. Each call carries valid args and a unique name so rows don't collide.
def make_tool_call():
    n = random.randint(1, 10 ** 9)
    name, args = random.choices(
        [
            ("create_client", {"full_name": f"Yük Müvekkil {n}", "kind": "individual"}),
            ("create_matter", {"title": f"Yük Dosya {n}"}),
            ("list_clients", {}),
            ("list_matters", {}),
        ],
        weights=[35, 35, 15, 15],
    )[0]
    return {"id": f"call_{n}", "type": "function", "function": {"name": name, "arguments": json.dumps(args)}}

# Fast fake RAG grounding hits (Knowledge /search stand-in).
FAKE_HITS = [
    {"chunkId": "00000000-0000-0000-0000-000000000001",
     "documentId": "00000000-0000-0000-0000-0000000000a1", "ordinal": 0,
     "text": "TCK m.125 uyarınca hakaret, bir kimseye onur, şeref ve saygınlığını rencide edecek "
             "biçimde isnat veya sövme ile işlenir.", "score": 0.91},
    {"chunkId": "00000000-0000-0000-0000-000000000002",
     "documentId": "00000000-0000-0000-0000-0000000000a1", "ordinal": 1,
     "text": "Yargıtay uygulamasında ağır eleştiri ile hakaret ayrımı, ifadenin muhatabı küçük "
             "düşürücü nitelik taşıyıp taşımadığına göre yapılır.", "score": 0.87},
    {"chunkId": "00000000-0000-0000-0000-000000000003",
     "documentId": "00000000-0000-0000-0000-0000000000a2", "ordinal": 0,
     "text": "Somut olayda kullanılan argo/küfür nitelikli sözler, mağdurun toplumsal saygınlığını "
             "zedelediğinden hakaret suçunu oluşturur.", "score": 0.83},
]


def wants_tool_call(body):
    """First model turn with tools and no tool result yet → make a tool call, else answer."""
    tools = body.get("tools") or []
    if not tools:
        return False
    for m in body.get("messages", []):
        if m.get("role") == "tool":
            return False  # a tool already ran this turn → produce the final answer
    return True


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):
        pass  # keep the load run quiet

    def do_GET(self):
        # A model list, so a gateway (llmr) or a settings screen can discover what is served.
        if self.path.rstrip("/").endswith("/models"):
            self._json(200, {"object": "list", "data": [
                {"id": "mock-chat", "object": "model", "owned_by": "mock"},
                {"id": "mock-embed", "object": "model", "owned_by": "mock"},
            ]})
            return
        self._json(200, {"status": "ok", "service": "mock-llm"})

    def do_POST(self):
        raw = self._read_body()
        path = self.path.rstrip("/")

        # Mock the Knowledge /search (RAG grounding) so a chat turn gets fast, deterministic hits
        # instead of retrying against a knowledge host that can't embed in the load stack.
        if path.endswith("/search"):
            self._json(200, {"hits": FAKE_HITS})
            return

        if path.endswith("/embeddings"):
            try:
                inputs = json.loads(raw or b"{}").get("input", [])
            except json.JSONDecodeError:
                inputs = []
            inputs = [inputs] if isinstance(inputs, str) else inputs
            # Deterministic: a text's vector depends only on the text.
            data = [{"object": "embedding", "index": i,
                     "embedding": [b / 255.0 for b in hashlib.sha256(str(t).encode()).digest()[:8]]}
                    for i, t in enumerate(inputs)]
            tokens = sum(len(str(t).split()) for t in inputs)
            self._json(200, {"object": "list", "model": "mock-embed", "data": data,
                             "usage": {"prompt_tokens": tokens, "total_tokens": tokens}})
            return

        if not path.endswith("/chat/completions"):
            self._json(404, {"error": "not found"})
            return

        try:
            body = json.loads(raw or b"{}")
        except json.JSONDecodeError:
            body = {}

        if LATENCY_MS > 0:
            time.sleep(LATENCY_MS / 1000.0)

        tc = make_tool_call() if wants_tool_call(body) else None
        if os.environ.get("MOCK_DEBUG"):
            roles = [m.get("role") for m in body.get("messages", [])]
            label = tc["function"]["name"] if tc else "FINAL_TEXT"
            print(f"[mock] stream={bool(body.get('stream'))} tools={bool(body.get('tools'))} roles={roles} -> {label}", file=sys.stderr, flush=True)
        if body.get("stream"):
            self._stream(tc)
        else:
            self._complete(tc)

    def _complete(self, tc):
        message = {"role": "assistant", "content": None, "tool_calls": [tc]} if tc \
            else {"role": "assistant", "content": FINAL_TEXT}
        self._json(200, {
            "id": "cmpl_load",
            "object": "chat.completion",
            "model": MODEL,
            "choices": [{"index": 0, "message": message, "finish_reason": "tool_calls" if tc else "stop"}],
            "usage": {"prompt_tokens": PROMPT_TOKENS, "completion_tokens": COMPLETION_TOKENS},
        })

    def _stream(self, tc):
        # No Content-Length on an SSE body, so close the connection at the end to frame it (the
        # .NET HttpClient then reads the stream until EOF). Simpler than manual chunked encoding.
        self.close_connection = True
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream")
        self.send_header("Cache-Control", "no-cache")
        self.send_header("Connection", "close")
        self.end_headers()

        def frame(obj):
            self.wfile.write(f"data: {json.dumps(obj)}\n\n".encode("utf-8"))
            self.wfile.flush()

        if tc:
            frame({"choices": [{"index": 0, "delta": {"tool_calls": [
                {"index": 0, "id": tc["id"], "type": "function", "function": tc["function"]}
            ]}}]})
        else:
            for chunk in _chunks(FINAL_TEXT):
                frame({"choices": [{"index": 0, "delta": {"content": chunk}}]})

        frame({"choices": [], "usage": {"prompt_tokens": PROMPT_TOKENS, "completion_tokens": COMPLETION_TOKENS}})
        self.wfile.write(b"data: [DONE]\n\n")
        self.wfile.flush()

    def _read_body(self):
        # .NET's JsonContent sends the request WITHOUT Content-Length (Transfer-Encoding: chunked),
        # so handle both framings — Content-Length reads short, chunked needs de-chunking.
        if "chunked" in self.headers.get("Transfer-Encoding", "").lower():
            parts = []
            while True:
                size_line = self.rfile.readline().strip()
                if not size_line:
                    continue
                size = int(size_line, 16)
                if size == 0:
                    self.rfile.readline()  # consume the trailing CRLF
                    break
                parts.append(self.rfile.read(size))
                self.rfile.readline()  # CRLF after each chunk
            return b"".join(parts)
        length = int(self.headers.get("Content-Length", "0"))
        return self.rfile.read(length) if length else b"{}"

    def _json(self, code, obj):
        payload = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)


def _chunks(text, size=48):
    for i in range(0, len(text), size):
        yield text[i:i + size]


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "8080"))
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()
