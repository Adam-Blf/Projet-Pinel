"""Fabrique l'installateur Pinel et, au besoin, publie la mise a jour.

Pourquoi un installateur plutot qu'un exe pose sur le Bureau : le DIM doit
pouvoir recevoir une correction sans qu'un technicien passe sur chaque poste.
Velopack installe Pinel sous le profil de l'utilisateur, sans droits
d'administration, et chaque poste va chercher les versions suivantes dans un
dossier du reseau du GHT. Aucun acces internet n'est necessaire, et un poste
isole continue de tourner avec la version qu'il a deja.

Ce que la commande produit, dans dist/ :
    Pinel-<version>-win-Setup.exe   installateur a distribuer une fois
    Pinel-<version>-full.nupkg      paquet complet
    Pinel-<version>-delta.nupkg     difference avec la version precedente
    releases.win.json               index que les postes interrogent

Usage :
    python tools/packager.py                       fabrique l'installateur
    python tools/packager.py --version 1.1.0       force la version
    python tools/packager.py --publier "\\\\serveur\\partage\\pinel"
                                                   copie la sortie sur le partage

Prerequis, une seule fois sur le poste de fabrication :
    dotnet tool install -g vpk
"""

from __future__ import annotations

import argparse
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DESKTOP = ROOT / "src" / "Pinel.Desktop" / "Pinel.Desktop.csproj"
PUBLISH = ROOT / "publish" / "velopack"
DIST = ROOT / "dist"
PACK_ID = "Pinel"


def project_version() -> str:
    """Version declaree par le projet : une seule source, celle du csproj."""
    match = re.search(r"<Version>([^<]+)</Version>", DESKTOP.read_text(encoding="utf-8"))
    if match is None:
        raise SystemExit("Version introuvable dans Pinel.Desktop.csproj")
    return match.group(1).strip()


def run(command: list[str], **kwargs) -> None:
    print("  " + " ".join(command))
    subprocess.run(command, check=True, cwd=ROOT, **kwargs)


def tool(name: str) -> str:
    path = shutil.which(name)
    if path is None:
        raise SystemExit(f"{name} introuvable. Installer avec : dotnet tool install -g {name}")
    return path


def build(version: str) -> None:
    if PUBLISH.exists():
        shutil.rmtree(PUBLISH)
    print("Publication autonome, un seul fichier :")
    run([tool("dotnet"), "publish", str(DESKTOP), "-c", "Release", "-o", str(PUBLISH)])

    print("Fabrication de l'installateur et des paquets de mise a jour :")
    run([
        tool("vpk"), "pack",
        "--packId", PACK_ID,
        "--packVersion", version,
        "--packDir", str(PUBLISH),
        "--packTitle", "Pinel",
        "--packAuthors", "Adam Beloucif",
        "--mainExe", "Pinel.exe",
        "--icon", str(ROOT / "src" / "Pinel.Desktop" / "app.ico"),
        "--outputDir", str(DIST),
    ])


def publish(share: str) -> None:
    """Copie les paquets sur le partage : c'est ce dossier que les postes interrogent."""
    target = Path(share)
    if not target.exists():
        raise SystemExit(f"Partage injoignable : {share}")
    print(f"Publication vers {target} :")
    run([
        tool("vpk"), "upload", "local",
        "--outputDir", str(DIST),
        "--path", str(target),
        "--keepMaxReleases", "5",
    ])


def main() -> int:
    parser = argparse.ArgumentParser(description="Fabrique l'installateur Pinel.")
    parser.add_argument("--version", help="Version du paquet, par defaut celle du projet.")
    parser.add_argument("--publier", metavar="DOSSIER", help="Partage reseau ou copier la mise a jour.")
    args = parser.parse_args()

    version = args.version or project_version()
    print(f"Pinel {version}\n")
    build(version)
    if args.publier:
        publish(args.publier)

    setup = next(DIST.glob(f"*{version}*Setup.exe"), None)
    print()
    print(f"Installateur : {setup if setup else DIST}")
    print("A distribuer une fois par poste. Les versions suivantes se prennent")
    print("toutes seules dans le dossier de mise a jour configure dans l'ecran A propos.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
