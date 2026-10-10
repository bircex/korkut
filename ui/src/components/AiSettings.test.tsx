import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { AiSettings, CHAT_ROUTE, EMBED_ROUTE } from "./AiSettings";
import { aiApi, ApiError, type AiProviderType } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return {
    ...mod,
    aiApi: {
      providerTypes: vi.fn(),
      providers: vi.fn(),
      addProvider: vi.fn(),
      removeProvider: vi.fn(),
      testProvider: vi.fn(),
      models: vi.fn(),
      enableModel: vi.fn(),
      routes: vi.fn(),
      setRoute: vi.fn(),
      usage: vi.fn(),
    },
  };
});

const TYPES: AiProviderType[] = [
  { id: "anthropic", name: "Anthropic", credential: "required", default_base_url: "https://api.anthropic.com", default_reach: "first-party-api", reach_required: false, kinds: ["chat"] },
  { id: "openai-compatible", name: "OpenAI-compatible", credential: "optional", default_base_url: null, default_reach: null, reach_required: true, kinds: ["chat", "embedding"] },
];

function gateway({ providers = [] as { id: string; type: string }[] } = {}) {
  vi.mocked(aiApi.providerTypes).mockResolvedValue(TYPES);
  vi.mocked(aiApi.providers).mockResolvedValue(
    providers.map((p) => ({ ...p, base_url: null, reach: null, enabled: true, credential: "…a1b2" })),
  );
  vi.mocked(aiApi.routes).mockResolvedValue([]);
  vi.mocked(aiApi.usage).mockResolvedValue({ total: { model: null, requests: 0, tokens: { total: 0 }, cost: [] }, data: [], cost_complete: true });
  vi.mocked(aiApi.models).mockImplementation(async (id) => ({
    listing_error: null,
    data:
      id === "anthropic"
        ? [{ id: "claude-sonnet-5", route: "anthropic/claude-sonnet-5", kind: "chat", enabled: false, listed: true, capabilities_source: "shipped" }]
        : [
            { id: "qwen3:8b", route: "ollama/qwen3:8b", kind: "chat", enabled: false, listed: true, capabilities_source: null },
            { id: "nomic-embed-text", route: "ollama/nomic-embed-text", kind: "chat", enabled: false, listed: true, capabilities_source: null },
          ],
  }));
}

describe("AiSettings", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("starts with the add-provider form when there is none, and adds one", async () => {
    gateway();
    vi.mocked(aiApi.addProvider).mockResolvedValueOnce({ id: "anthropic", type: "anthropic", base_url: null, reach: null, enabled: true, credential: "…test" });
    render(<AiSettings />);

    fireEvent.change(await screen.findByRole("combobox", { name: /^Sağlayıcı/ }), { target: { value: "anthropic" } });
    fireEvent.change(screen.getByLabelText(/API anahtarı/), { target: { value: " sk-ant-test " } });
    fireEvent.click(screen.getByRole("button", { name: "Ekle" }));

    await waitFor(() => expect(aiApi.addProvider).toHaveBeenCalledWith({ id: "anthropic", type: "anthropic", credential: "sk-ant-test" }));
  });

  it("asks where the data goes for an OpenAI-compatible host", async () => {
    gateway();
    vi.mocked(aiApi.addProvider).mockResolvedValueOnce({ id: "openai-compatible", type: "openai-compatible", base_url: null, reach: null, enabled: true, credential: null });
    render(<AiSettings />);

    fireEvent.change(await screen.findByRole("combobox", { name: /^Sağlayıcı/ }), { target: { value: "openai-compatible" } });
    fireEvent.change(screen.getByLabelText(/Adres/), { target: { value: "http://host.docker.internal:11434/v1" } });
    fireEvent.click(screen.getByRole("button", { name: "Ekle" }));

    await waitFor(() =>
      expect(aiApi.addProvider).toHaveBeenCalledWith({
        id: "openai-compatible",
        type: "openai-compatible",
        base_url: "http://host.docker.internal:11434/v1",
        reach: "self-hosted",
      }),
    );
  });

  it("points Korkut's chat and search names at the chosen models", async () => {
    gateway({ providers: [{ id: "anthropic", type: "anthropic" }, { id: "ollama", type: "openai-compatible" }] });
    vi.mocked(aiApi.enableModel).mockResolvedValue({});
    vi.mocked(aiApi.setRoute).mockResolvedValue({ name: "x", routes: [] });
    render(<AiSettings />);

    fireEvent.change(await screen.findByLabelText(/Sohbet modeli/), { target: { value: "anthropic/claude-sonnet-5" } });
    fireEvent.change(screen.getByLabelText(/Belge arama modeli/), { target: { value: "ollama/nomic-embed-text" } });
    fireEvent.click(screen.getByRole("button", { name: "Kaydet" }));

    await waitFor(() => expect(aiApi.setRoute).toHaveBeenCalledTimes(2));
    // A model llmr knows is only enabled; one it does not must say what it can do.
    expect(aiApi.enableModel).toHaveBeenCalledWith("anthropic", "claude-sonnet-5", "chat", false);
    expect(aiApi.enableModel).toHaveBeenCalledWith("ollama", "nomic-embed-text", "embedding", true);
    expect(aiApi.setRoute).toHaveBeenCalledWith(CHAT_ROUTE, ["anthropic/claude-sonnet-5"]);
    expect(aiApi.setRoute).toHaveBeenCalledWith(EMBED_ROUTE, ["ollama/nomic-embed-text"]);
  });

  it("reports a key the provider refused", async () => {
    gateway({ providers: [{ id: "anthropic", type: "anthropic" }] });
    vi.mocked(aiApi.testProvider).mockResolvedValueOnce({ provider: "anthropic", access: "denied", detail: "invalid x-api-key" });
    render(<AiSettings />);

    fireEvent.click(await screen.findByRole("button", { name: "Test et" }));

    expect(await screen.findByText("Sağlayıcı reddetti: invalid x-api-key")).toBeInTheDocument();
  });

  it("says so when the gateway cannot be reached", async () => {
    gateway();
    vi.mocked(aiApi.providerTypes).mockRejectedValue(new ApiError(503, "Yapay zeka ağ geçidine (llmr) ulaşılamadı."));
    render(<AiSettings />);

    expect(await screen.findByRole("alert")).toHaveTextContent("ulaşılamadı");
  });

  it("has no accessibility violations", async () => {
    gateway({ providers: [{ id: "anthropic", type: "anthropic" }] });
    render(<AiSettings />);
    await screen.findByLabelText(/Sohbet modeli/);

    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});
