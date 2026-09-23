"""Fabrique l'icone de Pinel a partir du dessin source, pour tous ses usages.

Une seule source, assets/icone-pinel.svg, et tout le reste en derive : icone de
l'application, icone de l'onglet de l'interface, marques de couverture des
documents PDF. Redessiner la marque ne demande donc qu'une modification et une
commande, jamais une retouche fichier par fichier.

Ce que la commande ecrit :
    src/Pinel.Desktop/app.ico        16 a 256 pixels, carres, pour Windows
    web/icons/favicon.png            onglet de l'interface embarquee
    docs/icons/guide.png             couverture des documents destines au DIM
    docs/icons/securite.png          couverture du document de securite

Les deux marques de couverture ne different que par la couleur du champ mis en
evidence : ocre pour les guides, rouge pour la securite et la conformite. Elles
remplacent les pictogrammes Icons8 utilises jusqu'au 23/09/2026, qui
imposaient une mention d'attribution.

Usage :
    python tools/make_icon.py
"""

from __future__ import annotations

import io
import sys
from pathlib import Path

import cairosvg
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "assets" / "icone-pinel.svg"

# En dessous de cette taille, le dessin complet devient illisible : les trois
# lignes se touchent. La variante des petites tailles prend le relais, comme on
# le fait pour toute icone soignee.
SMALL_SOURCE = ROOT / "assets" / "icone-pinel-petite.svg"
SMALL_UP_TO = 32

# Tailles attendues par l'explorateur Windows, de l'affichage en liste jusqu'a
# la tuile du menu Demarrer. Toutes carrees : une icone non carree est etiree
# par le systeme, ce qui etait le defaut de l'icone precedente.
ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)

ACCENT = "#fcc764"


def render(size: int, accent: str = ACCENT) -> Image.Image:
    source = SMALL_SOURCE if size <= SMALL_UP_TO and SMALL_SOURCE.exists() else SOURCE
    svg = source.read_text(encoding="utf-8")
    if accent != ACCENT:
        svg = svg.replace(ACCENT, accent)
    png = cairosvg.svg2png(bytestring=svg.encode("utf-8"), output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def write_ico(path: Path) -> None:
    """
    Chaque taille est dessinee pour elle-meme. Sans append_images, Pillow
    redimensionne la plus grande image pour toutes les autres, et la variante
    des petites tailles ne serait jamais embarquee : l'icone aurait l'air
    juste a 256 et resterait illisible dans la barre des taches.
    """
    # Image de base la plus grande, les autres jointes : Pillow apparie chaque
    # taille demandee avec l'image jointe de meme taille, et ne redimensionne
    # que celles qui manquent.
    images = [render(size) for size in sorted(ICO_SIZES, reverse=True)]
    path.parent.mkdir(parents=True, exist_ok=True)
    images[0].save(
        path,
        format="ICO",
        sizes=[(s, s) for s in sorted(ICO_SIZES, reverse=True)],
        append_images=images[1:],
    )
    print(f"{path.relative_to(ROOT)} : {', '.join(f'{s}x{s}' for s in ICO_SIZES)}")


def write_png(path: Path, size: int, accent: str = ACCENT) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    render(size, accent).save(path, format="PNG")
    print(f"{path.relative_to(ROOT)} : {size}x{size}")


def main() -> int:
    for source in (SOURCE, SMALL_SOURCE):
        if not source.exists():
            raise SystemExit(f"Dessin source introuvable : {source}")

    write_ico(ROOT / "src" / "Pinel.Desktop" / "app.ico")
    write_png(ROOT / "web" / "icons" / "favicon-16.png", 16)
    write_png(ROOT / "web" / "icons" / "favicon.png", 64)
    write_png(ROOT / "docs" / "icons" / "guide.png", 512)
    write_png(ROOT / "docs" / "icons" / "securite.png", 512, accent="#c0392b")
    return 0


if __name__ == "__main__":
    sys.exit(main())
