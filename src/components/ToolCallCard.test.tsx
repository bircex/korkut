import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ToolCallCard } from "./ToolCallCard";
import type { ToolCallInfo } from "../api";

describe("ToolCallCard", () => {
  it("renders nothing when there are no calls", () => {
    const { container } = render(<ToolCallCard calls={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows a Turkish label (never the raw tool name), with a spinner while the result is still pending", () => {
    const calls: ToolCallInfo[] = [{ name: "search_decisions", arguments: { source: "yargitay" } }];
    render(<ToolCallCard calls={calls} />);

    expect(screen.getByText("Karar aranıyor")).toBeInTheDocument();
    expect(screen.queryByText("search_decisions")).not.toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("Çalışıyor");
  });

  it("falls back to a humanized name for a tool missing from the label map", () => {
    const calls: ToolCallInfo[] = [{ name: "some_future_tool", arguments: { x: 1 } }];
    render(<ToolCallCard calls={calls} />);

    expect(screen.getByText("Some future tool")).toBeInTheDocument();
  });

  it("never renders arguments, results, or any expandable detail — even once the result arrives", () => {
    const calls: ToolCallInfo[] = [
      {
        name: "calc_adli_para_cezasi",
        arguments: { gun_sayisi: 100, gunluk_miktar: 200 },
        result: { content: JSON.stringify({ toplam_ceza: 20000 }), isError: false },
      },
    ];
    render(<ToolCallCard calls={calls} />);

    expect(screen.getByText("Adli para cezası hesaplanıyor")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByText("Girdi")).not.toBeInTheDocument();
    expect(screen.queryByText("Sonuç")).not.toBeInTheDocument();
    expect(screen.queryByText("100")).not.toBeInTheDocument();
    expect(screen.queryByText(/20[.,]?000/)).not.toBeInTheDocument();
  });

  it("signals an error result with a warning icon, never the raw error text", () => {
    const calls: ToolCallInfo[] = [{ name: "create_matter", arguments: {}, result: { content: "Yetkiniz yok.", isError: true } }];
    render(<ToolCallCard calls={calls} />);

    expect(screen.getByText("Dosya açılıyor")).toBeInTheDocument();
    expect(screen.queryByText("Yetkiniz yok.")).not.toBeInTheDocument();
    expect(screen.queryByText("Hata")).not.toBeInTheDocument();
  });
});
