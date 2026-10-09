import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { CalculatorForm } from "./CalculatorForm";
import { CALC_DEFINITIONS } from "../calcDefinitions";
import { api } from "../api";

// Faz 3 (KDM roadmap): GET /api/calc/schema is the live source of truth for min/max — this proves
// CalculatorForm actually applies it over calcDefinitions.ts's hand-written fallback, not just that
// the fetch fires.
vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return {
    ...mod,
    api: { ...mod.api, calc: vi.fn(), calcSchema: vi.fn() },
  };
});

const judicialFine = CALC_DEFINITIONS.find((d) => d.slug === "adli-para-cezasi")!;

describe("CalculatorForm", () => {
  it("falls back to calcDefinitions.ts min/max while the live schema is still loading", () => {
    vi.mocked(api.calcSchema).mockReturnValue(new Promise(() => {})); // never resolves
    render(<CalculatorForm def={judicialFine} />);

    const input = screen.getByLabelText(/Gün sayısı/);
    expect(input).toHaveAttribute("min", "5");
    expect(input).toHaveAttribute("max", "730");
  });

  it("overrides calcDefinitions.ts min/max once the live /api/calc/schema constraint arrives", async () => {
    vi.mocked(api.calcSchema).mockResolvedValue({
      "adli-para-cezasi": {
        gun_sayisi: { required: true, minimum: 1, maximum: 999 },
        gunluk_miktar: { required: true, minimum: 100, maximum: 500 },
        taksit: { required: true, minimum: null, maximum: null },
      },
    });
    render(<CalculatorForm def={judicialFine} />);

    await waitFor(() => {
      expect(screen.getByLabelText(/Gün sayısı/)).toHaveAttribute("max", "999");
    });
    expect(screen.getByLabelText(/Gün sayısı/)).toHaveAttribute("min", "1");
  });

  it("keeps the hardcoded value when the live schema has no entry for a field", async () => {
    vi.mocked(api.calcSchema).mockResolvedValue({ "adli-para-cezasi": {} });
    render(<CalculatorForm def={judicialFine} />);

    await waitFor(() => expect(api.calcSchema).toHaveBeenCalled());
    expect(screen.getByLabelText(/Gün sayısı/)).toHaveAttribute("min", "5");
    expect(screen.getByLabelText(/Gün sayısı/)).toHaveAttribute("max", "730");
  });
});
