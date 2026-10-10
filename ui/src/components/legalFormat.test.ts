import { describe, expect, it } from "vitest";
import { formatLegalText } from "./legalFormat";

// Gerçek bir Bedesten fetch çıktısından alınmış örnek (LegalText.HtmlToMarkdown indirgemesi):
// yapışık "İçtihat Metni" ayracı, sekmeli künye etiketleri, geniş boşluklu daire/E./K. satırı.
const SAMPLE = [
  "Kaynak: https://mevzuat.adalet.gov.tr/ictihat/1217011200",
  "",
  "2. Ceza Dairesi         2026/6127 E.  ,  2026/10109 K.",
  "",
  '"İçtihat Metni"MAHKEMESİ\t:Asliye Ceza Mahkemesi',
  "SAYISI\t: 2023/779 E., 2024/144 K.",
  "SUÇLAR\t: Hırsızlık, resmi belgeyi bozma, yok etme veya gizleme",
  "",
  "I. İSTEM Yargıtay Cumhuriyet Başsavcılığının 01.04.2026 tarihli istemi.",
  "Gerekçe metni burada devam eder.",
  "İkinci satır aynı paragraftadır.",
].join("\n");

describe("formatLegalText", () => {
  const out = formatLegalText(SAMPLE);

  it("yapışık İçtihat Metni ayracını başlığa çevirir", () => {
    expect(out).toContain("### İçtihat Metni");
    expect(out).not.toContain('"İçtihat Metni"MAHKEMESİ');
  });

  it("büyük harfli künye etiketlerini kalınlaştırır", () => {
    expect(out).toContain("**MAHKEMESİ:** Asliye Ceza Mahkemesi");
    expect(out).toContain("**SUÇLAR:** Hırsızlık, resmi belgeyi bozma, yok etme veya gizleme");
  });

  it("E./K. içeren künye etiketi satırında etiket kuralı daire-başlığı kuralını yener", () => {
    expect(out).toContain("**SAYISI:** 2023/779 E., 2024/144 K.");
    expect(out).not.toContain("**SAYISI : 2023/779");
  });

  it("daire + E./K. başlık satırını toparlayıp kalınlaştırır", () => {
    expect(out).toContain("**2. Ceza Dairesi 2026/6127 E., 2026/10109 K.**");
  });

  it("romen rakamlı bölüm başlığını alt başlık yapar", () => {
    expect(out).toMatch(/^### I\. İSTEM Yargıtay/m);
  });

  it("paragraf içi satır sonlarını hard break olarak korur", () => {
    expect(out).toContain("Gerekçe metni burada devam eder.  \nİkinci satır aynı paragraftadır.");
  });

  it("Kaynak satırını olduğu gibi bırakır (küçük harf içerir, künye etiketi sayılmaz)", () => {
    expect(out).toContain("Kaynak: https://mevzuat.adalet.gov.tr/ictihat/1217011200");
    expect(out).not.toContain("**Kaynak:**");
  });

  it("mevzuat liste satırlarına dokunmaz", () => {
    const mevzuat = "Madde 125\n- birinci bent\n- ikinci bent";
    const f = formatLegalText(mevzuat);
    expect(f).toContain("- birinci bent\n- ikinci bent");
  });

  it("boş ve bozuk girdilerde patlamaz", () => {
    expect(formatLegalText("")).toBe("");
    expect(formatLegalText("   \n\n  ")).toBe("");
  });
});
