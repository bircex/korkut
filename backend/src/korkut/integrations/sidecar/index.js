// Korkut WhatsApp sidecar — owns one WhatsApp Web session (Baileys) and exposes a tiny localhost
// HTTP API that the Python `whatsapp` connector polls. This is the Node half clawbot-style WhatsApp
// requires: WhatsApp has no first-class bot API, so we drive WhatsApp Web and act as the paired
// account.
//
//   GET  /inbox?after=<id>   -> { messages: [{id, from, chat, text, name}], cursor: <lastId> }
//   POST /send  {to, text}   -> { ok: true }
//   GET  /status             -> { connected, hasQR }
//
// Auth: every request must carry `Authorization: Bearer <TOKEN>` (the shared secret also stored on
// the office's whatsapp channel config). Bind to localhost only — this process can send WhatsApp
// messages as you.
//
// Env:
//   KORKUT_WA_PORT      default 8099
//   KORKUT_WA_TOKEN     shared secret (required; the connector sends the same value)
//   KORKUT_WA_AUTH_DIR  where the WhatsApp session is persisted (default ./auth)
//   KORKUT_WA_BUFFER    max buffered inbound messages (default 1000)
//
// First run prints a QR code in the terminal — scan it from the phone:
//   WhatsApp → Settings → Linked Devices → Link a Device.

import { createServer } from "node:http";
import { Buffer } from "node:buffer";
import makeWASocket, {
  useMultiFileAuthState,
  DisconnectReason,
  fetchLatestBaileysVersion,
} from "@whiskeysockets/baileys";
import qrcode from "qrcode-terminal";

const PORT = parseInt(process.env.KORKUT_WA_PORT || "8099", 10);
const TOKEN = process.env.KORKUT_WA_TOKEN || "";
const AUTH_DIR = process.env.KORKUT_WA_AUTH_DIR || "./auth";
const BUFFER_MAX = parseInt(process.env.KORKUT_WA_BUFFER || "1000", 10);

if (!TOKEN) {
  console.error("KORKUT_WA_TOKEN is required (shared secret with the Korkut connector).");
  process.exit(1);
}

// In-memory inbound buffer. Each message gets a monotonically increasing id; the connector acks by
// asking for messages "after" the last id it processed, so a brief connector outage replays.
let seq = 0;
const inbox = [];
let connected = false;
let lastQR = null;
let sock = null;

function pushInbound(msg) {
  inbox.push({ id: ++seq, ...msg });
  while (inbox.length > BUFFER_MAX) inbox.shift();
}

function textOf(message) {
  if (!message) return "";
  return (
    message.conversation ||
    message.extendedTextMessage?.text ||
    message.imageMessage?.caption ||
    message.videoMessage?.caption ||
    ""
  );
}

async function start() {
  const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);
  const { version } = await fetchLatestBaileysVersion();
  sock = makeWASocket({ version, auth: state, printQRInTerminal: false });

  sock.ev.on("creds.update", saveCreds);

  sock.ev.on("connection.update", (u) => {
    const { connection, lastDisconnect, qr } = u;
    if (qr) {
      lastQR = qr;
      console.log("\nScan this QR in WhatsApp → Linked Devices → Link a Device:\n");
      qrcode.generate(qr, { small: true });
    }
    if (connection === "open") {
      connected = true;
      lastQR = null;
      console.log("WhatsApp connected.");
    } else if (connection === "close") {
      connected = false;
      const code = lastDisconnect?.error?.output?.statusCode;
      if (code !== DisconnectReason.loggedOut) {
        console.log("Connection closed, reconnecting…");
        start();
      } else {
        console.log("Logged out — delete the auth dir and re-pair.");
      }
    }
  });

  sock.ev.on("messages.upsert", ({ messages, type }) => {
    if (type !== "notify") return;
    for (const m of messages) {
      if (m.key.fromMe) continue; // ignore our own outgoing messages
      const text = textOf(m.message);
      if (!text) continue;
      const chat = m.key.remoteJid || "";
      // In a group, the real sender is `participant`; in a DM it's the chat itself.
      const from = m.key.participant || chat;
      pushInbound({ from, chat, text, name: m.pushName || "" });
    }
  });
}

// ---- tiny HTTP API ----

function authed(req) {
  const h = req.headers["authorization"] || "";
  return h === `Bearer ${TOKEN}`;
}

function sendJSON(res, code, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(code, { "Content-Type": "application/json" });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve) => {
    const chunks = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => {
      try {
        resolve(JSON.parse(Buffer.concat(chunks).toString() || "{}"));
      } catch {
        resolve({});
      }
    });
  });
}

const server = createServer(async (req, res) => {
  if (!authed(req)) return sendJSON(res, 401, { ok: false, error: "unauthorized" });
  const url = new URL(req.url, "http://127.0.0.1");

  if (req.method === "GET" && url.pathname === "/inbox") {
    const after = parseInt(url.searchParams.get("after") || "0", 10) || 0;
    const messages = inbox.filter((m) => m.id > after);
    const cursor = messages.length ? messages[messages.length - 1].id : after;
    return sendJSON(res, 200, { messages, cursor });
  }

  if (req.method === "POST" && url.pathname === "/send") {
    const { to, text } = await readBody(req);
    if (!to || !text) return sendJSON(res, 400, { ok: false, error: "to and text required" });
    if (!connected || !sock) return sendJSON(res, 503, { ok: false, error: "not connected" });
    try {
      await sock.sendMessage(String(to), { text: String(text) });
      return sendJSON(res, 200, { ok: true });
    } catch (e) {
      return sendJSON(res, 500, { ok: false, error: String(e) });
    }
  }

  if (req.method === "GET" && url.pathname === "/status") {
    return sendJSON(res, 200, { connected, hasQR: Boolean(lastQR) });
  }

  return sendJSON(res, 404, { ok: false, error: "not found" });
});

server.listen(PORT, "127.0.0.1", () => {
  console.log(`Korkut WhatsApp sidecar on http://127.0.0.1:${PORT}`);
});

start().catch((e) => {
  console.error("sidecar failed to start:", e);
  process.exit(1);
});
