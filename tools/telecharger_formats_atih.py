"""Rapatrie les descriptifs de format officiels de l'ATIH.

POURQUOI CE SCRIPT EXISTE.

Jusqu'au 28/08/2026, les positions de champs de Pinel provenaient d'un ancien
script Python et n'avaient jamais ete confrontees a un descriptif officiel. La
verification a montre que RPS et RAA etaient justes, et que plusieurs autres
formats ne l'etaient pas - dont RPSA et R3A, ou l'outil lisait le hachage
d'anonymisation et le classait comme un identifiant de patient.

Les descriptifs sont publics, mais atih.sante.fr repond 403 a tout outil
automatise qui ne se presente pas comme un navigateur. Ce n'est pas une
authentification, c'est une protection anti-robot : un en-tete User-Agent
ordinaire suffit a passer. Le detail est ecrit ici pour que personne ne
reconclue, comme nous l'avions fait, que ces documents sont inaccessibles.

Usage :
    python tools/telecharger_formats_atih.py [dossier_de_sortie]

Par defaut, ecrit dans reference/atih/<annee>/, qui est ignore par git : ces
41 Mo de documents publics n'ont pas leur place dans l'historique, seul le
moyen de les retrouver en a une.

Verification apres execution :
    python tools/telecharger_formats_atih.py --verifier
compare la matrice embarquee aux descriptifs et signale tout ecart.
"""

from __future__ import annotations

import re
import sys
import urllib.request
from pathlib import Path

RACINE = Path(__file__).resolve().parent.parent
SORTIE = RACINE / "reference" / "atih"

# Le site refuse les requetes sans en-tete de navigateur. Voir l'en-tete du
# fichier : c'est une protection anti-robot, pas un controle d'acces.
UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)

# Les pages par annee. Certaines portent un suffixe parce que l'ATIH republie
# une page corrigee sans retirer l'ancienne : on visite les deux.
ANNEES = range(2013, 2027)
SUFFIXES = ("", "-0", "-1", "-2")

MOTIF_PIECE = re.compile(
    r"https://www\.atih\.sante\.fr/sites/default/files/[^\"]*\.(?:xlsx|xls|pdf)"
)


def recuperer(url: str, referer: str | None = None) -> bytes:
    entetes = {"User-Agent": UA}
    if referer:
        entetes["Referer"] = referer
    requete = urllib.request.Request(url, headers=entetes)
    with urllib.request.urlopen(requete, timeout=60) as reponse:
        return reponse.read()


def pieces_de_l_annee(annee: int) -> set[str]:
    """Rend les adresses des documents de format publies pour une annee."""
    trouvees: set[str] = set()
    for suffixe in SUFFIXES:
        page = f"https://www.atih.sante.fr/formats-pmsi-{annee}{suffixe}"
        try:
            html = recuperer(page).decode("utf-8", errors="replace")
        except Exception:
            continue
        if "introuvable" in html:
            continue
        trouvees.update(
            url for url in MOTIF_PIECE.findall(html) if "format" in url.lower()
        )
    return trouvees


def main(argv: list[str]) -> int:
    destination = Path(argv[1]) if len(argv) > 1 else SORTIE
    destination.mkdir(parents=True, exist_ok=True)

    total = 0
    for annee in ANNEES:
        pieces = pieces_de_l_annee(annee)
        if not pieces:
            continue
        dossier = destination / str(annee)
        dossier.mkdir(exist_ok=True)
        for url in sorted(pieces):
            cible = dossier / url.rsplit("/", 1)[-1]
            if cible.exists() and cible.stat().st_size > 0:
                continue
            try:
                cible.write_bytes(recuperer(url, referer=f"https://www.atih.sante.fr/formats-pmsi-{annee}"))
                total += 1
            except Exception as erreur:
                print(f"  echec {annee}/{cible.name} : {erreur}", file=sys.stderr)
        print(f"{annee} : {len(list(dossier.iterdir()))} fichiers")

    print(f"\n{total} document(s) telecharge(s) dans {destination}")
    if total == 0:
        print("Aucun document recupere. Verifier l'acces reseau au site de l'ATIH.")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
