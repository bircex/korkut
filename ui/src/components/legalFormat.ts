// Karar tam metnini (İlgili Kararlar drawer'ı, bkz. SourcesDrawer) okunur Markdown'a çevirir.
// Kaynak istemcileri HTML'i "markdown-imsi" DÜZ metne indirger (LegalText.HtmlToMarkdown):
// gerçek markdown yapısı yoktur ve tek satır sonlarını ReactMarkdown yutunca metin duvara
// döner. Bu dönüştürücü SUNUM katmanıdır (modele giden araç çıktısı değişmez) ve muhafazakârdır:
// metni asla değiştirmez, yalnızca satır yapısını korur ve bariz kalıpları işaretler:
//   - UYAP künye etiketleri ("MAHKEMESİ : X", "SAYISI : ...") → kalın etiket
//   - Romen rakamlı bölüm başlıkları ("I. İSTEM", "II. KARAR") → alt başlık
//   - Daire + E./K. satırı → boşlukları toparlanmış kalın satır
//   - "İçtihat Metni" ayracı → alt başlık
//   - diğer her satır: satır sonu korunur (markdown hard break)

const TR_UPPER = "A-ZÇĞİÖŞÜÂÎÛ";

// "MAHKEMESİ : Asliye Ceza Mahkemesi" gibi tamamı büyük harfli kısa künye etiketi.
const LABEL_LINE = new RegExp(`^([${TR_UPPER}][${TR_UPPER}0-9 .()\\-/]{1,60}?)\\s*:\\s?(.*)$`);

// "I. İSTEM", "II. KARAR", "III- DEĞERLENDİRME" gibi kısa bölüm başlığı.
const ROMAN_HEADING = /^([IVXLC]{1,6})[.)-]\s+(.{1,80})$/;

// Daire/kurul + esas/karar numarası satırı: "2. Ceza Dairesi    2026/6127 E. , 2026/10109 K."
const CASE_HEADER = /\d+\/\d+\s*E\./;
const DECISION_HEADER = /\d+\/\d+\s*K\./;

function isLabelLine(line: string): RegExpMatchArray | null {
  const m = line.match(LABEL_LINE);
  if (!m) return null;
  // Etiket kısmında küçük harf varsa künye etiketi değildir ("Kaynak: …" gibi satırlar kalır).
  return /[a-zçğıöşü]/.test(m[1]) ? null : m;
}

export function formatLegalText(raw: string): string {
  const text = raw
    .replace(/\r\n?/g, "\n")
    .replace(/\t/g, " ")
    // Portalın metne yapışık '"İçtihat Metni"' ayracı kendi satırına iner.
    .replace(/"İçtihat Metni"\s*/g, '"İçtihat Metni"\n');

  const out: string[] = [];
  for (const rawLine of text.split("\n")) {
    const line = rawLine.trim();
    if (line.length === 0) {
      out.push("");
      continue;
    }

    if (line === '"İçtihat Metni"' || line === "İçtihat Metni") {
      out.push("### İçtihat Metni");
      continue;
    }

    const roman = line.match(ROMAN_HEADING);
    if (roman && !/[a-zçğıöşü]{4,}/.test(line.slice(0, 4))) {
      out.push(`### ${line.replace(/\s{2,}/g, " ")}`);
      continue;
    }

    // Künye etiketi ÖNCE denenir: "SAYISI : 2023/779 E., 2024/144 K." gibi satırlar E./K.
    // içerdiği için daire-başlığı kuralına da uyar; etiket kuralı kazanmalı.
    const label = isLabelLine(line);
    if (label) {
      const value = label[2].trim();
      out.push(value.length > 0 ? `**${label[1].trim()}:** ${value}` : `**${label[1].trim()}:**`);
      continue;
    }

    if (CASE_HEADER.test(line) && DECISION_HEADER.test(line) && /\s{2,}/.test(line)) {
      out.push(`**${line.replace(/\s{2,}/g, " ").replace(/\s+,\s+/g, ", ")}**`);
      continue;
    }

    out.push(line.replace(/\s{2,}/g, " "));
  }

  // Paragraf içindeki satır sonları hard break olarak korunur (iki boşluk + \n); boş satırlar
  // paragraf sınırı kalır. Markdown blok satırlarına (başlık/liste) hard break eklenmez.
  return out
    .map((l, i) => {
      if (l === "" || l.startsWith("### ") || l.startsWith("- ")) return l;
      const next = out[i + 1];
      return next && next !== "" && !next.startsWith("### ") ? `${l}  ` : l;
    })
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}
