"""Emet les licences de Pinel et garde la cle qui les signe.

Pourquoi une signature et pas un appel a un serveur : les postes d'un
departement d'information medicale n'ont pas internet. Une licence est donc un
petit fichier signe, verifiable hors ligne par la cle publique embarquee dans
l'application. Rien ne sort du poste, rien n'est appele, et un etablissement
isole reste autonome.

Ce que contient une licence, et rien d'autre : la raison sociale, le FINESS
d'inscription e-PMSI auquel elle est rattachee, ses dates, et un numero. Aucune
donnee personnelle, aucune donnee de sante.

La cle privee ne vit jamais dans le depot. Elle est rangee sous
~/.secrets/pinel-licences/ et c'est la seule piece a sauvegarder : la perdre
oblige a reemettre toutes les licences.

Usage :
    python tools/licence.py cles                 cree la paire de cles, une fois
    python tools/licence.py emettre --etablissement "CH de ..." \
        --finess 210000123 --mois 12
    python tools/licence.py verifier licence.lic
"""

from __future__ import annotations

import argparse
import base64
import json
import sys
from datetime import date, timedelta
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa

ROOT = Path(__file__).resolve().parent.parent
SECRETS = Path.home() / ".secrets" / "pinel-licences"
PRIVATE_KEY = SECRETS / "cle-privee.pem"
PUBLIC_KEY = SECRETS / "cle-publique.pem"
EMBEDDED_KEY = ROOT / "src" / "Pinel.Core" / "Licensing" / "cle-publique.pem"
LICENCES = SECRETS / "emises"


def b64(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).decode().rstrip("=")


def unb64(text: str) -> bytes:
    return base64.urlsafe_b64decode(text + "=" * (-len(text) % 4))


def keys() -> int:
    if PRIVATE_KEY.exists():
        raise SystemExit(f"La cle existe deja : {PRIVATE_KEY}. La remplacer invaliderait toutes les licences emises.")
    SECRETS.mkdir(parents=True, exist_ok=True)
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    PRIVATE_KEY.write_bytes(key.private_bytes(
        serialization.Encoding.PEM,
        serialization.PrivateFormat.PKCS8,
        serialization.NoEncryption()))
    public_pem = key.public_key().public_bytes(
        serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo)
    PUBLIC_KEY.write_bytes(public_pem)
    EMBEDDED_KEY.parent.mkdir(parents=True, exist_ok=True)
    EMBEDDED_KEY.write_bytes(public_pem)
    print(f"Cle privee : {PRIVATE_KEY}  (a sauvegarder, jamais dans le depot)")
    print(f"Cle publique embarquee dans l'application : {EMBEDDED_KEY.relative_to(ROOT)}")
    return 0


def issue(etablissement: str, finess: str, mois: int, numero: str | None) -> int:
    if not PRIVATE_KEY.exists():
        raise SystemExit("Aucune cle. Lancer d'abord : python tools/licence.py cles")
    if not (finess.isdigit() and len(finess) == 9):
        raise SystemExit("Le FINESS d'inscription e-PMSI compte neuf chiffres.")

    debut = date.today()
    payload = {
        "etablissement": etablissement,
        "finess": finess,
        "emise": debut.isoformat(),
        "expire": (debut + timedelta(days=31 * mois)).isoformat(),
        "numero": numero or f"{finess}-{debut:%Y%m}",
    }
    body = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")

    key = serialization.load_pem_private_key(PRIVATE_KEY.read_bytes(), password=None)
    signature = key.sign(body, padding.PKCS1v15(), hashes.SHA256())

    LICENCES.mkdir(parents=True, exist_ok=True)
    path = LICENCES / f"{payload['numero']}.lic"
    path.write_text(f"{b64(body)}.{b64(signature)}\n", encoding="ascii")
    print(f"Licence emise : {path}")
    print(f"  {etablissement}, FINESS {finess}, valable jusqu'au {payload['expire']}")
    print("A transmettre a l'etablissement, qui l'importe depuis l'ecran A propos.")
    return 0


def verify(path: str) -> int:
    body_b64, _, signature_b64 = Path(path).read_text(encoding="ascii").strip().partition(".")
    body, signature = unb64(body_b64), unb64(signature_b64)
    key = serialization.load_pem_public_key(PUBLIC_KEY.read_bytes())
    try:
        key.verify(signature, body, padding.PKCS1v15(), hashes.SHA256())
    except Exception:
        print("Signature invalide.")
        return 1
    payload = json.loads(body)
    valide = date.fromisoformat(payload["expire"]) >= date.today()
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    print("Signature valide." + ("" if valide else " Licence expiree."))
    return 0 if valide else 2


def main() -> int:
    parser = argparse.ArgumentParser(description="Licences de Pinel.")
    sub = parser.add_subparsers(dest="commande", required=True)
    sub.add_parser("cles", help="Cree la paire de cles, une seule fois.")
    emettre = sub.add_parser("emettre", help="Emet une licence pour un etablissement.")
    emettre.add_argument("--etablissement", required=True)
    emettre.add_argument("--finess", required=True, help="FINESS d'inscription e-PMSI, neuf chiffres.")
    emettre.add_argument("--mois", type=int, default=12)
    emettre.add_argument("--numero")
    verifier = sub.add_parser("verifier", help="Relit une licence emise.")
    verifier.add_argument("fichier")
    args = parser.parse_args()

    if args.commande == "cles":
        return keys()
    if args.commande == "emettre":
        return issue(args.etablissement, args.finess, args.mois, args.numero)
    return verify(args.fichier)


if __name__ == "__main__":
    sys.exit(main())
