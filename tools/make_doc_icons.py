"""Produit les marques de couverture des PDF de documentation.

Pourquoi : les deux marques d'origine venaient du jeu Icons8 Fluent, abandonne
par le projet. Toute icone de Pinel vient desormais de Phosphor, deja versionne
sous licence MIT dans web/icons/ et servi en local par l'interface. Ce script ne
telecharge rien : il rasterise les fichiers du depot.

La marque se pose sur une bande bleu nuit, elle est donc rendue en blanc.

Usage :
    python tools/make_doc_icons.py

Produit :
    docs/icons/<nom>.png     marques de couverture, 512 px, fond transparent

Verification apres execution :
    python -c "from PIL import Image; import glob; [print(f, Image.open(f).size, Image.open(f).mode) for f in sorted(glob.glob('docs/icons/*.png'))]"
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

import cairosvg

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "web" / "icons"
TARGET = ROOT / "docs" / "icons"

TAILLE = 512
BLANC = "#ffffff"

# Une marque par document. Le glyphe dit de quoi parle le document avant que le
# titre ne soit lu, donc il se choisit sur le contenu, pas sur l'esthetique.
MARQUES = {
    "commencer": "info",                  # 00, la porte d'entree
    "guide": "scroll",                    # 01, le document qu'on deroule
    "fonctionnel": "list-checks",         # 02, les regles metier une a une
    "technique": "tree-structure",        # 03, l'architecture
    "securite": "folder-lock",            # 04, les donnees sous garde
    "glossaire": "magnifying-glass",      # 06, on y cherche un mot
}


def blanchir(svg: str) -> str:
    """Force le trace en blanc.

    Les fichiers Phosphor portent un fill sur la racine et parfois sur les
    formes. Remplacer toutes les occurrences est plus sur que de viser la
    racine : un glyphe a plusieurs formes, et une seule oubliee sort en noir sur
    fond bleu nuit, donc invisible.
    """
    svg = re.sub(r'fill="(?!none)[^"]*"', f'fill="{BLANC}"', svg)
    if "fill=" not in svg.split(">", 1)[0]:
        svg = svg.replace("<svg", f'<svg fill="{BLANC}"', 1)
    return svg


def main() -> int:
    if not SOURCE.is_dir():
        print(f"source introuvable : {SOURCE}", file=sys.stderr)
        return 1

    TARGET.mkdir(parents=True, exist_ok=True)
    manquants = []

    for nom, glyphe in MARQUES.items():
        origine = SOURCE / f"{glyphe}.svg"
        if not origine.exists():
            manquants.append(f"{nom} : {glyphe}.svg absent de web/icons/")
            continue

        cairosvg.svg2png(
            bytestring=blanchir(origine.read_text(encoding="utf-8")).encode("utf-8"),
            write_to=str(TARGET / f"{nom}.png"),
            output_width=TAILLE,
            output_height=TAILLE,
            background_color=None,
        )
        print(f"{nom + '.png':20} <- {glyphe}.svg")

    if manquants:
        # Un glyphe absent se signale, il ne s'approxime pas.
        for ligne in manquants:
            print(f"attention : {ligne}", file=sys.stderr)
        return 1

    print(f"\n{len(MARQUES)} marques dans {TARGET}")
    print("Licence : Phosphor Icons, MIT, texte dans web/legal/phosphor-mit.txt")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
