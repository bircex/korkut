import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import type { SourceRef } from "../api";
import { SourcesDrawer } from "./SourcesDrawer";

const KARAR: SourceRef = {
  kind: "ictihat",
  source: "yargitay",
  id: "k1",
  title: "Yargıtay Ceza Genel Kurulu 2015/89 E. 2018/140 K.",
  date: "2018-04-02",
  snippet: "Sanığın katılana gönderdiği ifade nedeniyle...",
  url: "https://example.org/karar/1",
};

const KANUN: SourceRef = {
  kind: "mevzuat",
  source: "mevzuat",
  id: "5237",
  title: "Türk Ceza Kanunu",
};

describe("SourcesDrawer", () => {
  it("içtihatları sayan başlıkla açılır ve kartları çizer", () => {
    render(<SourcesDrawer sources={[KARAR, { ...KARAR, id: "k2", title: "İkinci Karar" }]} onClose={() => {}} />);
    expect(screen.getByRole("complementary", { name: "İlgili Kararlar (2)" })).toBeInTheDocument();
    expect(screen.getByText(KARAR.title)).toBeVisible();
    expect(screen.getByText("İkinci Karar")).toBeVisible();
    expect(screen.getAllByText(/Karar tarihi: 2018-04-02/).length).toBeGreaterThan(0);
  });

  it("mevzuat hitlerini ayrı bölümde, mevzuat rozetiyle gösterir", () => {
    render(<SourcesDrawer sources={[KARAR, KANUN]} onClose={() => {}} />);
    expect(screen.getByRole("heading", { name: "Mevzuat" })).toBeVisible();
    expect(screen.getByText("Türk Ceza Kanunu")).toBeVisible();
    expect(screen.getByText("İçtihat")).toBeVisible();
    expect(screen.getByText("Mevzuat", { selector: "span" })).toBeVisible();
  });

  it("yalnız mevzuat varsa başlık Kaynaklar olur", () => {
    render(<SourcesDrawer sources={[KANUN]} onClose={() => {}} />);
    expect(screen.getByRole("complementary", { name: "Kaynaklar (1)" })).toBeInTheDocument();
  });

  it("url'li kart yeni sekmeye güvenli dış bağlantı verir, url'siz kart vermez", () => {
    render(<SourcesDrawer sources={[KARAR, KANUN]} onClose={() => {}} />);
    const link = screen.getByRole("link", { name: "Kaynakta aç" });
    expect(link).toHaveAttribute("href", KARAR.url);
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", expect.stringContaining("noopener"));
    expect(screen.getAllByRole("link")).toHaveLength(1);
  });

  it("kapat düğmesi onClose çağırır", () => {
    const onClose = vi.fn();
    render(<SourcesDrawer sources={[KARAR]} onClose={onClose} />);
    fireEvent.click(screen.getByRole("button", { name: "Kapat" }));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
