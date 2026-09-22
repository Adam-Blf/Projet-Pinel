"""Rapatrie en local les polices et les icones de l'interface.

Pourquoi : Pinel tourne sur des postes DIM sans acces internet, et la direction
des ressources numeriques exige zero flux sortant. Toute police servie par un
CDN casse l'affichage sur un poste isole.

Typographie du produit : Montserrat, en une seule famille pour toute
l'interface. Les chiffres restent alignes grace a font-variant-numeric.

Usage :
    python tools/vendor_assets.py

Icones : Phosphor (licence MIT), graisse regular, version figee ci-dessous.
Chaque glyphe est copie en SVG et sert de masque CSS colore par currentColor,
net a toute densite d'ecran. Necessite npm sur le poste de developpement,
jamais sur le poste DIM.

Produit :
    web/fonts/fonts.css     @font-face reecrits en chemins locaux, sous-ensembles
                            latin et latin-ext seulement (interface en francais)
    web/fonts/*.woff2       fichiers de police
    web/icons/*.svg         glyphes Phosphor utilises par l'interface
    web/legal/phosphor-mit.txt  texte de la licence Phosphor

Verification apres execution, aucun resultat attendu :
    grep -rn "https://" web/index.html web/css web/js
"""

from __future__ import annotations

import hashlib
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FONTS = ROOT / "web" / "fonts"
ICONS = ROOT / "web" / "icons"
LEGAL = ROOT / "web" / "legal"

PHOSPHOR_PACKAGE = "@phosphor-icons/core@2.1.1"
PHOSPHOR_WEIGHT = "regular"

# Glyphes utilises par l'interface (attribut data-icon de index.html et des
# scripts). Un glyphe absent de la liste ne s'affiche pas : l'ajouter ici.
PHOSPHOR_ICONS = (
    "ambulance",
    "arrow-counter-clockwise",
    "check-circle",
    "circle-notch",
    "clock-counter-clockwise",
    "download-simple",
    "file-arrow-down",
    "folder-lock",
    "folder-plus",
    "identification-card",
    "info",
    "lightning",
    "list-checks",
    "magnifying-glass",
    "moon",
    "scroll",
    "sun",
    "tree-structure",
    "warning-circle",
)

# Sous-ensembles conserves : l'interface est en francais, les blocs cyrillique
# et vietnamien ne seraient jamais telecharges par le moteur mais alourdissent
# l'installation.
KEPT_SUBSETS = ("latin", "latin-ext")

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


def keep_subsets(css: str) -> str:
    """Garde les blocs @font-face des sous-ensembles utiles, precedes de leur commentaire."""
    blocks = re.findall(r"/\* ([\w-]+) \*/\s*(@font-face \{.*?\})", css, flags=re.S)
    kept = [f"/* {name} */\n{body}" for name, body in blocks if name in KEPT_SUBSETS]
    return "\n".join(kept) + "\n"


def vendor_icons() -> int:
    npm = shutil.which("npm")
    if npm is None:
        print("npm introuvable : icones non rapatriees", file=sys.stderr)
        return 1
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run([npm, "pack", PHOSPHOR_PACKAGE, "--silent"], cwd=tmp, check=True,
                       stdout=subprocess.DEVNULL)
        archive = next(Path(tmp).glob("*.tgz"))
        with tarfile.open(archive) as tar:
            tar.extractall(tmp, filter="data")
        assets = Path(tmp) / "package" / "assets" / PHOSPHOR_WEIGHT
        for old in ICONS.glob("*"):
            old.unlink()
        ICONS.mkdir(parents=True, exist_ok=True)
        for name in PHOSPHOR_ICONS:
            shutil.copyfile(assets / f"{name}.svg", ICONS / f"{name}.svg")
        LEGAL.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(Path(tmp) / "package" / "LICENSE", LEGAL / "phosphor-mit.txt")
    print(f"{len(PHOSPHOR_ICONS)} icones Phosphor ({PHOSPHOR_WEIGHT}) dans {ICONS}")
    return 0


def main() -> int:
    FONTS.mkdir(parents=True, exist_ok=True)
    for old in FONTS.glob("*.woff2"):
        old.unlink()

    css = keep_subsets(fetch(FONTS_CSS).decode("utf-8"))
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
    return vendor_icons()


if __name__ == "__main__":
    raise SystemExit(main())
