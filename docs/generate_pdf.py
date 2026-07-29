"""Genere les PDF de documentation a partir des sources Markdown.

Usage :
    python docs/generate_pdf.py

Entrees  : docs/DOCUMENTATION_FONCTIONNELLE_ET_TECHNIQUE.md
           docs/GUIDE_UTILISATEUR.md
Sorties  : docs/Sovereign_OS_DIM_Documentation_Technique.pdf
           docs/Sovereign_OS_DIM_Guide_Utilisateur.pdf

Le rendu couvre le sous-ensemble Markdown utilise par ces documents :
titres h1 a h3, paragraphes, listes a puces, listes numerotees, tableaux
pipe, blocs de code, gras en ligne, code en ligne et separateurs.
Police : Montserrat, une seule famille pour tout le document, fichiers TTF
versionnes dans docs/fonts/ avec leur licence OFL. Aucune dependance hors fpdf2.
"""

from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from pathlib import Path

from fpdf import FPDF
from fpdf.enums import Align

DOCS = Path(__file__).resolve().parent
FONT_DIR = DOCS / "fonts"   # Montserrat, versionne avec le projet et sa licence OFL
CSPROJ = DOCS.parent / "SovereignOS.Desktop" / "SovereignOS.Desktop.csproj"


def read_version() -> str:
    """Version semver lue dans le csproj, source de verite unique."""
    match = re.search(r"<Version>([\d.]+)</Version>", CSPROJ.read_text(encoding="utf-8"))
    if not match:
        return "0.0.0"
    parts = match.group(1).split(".")
    return ".".join(parts[:3])

# --- Grille au nombre d'or -------------------------------------------------
# Le bloc de texte est un rectangle d'or : hauteur / largeur = phi.
# La marge basse vaut la marge haute multipliee par phi, ce qui laisse
# 244,64 mm de hauteur utile, donc 151,20 mm de largeur, donc 29,40 mm de
# marge laterale. Les corps de texte et les espacements suivent la meme
# progression (phi et sa racine), de sorte qu'aucune valeur n'est arbitraire.
PHI = 1.6180339887
PAGE_W, PAGE_H = 210.0, 297.0
MARGIN_TOP = 20.0
MARGIN_BOTTOM = MARGIN_TOP * PHI            # 32,36
TEXT_H = PAGE_H - MARGIN_TOP - MARGIN_BOTTOM  # 244,64
TEXT_W = TEXT_H / PHI                        # 151,20
MARGIN_SIDE = (PAGE_W - TEXT_W) / 2          # 29,40
RIGHT_EDGE = PAGE_W - MARGIN_SIDE

# Echelle typographique : raison racine de phi (1,272) autour du corps 10,5.
PT_CAPTION = 8.2
PT_BODY = 10.5
PT_H3 = 13.4
PT_H2 = 17.0
PT_TITLE = 27.8

# Interlignes et espacements, en millimetres, issus de la meme suite.
LEAD_BODY = 6.0          # 10,5 pt x phi
LEAD_SMALL = 4.7
SP_XS = 1.85
SP_S = 3.0
SP_M = 4.85
SP_L = 7.85
SP_XL = 12.7

# Reperes horizontaux de la couverture.
COVER_BAND = PAGE_H / (PHI ** 2)             # 113,40
COVER_TITLE_Y = COVER_BAND / PHI             # 70,08
COVER_INFO_Y = PAGE_H / PHI                  # 183,54
COVER_NOTE_Y = PAGE_H - PAGE_H / (PHI ** 4)  # 253,70
RULE_LEN = TEXT_W / (PHI ** 4)               # 22,03

VERSION = read_version()

NAVY = (0, 19, 41)
ACCENT = (0, 137, 123)
GREY = (110, 118, 129)
RULE = (222, 226, 230)
HEAD_BG = (240, 243, 246)


@dataclass
class Doc:
    source: Path
    output: Path
    title: str
    subtitle: str
    recipient: str
    icon: Path  # marque de couverture, jeu Icons8 Fluent


DOCUMENTS = [
    Doc(
        source=DOCS / "DOCUMENTATION_FONCTIONNELLE_ET_TECHNIQUE.md",
        output=DOCS / "Sovereign_OS_DIM_Documentation_Technique.pdf",
        title="Sovereign OS DIM",
        subtitle="Documentation fonctionnelle et technique",
        recipient="Direction des Ressources Numeriques - GHT Psy Sud Paris",
        icon=DOCS / "icons" / "securite.png",
    ),
    Doc(
        source=DOCS / "GUIDE_UTILISATEUR.md",
        output=DOCS / "Sovereign_OS_DIM_Guide_Utilisateur.pdf",
        title="Sovereign OS DIM",
        subtitle="Guide utilisateur",
        recipient="Departement d'Information Medicale - GHT Psy Sud Paris",
        icon=DOCS / "icons" / "guide.png",
    ),
]


class Renderer(FPDF):
    def __init__(self, doc: Doc):
        super().__init__(orientation="P", unit="mm", format="A4")
        self.doc = doc
        self.cover = True
        self.set_margins(MARGIN_SIDE, MARGIN_TOP, MARGIN_SIDE)
        self.set_auto_page_break(True, margin=MARGIN_BOTTOM)
        self.add_font("body", "", FONT_DIR / "Montserrat-Regular.ttf")
        self.add_font("body", "B", FONT_DIR / "Montserrat-SemiBold.ttf")
        self.add_font("body", "I", FONT_DIR / "Montserrat-Italic.ttf")
        self.add_font("mono", "", FONT_DIR / "Montserrat-Regular.ttf")
        self.add_font("mono", "B", FONT_DIR / "Montserrat-SemiBold.ttf")
        self.set_title(f"{doc.title} - {doc.subtitle}")
        self.set_author("Adam Beloucif")

    # -- gabarit ---------------------------------------------------------
    def header(self):
        if self.cover:
            return
        self.set_font("body", "", PT_CAPTION)
        self.set_text_color(*GREY)
        self.cell(0, LEAD_SMALL, f"{self.doc.title} - {self.doc.subtitle}", align=Align.L)
        self.cell(0, LEAD_SMALL, f"Version {VERSION}", align=Align.R, new_x="LMARGIN", new_y="NEXT")
        self.set_draw_color(*RULE)
        rule_y = MARGIN_TOP + SP_M
        self.line(MARGIN_SIDE, rule_y, RIGHT_EDGE, rule_y)
        self.set_y(rule_y + SP_L)

    def footer(self):
        if self.cover:
            return
        self.set_y(-(MARGIN_BOTTOM - SP_XL))
        self.set_font("body", "", PT_CAPTION)
        self.set_text_color(*GREY)
        self.cell(0, LEAD_SMALL, "Document interne - donnees de sante - diffusion restreinte", align=Align.L)
        self.cell(0, LEAD_SMALL, str(self.page_no()), align=Align.R)

    def draw_cover(self):
        self.add_page()
        self.set_fill_color(*NAVY)
        self.rect(0, 0, PAGE_W, COVER_BAND, style="F")

        # Marque Icons8 posee sur la diagonale d'or de la bande.
        if self.doc.icon.exists():
            mark = SP_XL * PHI                       # 20,55 mm
            self.image(
                str(self.doc.icon),
                x=MARGIN_SIDE,
                y=COVER_TITLE_Y - mark - SP_M,
                w=mark,
            )

        self.set_xy(MARGIN_SIDE, COVER_TITLE_Y)
        self.set_font("body", "B", PT_TITLE)
        self.set_text_color(255, 255, 255)
        self.cell(0, SP_XL, self.doc.title, new_x="LMARGIN", new_y="NEXT")
        self.set_x(MARGIN_SIDE)
        self.set_font("body", "", PT_H2)
        self.set_text_color(180, 205, 200)
        self.cell(0, SP_L, self.doc.subtitle, new_x="LMARGIN", new_y="NEXT")

        self.set_xy(MARGIN_SIDE, COVER_INFO_Y)
        self.set_text_color(*NAVY)
        self.set_font("body", "B", PT_BODY)
        self.cell(0, LEAD_BODY, self.doc.recipient, new_x="LMARGIN", new_y="NEXT")
        self.ln(SP_XS)
        self.set_font("body", "", PT_BODY)
        self.set_text_color(*GREY)
        for line in (
            f"Version applicative {VERSION}",
            "Adam BELOUCIF, apprenti ingenieur PMSI, DIM",
            "adam.beloucif@psysudparis.fr, 01 42 11 70 60",
            "Groupe Hospitalier Paul Guiraud, 54 avenue de la Republique, 94806 Villejuif cedex",
        ):
            self.set_x(MARGIN_SIDE)
            self.cell(0, LEAD_BODY, line, new_x="LMARGIN", new_y="NEXT")

        self.set_draw_color(*ACCENT)
        self.set_line_width(0.8)
        self.line(MARGIN_SIDE, COVER_NOTE_Y, MARGIN_SIDE + RULE_LEN, COVER_NOTE_Y)
        self.set_line_width(0.2)
        self.set_xy(MARGIN_SIDE, COVER_NOTE_Y + SP_S)
        self.set_font("body", "", PT_CAPTION)
        self.set_text_color(*GREY)
        # Saut de page desactive : la note tient sous le filet, elle ne doit pas
        # deborder sur la page du sommaire.
        self.set_auto_page_break(False)
        self.multi_cell(
            TEXT_W,
            LEAD_SMALL,
            "Ce document decrit une application de traitement de fichiers PMSI.\n"
            "Il ne contient aucune donnee patient.",
            align=Align.L,
        )
        self.set_auto_page_break(True, margin=MARGIN_BOTTOM)
        self.cover = False

    # -- blocs -----------------------------------------------------------
    def h1(self, text: str):
        self.add_page()
        self.set_font("body", "B", PT_TITLE / PHI ** 0.5)
        self.set_text_color(*NAVY)
        self.multi_cell(0, SP_L, text, new_x="LMARGIN", new_y="NEXT")
        self.set_draw_color(*ACCENT)
        self.set_line_width(0.6)
        y = self.get_y() + SP_XS
        self.line(MARGIN_SIDE, y, MARGIN_SIDE + RULE_LEN, y)
        self.set_line_width(0.2)
        self.ln(SP_L)

    def h2(self, text: str):
        # Un titre bascule a la page suivante seulement s'il ne reste pas de quoi
        # poser le titre et quelques lignes dessous. Sinon on cree des vides.
        if self.get_y() > PAGE_H - MARGIN_BOTTOM - SP_XL * PHI ** 2:
            self.add_page()
        self.start_section(text, level=0)
        self.ln(SP_M)
        self.set_font("body", "B", PT_H2)
        self.set_text_color(*NAVY)
        self.multi_cell(0, SP_L, text, new_x="LMARGIN", new_y="NEXT")
        self.ln(SP_XS)

    def h3(self, text: str):
        if self.get_y() > PAGE_H - MARGIN_BOTTOM - SP_XL * PHI:
            self.add_page()
        self.start_section(text, level=1)
        self.ln(SP_S)
        self.set_font("body", "B", PT_H3)
        self.set_text_color(*ACCENT)
        self.multi_cell(0, LEAD_BODY, text, new_x="LMARGIN", new_y="NEXT")
        self.ln(SP_XS / PHI)

    def paragraph(self, text: str):
        self.set_font("body", "", PT_BODY)
        self.set_text_color(30, 34, 40)
        self.write_rich(text, line_height=LEAD_BODY)
        self.ln(SP_S)

    def bullet(self, text: str, marker: str = "-"):
        self.set_font("body", "", PT_BODY)
        self.set_text_color(30, 34, 40)
        left = self.l_margin
        indent = SP_L                      # retrait de liste = 7,85 mm
        self.set_x(left + SP_S)
        self.cell(SP_M, LEAD_BODY, marker)
        self.set_x(left + indent)
        with self.local_context():
            self.set_left_margin(left + indent)
            self.write_rich(text, line_height=LEAD_BODY)
        self.set_left_margin(left)
        self.ln(SP_XS / PHI)

    def code_block(self, lines: list[str]):
        if not lines:
            return
        pad = SP_S
        usable = TEXT_W - 2 * pad
        # Corps calcule sur la largeur reellement occupee, pas sur un nombre de
        # caracteres : une mono large ferait deborder une ligne de commande.
        self.set_font("mono", "", 10)
        widest = max(self.get_string_width(ln) for ln in lines) or 1
        size = min(PT_BODY / PHI ** 0.5, 10 * usable / widest)
        height = size * 0.352778 * PHI      # interligne mono = corps x phi
        self.ln(SP_XS)
        top = self.get_y()
        needed = height * len(lines) + 2 * pad
        if top + needed > self.h - self.b_margin:
            self.add_page()
            top = self.get_y()
        self.set_fill_color(*HEAD_BG)
        self.rect(MARGIN_SIDE, top, TEXT_W, needed, style="F")
        self.set_xy(MARGIN_SIDE + pad, top + pad)
        self.set_font("mono", "", size)
        self.set_text_color(20, 30, 45)
        for line in lines:
            self.set_x(MARGIN_SIDE + pad)
            self.cell(0, height, line, new_x="LMARGIN", new_y="NEXT")
        self.set_y(top + needed)
        self.ln(SP_S)

    def table_block(self, rows: list[list[str]]):
        if not rows:
            return
        self.ln(SP_XS)
        self.set_font("body", "", PT_BODY / PHI ** 0.5)
        self.set_text_color(30, 34, 40)
        self.set_draw_color(*RULE)
        # La couleur de remplissage courante servirait sinon de fond de cellule :
        # on la remet a blanc, sinon les lignes heritent du navy de la couverture.
        self.set_fill_color(255, 255, 255)
        widths = column_widths(rows)
        with self.table(
            col_widths=widths,
            line_height=LEAD_SMALL,
            padding=(SP_XS, SP_XS * PHI, SP_XS, SP_XS * PHI),
            text_align=Align.L,
            headings_style=heading_style(self),
            borders_layout="HORIZONTAL_LINES",
            cell_fill_color=(255, 255, 255),
            cell_fill_mode="NONE",
        ) as table:
            for raw in rows:
                row = table.row()
                for cell in raw:
                    row.cell(strip_inline(cell))
        self.ln(SP_S)

    def figure(self, caption: str, path: Path, small: bool = False):
        if not path.exists():
            print(f"capture manquante : {path}", file=sys.stderr)
            return
        # Format galerie : largeur reduite d'un cran de la suite d'or, deux
        # captures tiennent alors sur une page sans laisser de blanc.
        width = TEXT_W / PHI ** 0.5 if small else TEXT_W
        from PIL import Image

        with Image.open(path) as img:
            height = width * img.height / img.width
        needed = height + SP_L
        if self.get_y() + needed > self.h - self.b_margin:
            self.add_page()
        self.ln(SP_XS)
        y = self.get_y()
        self.set_draw_color(*RULE)
        self.rect(MARGIN_SIDE, y, width, height)
        self.image(str(path), x=MARGIN_SIDE, y=y, w=width)
        self.set_y(y + height + SP_XS)
        self.set_font("body", "I", PT_CAPTION)
        self.set_text_color(*GREY)
        self.multi_cell(width, LEAD_SMALL, caption, new_x="LMARGIN", new_y="NEXT")
        self.ln(SP_S)

    def rule(self):
        self.ln(SP_XS)
        self.set_draw_color(*RULE)
        y = self.get_y()
        self.line(MARGIN_SIDE, y, RIGHT_EDGE, y)
        self.ln(SP_M)

    def write_rich(self, text: str, line_height: float):
        """Ecrit un paragraphe en gerant **gras** et `code`."""
        for chunk, style in split_inline(text):
            if style == "b":
                self.set_font("body", "B", self.font_size_pt)
            elif style == "code":
                self.set_font("mono", "", self.font_size_pt - 1)
            else:
                self.set_font("body", "", self.font_size_pt)
            self.write(line_height, chunk)
        self.ln(line_height)


def render_toc(pdf: "Renderer", outline):
    """Sommaire : titres de niveau 1 et 2, avec ligne de points et page."""
    pdf.set_font("body", "B", PT_TITLE / PHI ** 0.5)
    pdf.set_text_color(*NAVY)
    # Position explicite : dans le rendu differe du sommaire, le curseur n'est
    # pas garanti a la marge gauche, une cellule de largeur 0 deborderait.
    pdf.set_x(MARGIN_SIDE)
    pdf.cell(TEXT_W, SP_L, "Sommaire", align=Align.L, new_x="LMARGIN", new_y="NEXT")
    pdf.set_draw_color(*ACCENT)
    pdf.set_line_width(0.6)
    y = pdf.get_y() + SP_XS
    pdf.line(MARGIN_SIDE, y, MARGIN_SIDE + RULE_LEN, y)
    pdf.set_line_width(0.2)
    pdf.ln(SP_L)

    page_width = SP_XL
    for entry in outline:
        label = entry.name
        if entry.level == 0:
            pdf.ln(SP_XS / PHI)
            pdf.set_font("body", "B", PT_BODY / PHI ** 0.25)
            pdf.set_text_color(*NAVY)
            indent = 0.0
        else:
            pdf.set_font("body", "", PT_BODY / PHI ** 0.5)
            pdf.set_text_color(80, 88, 96)
            indent = SP_L
        pdf.set_x(MARGIN_SIDE + indent)
        label_width = pdf.get_string_width(label)
        available = TEXT_W - indent - label_width - page_width
        dots = ""
        if available > SP_M:
            dot_width = pdf.get_string_width(".")
            dots = " " + "." * max(0, int((available - SP_XS) / dot_width))
        pdf.cell(label_width + pdf.get_string_width(dots), LEAD_SMALL, label + dots)
        pdf.cell(page_width, LEAD_SMALL, str(entry.page_number), align=Align.R,
                 new_x="LMARGIN", new_y="NEXT")


def heading_style(pdf: FPDF):
    from fpdf.fonts import FontFace

    return FontFace(emphasis="BOLD", color=NAVY, fill_color=HEAD_BG)


def column_widths(rows: list[list[str]]) -> list[float]:
    """Largeurs relatives, avec un plancher pour qu'aucune colonne ne devienne
    plus etroite que le mot le plus long qu'elle doit accueillir."""
    cols = len(rows[0])
    scores = []
    for i in range(cols):
        cells = [strip_inline(r[i]) for r in rows]
        longest = max(len(c) for c in cells)
        average = sum(len(c) for c in cells) / len(cells)
        # Mot le plus long : il ne peut pas etre coupe, il fixe le plancher.
        longest_word = max((len(w) for c in cells for w in c.split()), default=4)
        scores.append((max(6.0, 0.4 * longest + 0.6 * average), longest_word))

    total = sum(s for s, _ in scores)
    shares = [100 * s / total for s, _ in scores]

    # Plancher en pourcentage : largeur du mot le plus long, marge de securite
    # comprise, rapportee a la largeur utile.
    floors = [
        min(100 / cols, 100 * (word * 2.0 + 2 * SP_XS * PHI) / TEXT_W)
        for _, word in scores
    ]
    shares = [max(s, f) for s, f in zip(shares, floors)]
    total = sum(shares)
    return [100 * s / total for s in shares]


BOLD_RE = re.compile(r"\*\*(.+?)\*\*")
CODE_RE = re.compile(r"`([^`]+)`")


def split_inline(text: str):
    """Decoupe un texte en segments (contenu, style)."""
    tokens: list[tuple[str, str]] = []
    pattern = re.compile(r"\*\*(.+?)\*\*|`([^`]+)`")
    pos = 0
    for match in pattern.finditer(text):
        if match.start() > pos:
            tokens.append((text[pos:match.start()], ""))
        if match.group(1) is not None:
            tokens.append((match.group(1), "b"))
        else:
            tokens.append((match.group(2), "code"))
        pos = match.end()
    if pos < len(text):
        tokens.append((text[pos:], ""))
    return tokens or [(text, "")]


def strip_inline(text: str) -> str:
    return CODE_RE.sub(r"\1", BOLD_RE.sub(r"\1", text)).strip()


def parse_table_row(line: str) -> list[str]:
    return [c.strip() for c in line.strip().strip("|").split("|")]


def is_separator(line: str) -> bool:
    return bool(re.fullmatch(r"\|[\s:|-]+\|", line.strip()))


def render(doc: Doc) -> Path:
    text = doc.source.read_text(encoding="utf-8")
    pdf = Renderer(doc)
    pdf.draw_cover()
    pdf.add_page()
    pdf.insert_toc_placeholder(render_toc, allow_extra_pages=True)

    lines = text.splitlines()
    i = 0
    paragraph: list[str] = []

    def flush():
        nonlocal paragraph
        if paragraph:
            pdf.paragraph(" ".join(paragraph))
            paragraph = []

    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        if stripped.startswith("```"):
            flush()
            block: list[str] = []
            i += 1
            while i < len(lines) and not lines[i].strip().startswith("```"):
                block.append(lines[i].rstrip())
                i += 1
            pdf.code_block(block)
            i += 1
            continue

        if stripped.startswith("|"):
            flush()
            rows = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                if not is_separator(lines[i]):
                    rows.append(parse_table_row(lines[i]))
                i += 1
            pdf.table_block(rows)
            continue

        if not stripped:
            flush()
            i += 1
            continue

        if stripped == "---":
            flush()
            pdf.rule()
            i += 1
            continue

        # ![legende](chemin) ou ![legende](chemin){small} pour le format galerie
        image = re.match(r"^!\[(.*?)\]\((.+?)\)(\{small\})?$", stripped)
        if image:
            flush()
            pdf.figure(
                image.group(1),
                (DOCS / image.group(2)).resolve(),
                small=bool(image.group(3)),
            )
            i += 1
            continue

        if stripped.startswith("### "):
            flush()
            pdf.h3(stripped[4:])
            i += 1
            continue

        if stripped.startswith("## "):
            flush()
            pdf.h2(stripped[3:])
            i += 1
            continue

        if stripped.startswith("# "):
            flush()
            i += 1
            continue

        numbered = re.match(r"^(\d+)\.\s+(.*)$", stripped)
        if stripped.startswith("- ") or numbered:
            flush()
            marker = f"{numbered.group(1)}." if numbered else "-"
            content = numbered.group(2) if numbered else stripped[2:]
            i += 1
            while i < len(lines) and lines[i].startswith("   ") and lines[i].strip():
                content += " " + lines[i].strip()
                i += 1
            pdf.bullet(content, marker)
            continue

        paragraph.append(stripped)
        i += 1

    flush()
    pdf.output(str(doc.output))
    return doc.output


def main() -> int:
    for doc in DOCUMENTS:
        if not doc.source.exists():
            print(f"source manquante : {doc.source}", file=sys.stderr)
            return 1
        out = render(doc)
        print(f"{out.name}  {out.stat().st_size // 1024} Ko")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
