"""Rapatrie en local les polices de l'interface.

Pourquoi : Pinel tourne sur des postes DIM sans acces internet, et la direction
des ressources numeriques exige zero flux sortant. Toute police servie par un
CDN casse l'affichage sur un poste isole.

Typographie du produit : Montserrat, en une seule famille pour toute
l'interface. Les chiffres restent alignes grace a font-variant-numeric.

Usage :
    python tools/vendor_assets.py

Produit :
    web/fonts/fonts.css     @font-face reecrits en chemins locaux
    web/fonts/*.woff2       fichiers de police

Verification apres execution, aucun resultat attendu :
    grep -rn "https://" web/index.html web/css web/js
"""

from __future__ import annotations

import hashlib
import re
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FONTS = ROOT / "web" / "fonts"

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)

FONTS_CSS = (
    "https://fonts.googleapis.com/css2"
    "?family=Montserrat:wght@400;500;600;700;800"
    "&display=swap"
)

FAMILY_SLUGS = {
    "montserrat": "montserrat",
}


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def main() -> int:
    FONTS.mkdir(parents=True, exist_ok=True)
    for old in FONTS.glob("*.woff2"):
        old.unlink()

    css = fetch(FONTS_CSS).decode("utf-8")
    urls = sorted(set(re.findall(r"https://fonts\.gstatic\.com/[^)]+\.woff2", css)))

    for url in urls:
        family = next((slug for key, slug in FAMILY_SLUGS.items() if key in url.lower()), "font")
        filename = f"{family}-{hashlib.sha1(url.encode()).hexdigest()[:8]}.woff2"
        data = fetch(url)
        (FONTS / filename).write_bytes(data)
        css = css.replace(url, filename)
        print(f"{filename:34} {len(data) // 1024:>4} Ko")

    (FONTS / "fonts.css").write_text(css, encoding="utf-8")
    remaining = len(re.findall(r"https://", css))
    if remaining:
        print(f"attention : {remaining} URL restantes dans fonts.css", file=sys.stderr)
        return 1

    print(f"\n{len(urls)} fichiers de police dans {FONTS}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
