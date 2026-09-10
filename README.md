# Pinel

Outil de traitement des fichiers PMSI psychiatrie pour le Département d'Information Médicale du GHT Psy Sud Paris.

Pinel convertit les fichiers au format national ATIH (largeur fixe) en CSV exploitable, crée des épisodes de prise en charge ambulatoire, organise la structure du GHT et applique les contrôles de qualité du PMSI selon le cahier des charges du 22 décembre 2025.

**Pour qui** : techniciens d'information médicale, médecins du DIM, et équipes DSI évaluant la solution.

---

## Architecture

```mermaid
graph TB
    subgraph Desktop["Application Bureau .NET 8"]
        WPF["Interface WPF"]
        Bridge["Pont HTTP<br/>127.0.0.1:5000<br/>Authentification par jeton"]
    end

    subgraph Web["Interface HTML Embarquée"]
        UI["Pages HTML/CSS/JS<br/>- Conversion formats<br/>- Épisodes ambulatoires<br/>- Structure GHT<br/>- Contrôles de qualité<br/>- Configuration"]
    end

    subgraph Core["Pinel.Core - Logique Métier"]
        Format["Conversion de formats<br/>RPS, RAA, FICHCOMP..."]
        Episode["Épisodes de prise<br/>en charge"]
        Structure["Fichier structure<br/>du GHT"]
        Quality["Contrôles de qualité<br/>Chaînages et doublons"]
        IO["Lecture/écriture<br/>fichiers locaux"]
    end

    subgraph Local["Système de Fichiers Local"]
        Input["Fichiers sources<br/>texte/CSV"]
        Output["Fichiers générés<br/>CSV/XLSX"]
    end

    WPF -->|initialise| UI
    UI -->|appels HTTP| Bridge
    Bridge -->|execute| Format
    Bridge -->|execute| Episode
    Bridge -->|execute| Structure
    Bridge -->|execute| Quality
    Format -->|utilise| IO
    Episode -->|utilise| IO
    Structure -->|utilise| IO
    Quality -->|utilise| IO
    IO -->|lit| Input
    IO -->|ecrit| Output

    style Desktop fill:#e8f4f8
    style Web fill:#f0f8e8
    style Core fill:#f8f0e8
    style Local fill:#efefef
```

**Points clés** : L'application fonctionne en standalone sur le poste local. Aucune donnée ne sort de la machine. L'interface Web embarquée et servie sur la boucle locale (127.0.0.1) via un serveur HTTP intégré au processus WPF. Tous les fichiers lus et écrits se limitent aux dossiers que l'utilisateur autorise.

---

## Dossiers

| Chemin | Responsabilité |
|--------|---|
| `src/Pinel.Core` | Logique métier : formats ATIH, épisodes ambulatoires, contrôles de qualité, entrées/sorties fichiers |
| `src/Pinel.Desktop` | Interface WPF, serveur HTTP local, authentification, pont WebView2 vers Pinel.Core |
| `web/` | Pages HTML, CSS, JavaScript de l'interface utilisateur |
| `tests/Pinel.Tests` | Tous les tests xUnit couvrant Pinel.Core |
| `docs/` | Quatre documents destinés à différents lecteurs |

---

## Compilation et lancement

**Prérequis** : .NET 8 SDK, disponible sur https://dotnet.microsoft.com/download.

**Compiler en Debug** :
```bash
dotnet build Pinel.sln
```

**Lancer l'application** (après compilation) :
```bash
dotnet run --project src/Pinel.Desktop/Pinel.Desktop.csproj
```

**Lancer les tests** :
```bash
dotnet test Pinel.sln
```

**Construire un exécutable autonome** (Release, fichier unique distribué) :
```bash
dotnet publish src/Pinel.Desktop/Pinel.Desktop.csproj -c Release
```

L'exécutable se trouve ensuite dans `src/Pinel.Desktop/bin/Release/net8.0-windows/win-x64/publish/Pinel.exe`. Aucune installation requise, aucun droit administrateur.

---

## Documentation

Quatre documents complémentaires, à lire selon votre rôle :

| Document | Lecteur | Contenu |
|----------|--------|---------|
| `docs/01_GUIDE_UTILISATEUR.md` | Technicien TIM, Médecin DIM | Prise en main, écrans, données, premiers pas |
| `docs/02_DOSSIER_FONCTIONNEL_DIM.md` | Département d'Information Médicale | Spécifications du cahier des charges, règles de gestion, formats reconnus |
| `docs/03_DOSSIER_TECHNIQUE_DSI.md` | Direction des Ressources Numériques | Architecture, dépendances, déploiement, maintenance |
| `docs/04_SECURITE_ET_CONFORMITE.md` | DSI, Responsable conformité | Authentification, RGPD, anonymisation, contrôles de sécurité |

---

## Limites connues

Cette section est volontairement franche. Les points ci-dessous sont établis, et ils doivent être corrigés ou validés par le DIM avant toute mise en production.

**Le format est reconnu sur le nom du fichier, pas sur la ligne** : `AtihFormatIdentifier` déduit le format du seul nom du fichier, puis `AtihParser` applique le même découpage à toutes ses lignes. C'est un défaut de conception. Un fichier PMSI peut mélanger plusieurs types d'enregistrement, chacun avec sa propre structure, et le type est porté par la ligne elle-même. Conséquence directe pour l'utilisateur : un fichier dont le nom ne contient pas le sigle du format est ignoré, et un fichier mal nommé est découpé avec le mauvais gabarit sans aucun message.

**Les positions des champs embarquées sont à reprendre** : `AtihMatrix` fige les positions de 23 formats, et son propre commentaire indique qu'elles ont été reprises d'un script Python antérieur, non des descriptifs officiels de l'ATIH. Sur 23 entrées, 12 déclarent des positions identiques. Cas le plus net : les formats anonymes RPSA et R3A se voient attribuer une position d'identifiant patient et de date de naissance, alors que l'arrêté du 23 décembre 2016 modifié, article 5, établit que ces fichiers n'en contiennent pas, le numéro d'identification permanent y étant remplacé par une anonymisation irréversible. Les descriptifs officiels de l'ATIH font foi et doivent être déposés format par format.

**Deux formats déclarés n'ont pas de réalité vérifiée** : FICHSUP est un recueil agrégé, sans ligne patient, et il a été supprimé en psychiatrie au 1er janvier 2021 au profit de FICHCOMP. EDGAR n'est pas un format de fichier mais une typologie d'actes (entretien, démarche, groupe, accompagnement, réunion) codée à l'intérieur du RAA.

**La clé de chaînage retenue est discutable** : les contrôles de couverture joignent l'activité et le VID-HOSP sur l'identifiant permanent du patient. La clé de chaînage nationale repose sur le numéro administratif de séjour et sur une anonymisation du numéro de sécurité sociale, jamais sur l'identifiant interne de l'établissement. À trancher avec le DIM avant de se fier aux anomalies produites.

**Corrections volontaires sur FICHCOMP transports** : la ligne FICHCOMP produite pour les transports corrige trois défauts du classeur Excel d'origine, qui sont documentés et assumés. Premièrement, chaque formule de la colonne M visait une ligne située 196 rangs plus bas, donc composait la ligne d'un autre patient. Deuxièmement, la date était lue dans une colonne étiquetée identifiant patient et restée vide, au lieu de la date de transport. Troisièmement, le libellé d'unité fonctionnelle, de longueur variable, était concaténé sans être complété, ce qui décalait tout ce qui suivait. Les lignes produites ne seront donc pas identiques à celles du classeur actuel. Deux largeurs de champ ne sont attestées par aucune formule d'origine et restent à valider par le DIM.

**Aucun contrôle ne bloque une transmission sur le contenu** : le niveau bloquant n'est atteint qu'en cas d'erreur d'ouverture de fichier. Le garde-fou annoncé au chapitre 8 du guide utilisateur ne peut donc pas passer au rouge sur une donnée fausse.

**Intelligence artificielle sur l'exhaustivité du recueil** : le cahier des charges citait des pistes pour signaler les lacunes en ambulatoire. Cette phase n'est pas traitée dans cette version.

---

## Retours et améliorations

Tous les retours sur l'interface, les noms, les critères de validation et les limites peuvent être transmis au DIM. Les noms d'écrans et les libellés de colonnes sont particulièrement malléables et seront adaptés si demandé.
