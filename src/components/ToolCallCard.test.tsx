import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ToolCallCard } from "./ToolCallCard";
import type { ToolCallInfo } from "../api";

describe("ToolCallCard", () => {
  it("renders nothing when there are no calls", () => {
    const { container } = render(<ToolCallCard calls={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows the tool name collapsed, with a spinner while the result is still pending", () => {
    const calls: ToolCallInfo[] = [{ name: "search_decisions", arguments: { source: "yargitay" } }];
    render(<ToolCallCard calls={calls} />);

    expect(screen.getByText("search_decisions")).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("Çalışıyor");
    expect(screen.queryByText("yargitay")).not.toBeInTheDocument(); // henüz açılmadı
  });

  it("expands to show arguments and a JSON result once it arrives", async () => {
    const user = userEvent.setup();
    const calls: ToolCallInfo[] = [
      {
        name: "calc_adli_para_cezasi",
        arguments: { gun_sayisi: 100, gunluk_miktar: 200 },
        result: { content: JSON.stringify({ toplam_ceza: 20000 }), isError: false },
      },
    ];
    render(<ToolCallCard calls={calls} />);

    await user.click(screen.getByRole("button", { name: /calc_adli_para_cezasi/ }));

    expect(screen.getByText("Girdi")).toBeInTheDocument();
    expect(screen.getByText("100")).toBeInTheDocument();
    expect(screen.getByText("Sonuç")).toBeInTheDocument();
    expect(screen.getByText(/20[.,]?000/)).toBeInTheDocument();
  });

  it("renders an error result as plain text under a Hata heading", async () => {
    const user = userEvent.setup();
    const calls: ToolCallInfo[] = [{ name: "create_matter", arguments: {}, result: { content: "Yetkiniz yok.", isError: true } }];
    render(<ToolCallCard calls={calls} />);

    await user.click(screen.getByRole("button", { name: /create_matter/ }));

    expect(screen.getByText("Hata")).toBeInTheDocument();
    expect(screen.getByText("Yetkiniz yok.")).toBeInTheDocument();
  });
});
