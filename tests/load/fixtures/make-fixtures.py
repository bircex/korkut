#!/usr/bin/env python3
"""Generate the load-test fixtures: a scanned-page PNG (for real OCR) and a plain-text contract
(for the agent upload path). The PNG renders Turkish legal text as an image so Tesseract has real
work to do. Self-bootstraps Pillow if missing."""

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

TEXT = (
    "T.C. YARGITAY 4. HUKUK DAİRESİ\n"
    "Esas No: 2023/12345  Karar No: 2024/6789\n\n"
    "DAVA: Davacı vekili dava dilekçesinde özetle; müvekkilinin kişilik\n"
    "haklarına yönelik hakaret niteliğindeki sözler nedeniyle manevi\n"
    "tazminata hükmedilmesini talep etmiştir.\n\n"
    "KARAR: Somut olayda kullanılan ifadelerin, muhatabın onur, şeref ve\n"
    "saygınlığını rencide edici nitelikte olduğu; Türk Ceza Kanunu m.125\n"
    "kapsamında hakaret suçunun unsurlarının oluştuğu anlaşılmaktadır.\n"
    "Bu itibarla yerel mahkeme kararı usul ve yasaya uygundur.\n\n"
    "SONUÇ: Temyiz itirazlarının reddi ile hükmün ONANMASINA, oybirliğiyle\n"
    "karar verildi.\n"
)

CONTRACT = (
    "TAŞINIR SATIŞ SÖZLEŞMESİ\n\n"
    "MADDE 1 - TARAFLAR: İşbu sözleşme, satıcı [Satıcı Adı] ile alıcı [Alıcı Adı]\n"
    "arasında akdedilmiştir.\n"
    "MADDE 2 - KONU: Sözleşmenin konusu, aşağıda nitelikleri belirtilen taşınırın\n"
    "satışıdır.\n"
    "MADDE 3 - BEDEL: Satış bedeli KDV dahil [tutar] TL olup, teslim anında peşin\n"
    "ödenecektir.\n"
    "MADDE 4 - AYIP: Alıcı, malı teslim aldığında muayene ile yükümlüdür (TBK m.223).\n"
    "MADDE 5 - UYUŞMAZLIK: İşbu sözleşmeden doğacak uyuşmazlıklarda [yer] mahkemeleri\n"
    "ve icra daireleri yetkilidir.\n"
) * 6  # bulk it up so extraction has real content


def ensure_pillow():
    try:
        import PIL  # noqa: F401
        return
    except ImportError:
        print("Pillow bulunamadı, kuruluyor...", file=sys.stderr)
        subprocess.check_call([sys.executable, "-m", "pip", "install", "--quiet", "--user", "pillow"])


def main():
    ensure_pillow()
    from PIL import Image, ImageDraw, ImageFont

    img = Image.new("RGB", (1100, 1500), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 26)
    except Exception:
        font = ImageFont.load_default()

    y = 60
    for line in TEXT.splitlines():
        draw.text((60, y), line, fill="black", font=font)
        y += 42

    png = os.path.join(HERE, "scan.png")
    img.save(png, "PNG")
    print(f"wrote {png} ({os.path.getsize(png)} bytes)")

    txt = os.path.join(HERE, "contract.txt")
    with open(txt, "w", encoding="utf-8") as f:
        f.write(CONTRACT)
    print(f"wrote {txt} ({os.path.getsize(txt)} bytes)")


if __name__ == "__main__":
    main()
