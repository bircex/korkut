const $ = (id) => document.getElementById(id);

const loginView = $("login-view");
const chatView = $("chat-view");
const messages = $("messages");

async function api(path, opts = {}) {
  const res = await fetch(path, {
    credentials: "same-origin",
    headers: { "Content-Type": "application/json" },
    ...opts,
  });
  return res;
}

function show(view) {
  loginView.classList.toggle("hidden", view !== "login");
  chatView.classList.toggle("hidden", view !== "chat");
}

function addMessage(role, content, pending = false) {
  const wrap = document.createElement("div");
  wrap.className = `msg ${role}`;
  const bubble = document.createElement("div");
  bubble.className = "bubble" + (pending ? " pending" : "");
  bubble.textContent = content;
  wrap.appendChild(bubble);
  messages.appendChild(wrap);
  messages.scrollTop = messages.scrollHeight;
  return bubble;
}

async function loadHistory() {
  messages.innerHTML = "";
  const res = await api("/api/history");
  if (!res.ok) return;
  const rows = await res.json();
  if (rows.length === 0) {
    addMessage("assistant", "Merhaba, ben Korkut. Cankar Hukuk asistanınız. Size nasıl yardımcı olabilirim?");
    return;
  }
  for (const m of rows) addMessage(m.role, m.content);
}

async function checkSession() {
  const res = await api("/api/me");
  if (res.ok) {
    show("chat");
    await loadHistory();
  } else {
    show("login");
  }
}

$("login-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const err = $("login-error");
  err.classList.add("hidden");
  $("login-btn").disabled = true;
  const res = await api("/api/login", {
    method: "POST",
    body: JSON.stringify({ username: $("username").value, password: $("password").value }),
  });
  $("login-btn").disabled = false;
  if (res.ok) {
    $("password").value = "";
    show("chat");
    await loadHistory();
  } else {
    const data = await res.json().catch(() => ({}));
    err.textContent = data.detail || "Giriş başarısız";
    err.classList.remove("hidden");
  }
});

$("logout-btn").addEventListener("click", async () => {
  await api("/api/logout", { method: "POST" });
  show("login");
  $("username").focus();
});

const input = $("chat-input");
input.addEventListener("input", () => {
  input.style.height = "auto";
  input.style.height = Math.min(input.scrollHeight, 160) + "px";
});
input.addEventListener("keydown", (e) => {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    $("chat-form").requestSubmit();
  }
});

$("chat-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const text = input.value.trim();
  if (!text) return;
  input.value = "";
  input.style.height = "auto";
  $("send-btn").disabled = true;
  addMessage("user", text);
  const pending = addMessage("assistant", "Korkut yazıyor…", true);
  try {
    const res = await api("/api/chat", { method: "POST", body: JSON.stringify({ text }) });
    if (res.ok) {
      const data = await res.json();
      pending.textContent = data.reply;
      pending.classList.remove("pending");
    } else if (res.status === 401) {
      show("login");
    } else {
      const data = await res.json().catch(() => ({}));
      pending.textContent = "Hata: " + (data.detail || "yanıt alınamadı");
    }
  } catch {
    pending.textContent = "Bağlantı hatası. Tekrar deneyin.";
  } finally {
    $("send-btn").disabled = false;
    input.focus();
  }
});

checkSession();
