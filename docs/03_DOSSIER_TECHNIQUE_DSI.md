# Dossier technique

Version 1.0.0.

Ce document s'adresse à la direction des systèmes d'information qui doit
autoriser, déployer et exploiter Pinel. Il répond aux questions posées avant
toute exécution, et propose un protocole de recette en environnement isolé.

Les sigles sont développés à leur première apparition et figurent dans le
[glossaire](06_GLOSSAIRE.md).

---

## 1. Les réponses en une page

| Question | Réponse |
|---|---|
| **À quoi ça sert ?** | Découper des fichiers PMSI à largeur fixe en tableaux exploitables, les contrôler, et reconstruire des parcours de soins ambulatoires. |
| **Qui s'en sert ?** | Les techniciens et médecins du département d'information médicale, sur leur poste. |
| **En quoi est-ce écrit ?** | C# sur .NET 8. Interface en HTML affichée par le composant Microsoft WebView2. |
| **Ça accède au réseau ?** | Aucun accès à internet. Un service web local écoute sur l'adresse de bouclage, joignable depuis le poste seul. Un dossier de mise à jour sur un partage interne, si et seulement si un chemin est renseigné. |
| **Ça stocke quoi, et où ?** | Réglages, descriptifs, journal d'audit, licence, règles apprises, modèle et une clé de pseudonymisation, tous sous `%LOCALAPPDATA%\Pinel-DIM`. Voir le chapitre 5, le détail compte. |
| **Ça se connecte au système d'information ?** | Non. Aucun flux vers la facturation, le dossier patient, les outils de pilotage ou l'entrepôt de requêtes. |
| **Une base de données ?** | Aucune. Les données lues vivent en mémoire de session. |
| **Des droits d'administration ?** | Aucun, ni à l'installation, ni à l'exécution, ni à la mise à jour. |

---

## 2. Architecture

Un seul processus, un seul fichier exécutable à distribuer.

### 2.1 Les cinq projets

| Projet | Rôle |
|---|---|
| `Pinel.Core` | Toute la logique métier : formats, contrôles, épisodes, structure, pseudonymisation, apprentissage, licence, confinement des chemins |
| `Pinel.Desktop` | La fenêtre, le service web local, et le point d'entrée qui aiguille la ligne de commande |
| `Pinel.Cli` | **Une bibliothèque**, pas un exécutable séparé : les verbes de la ligne de commande, référencés par `Pinel.Desktop` pour qu'il n'y ait qu'un seul fichier à distribuer |
| `Pinel.Ml` | Le modèle de détection des suppressions |
| `Pinel.Tests` | 244 tests automatisés |

### 2.2 La chaîne d'appel

```
Fenêtre WPF
   |
   +-- WebView2 affiche l'interface HTML depuis un hôte virtuel local
   |        |
   |        +-- appels HTTP vers 127.0.0.1:8787, porteur de jeton
   |                 |
   +-- Service web local (ASP.NET Core, en processus)
            |
            +-- Pinel.Core  -> système de fichiers, dossiers autorisés seulement
            +-- Pinel.Cli   -> mêmes traitements, en ligne de commande
            +-- Pinel.Ml    -> modèle local
```

### 2.3 Les dépendances

| Paquet | Version | Projet | Ce que ça apporte |
|---|---|---|---|
| ClosedXML | 0.105.0 | Core | Lecture et écriture de classeurs Excel |
| System.Text.Encoding.CodePages | 10.0.6 | Core | Encodage ISO-8859-1 des fichiers PMSI |
| Microsoft.Web.WebView2 | 1.0.3912.50 | Desktop | Affichage de l'interface HTML |
| Velopack | 1.2.158 | Desktop | Installation sous le profil et mise à jour différentielle |
| Microsoft.ML | 5.0.0 | Ml | Cadre d'apprentissage automatique |
| Microsoft.ML.LightGbm | 5.0.0 | Ml | Arbres de décision boostés, avec des binaires natifs par plateforme |
| System.Security.Cryptography.ProtectedData | 10.0.12 | Cli | Protection de la clé de pseudonymisation par le service Windows DPAPI |

Trois de ces dépendances changent la nature du produit du point de vue d'une
direction informatique et méritent d'être vues : **Velopack** parce qu'il
installe et met à jour, **ML.NET et LightGBM** parce qu'ils embarquent une
bibliothèque d'apprentissage automatique avec des binaires natifs, et
**ProtectedData** parce qu'il manipule un secret.

### 2.4 L'interface ne dépend de rien d'externe

Aucun réseau de diffusion de contenu n'est appelé. Polices et pictogrammes sont
embarqués dans l'exécutable et extraits au premier lancement. Une page servie
depuis un poste sans internet s'affiche à l'identique.

---

## 3. Flux et accès réseau

| Flux | Origine | Destination | Port | Nature |
|---|---|---|---|---|
| Interface vers service | Le processus | Lui-même | 8787, bouclage | HTTP local, porteur de jeton |
| Lecture et écriture de fichiers | Le processus | Dossiers déclarés, locaux ou partages | - | Système de fichiers |
| Recherche de mise à jour | Le processus | Un partage de l'établissement | - | Lecture de fichiers, uniquement si un chemin est renseigné |

**Aucun flux sortant vers internet, à aucun moment.** La vérification de licence
est entièrement locale, voir le chapitre 7.

### 3.1 Protection du service local

| Mesure | Détail |
|---|---|
| Liaison | Écoute sur `http://127.0.0.1:8787` uniquement, jamais sur une interface réseau |
| Jeton | 32 octets aléatoires tirés au démarrage, exigés en en-tête d'autorisation sur **toutes** les routes sauf le test de vie. À défaut, refus |
| Liste blanche d'hôtes | Seuls `127.0.0.1:8787` et `localhost:8787` sont acceptés. C'est la parade à la réattribution de nom, où une page hostile fait pointer son propre nom vers l'adresse de bouclage pour parler au service |
| Origines | Une seule origine autorisée, l'hôte virtuel de l'interface, et trois méthodes |
| En-têtes | Pas de mise en cache, pas de réinterprétation du type de contenu |
| Environnement | Forcé en production, ce qui supprime les pages d'erreur détaillées |

Une variable d'environnement permet d'imposer le jeton. **Elle est destinée aux
tests automatisés et ne doit pas être utilisée en exploitation** : un jeton fixe
perd l'intérêt d'un jeton tiré à chaque démarrage.

Le port 8787 n'est configurable par aucun réglage : il est inscrit dans le code.
Si ce port est déjà pris sur les postes du service, c'est un point à signaler.

### 3.2 Les routes

Trente-sept routes, réparties en huit familles : système et journal, licence et
mise à jour, réglages, conversion, épisodes, identitovigilance et contrôles,
structure et fichiers complémentaires, revue des corrections, outils.

Dix-huit d'entre elles sont tracées au journal d'audit après exécution.

**Toute opération sur un fichier passe par la validation de chemin** décrite au
chapitre 4, avec un refus explicite si le chemin sort des dossiers autorisés.

---

## 4. Confinement des fichiers

Pinel ne lit et n'écrit que dans des emplacements déclarés. Trois sources :

1. les dossiers ajoutés par l'utilisateur ;
2. le dossier de travail par défaut, sous le profil ;
3. les dossiers imposés par stratégie de groupe, le cas échéant.

Sont refusés : les remontées d'arborescence, les flux de données alternés NTFS
qui permettent d'attacher un contenu caché à un fichier, les chemins étendus qui
désactivent la normalisation Windows, et les liens ou jonctions qui pointent hors
de la zone autorisée.

Ce dernier point mérite l'attention d'une direction informatique : **une jonction
NTFS n'est pas un raccourci**, le système la suit à l'ouverture. Un dossier
autorisé qui contiendrait une jonction vers ailleurs ouvrirait cet ailleurs. La
résolution est faite avant la comparaison.

---

## 5. Stockage local

**Emplacement : `%LOCALAPPDATA%\Pinel-DIM`**, et délibérément pas
`%LOCALAPPDATA%\Pinel`.

Le motif est important pour l'exploitation. Depuis que l'application s'installe et
se met à jour seule, **l'installateur est propriétaire du dossier qui porte le nom
du produit** : il y fait le ménage à chaque mise à jour et l'efface à la
désinstallation. Les réglages, le journal, la licence, les règles apprises, le
modèle et la clé de pseudonymisation auraient disparu avec lui.

Une installation antérieure qui aurait rangé ces pièces sous l'ancien dossier est
reprise au premier démarrage, pièce connue par pièce connue, sans jamais écraser
un fichier déjà présent.

| Chemin | Contenu | Donnée patient |
|---|---|---|
| `settings.json` | Dossiers autorisés, dossier de sortie, dossier des descriptifs, identité de l'établissement | non |
| `formats\` | Descriptifs de format déposés | non |
| `travail\` | Espace de travail par défaut | **oui, si l'utilisateur y dépose des fichiers** |
| `audit-AAAAMMJJ.log` | Journal d'audit | non, voir chapitre 6 |
| `interface\{version}\` | Interface extraite au premier lancement | non |
| `licence.lic` | Licence signée | non |
| `maj.json` | Chemin du partage de mise à jour | non |
| `regles-apprises.json` | Règles apprises et empreintes des paires traitées | non, codes seulement |
| `modele\` | Modèle entraîné, sa fiche, et l'historique | non, codes et décomptes seulement |
| **`anonymisation.key`** | **Clé de 32 octets, protégée par DPAPI au compte courant** | **c'est un secret** |

> **La ligne à ne pas manquer est la dernière.** La clé de pseudonymisation est un
> secret cryptographique. Sa perte rend le chaînage des copies pseudonymisées
> irrécupérable ; sa compromission rend la pseudonymisation réversible pour qui
> détient ces copies. Sa sauvegarde, sa rotation et son sort lors d'un changement
> de poste sont des décisions attendues de la direction informatique.

---

## 6. Journal d'audit

Une ligne par opération sensible, chacune étant un enregistrement complet, ce qui
permet de lire le fichier au fil de l'eau. Rotation quotidienne.

Champs enregistrés : horodatage, point de terminaison, méthode, code de réponse,
volume, et éventuellement un détail et un **dossier**.

### 6.1 La garantie exacte

La version précédente de ce document affirmait qu'aucun chemin de fichier ne
transitait par le journal. **C'est faux, et le choix est assumé.** Un chemin de
dossier n'est pas une donnée de patient, et c'est précisément ce qui rend un acte
imputable : il nomme l'espace de travail sur lequel on a agi.

La garantie réelle, mécanique et couverte par des tests, est celle-ci :

> **Toute suite de neuf chiffres ou plus est remplacée par un marqueur avant
> écriture.**

Le seuil de neuf vient du fait que le FINESS fait neuf chiffres, et que
l'identifiant patient comme le numéro de sécurité sociale sont plus longs. En
dessous, une suite de chiffres est un compte, une année ou une taille, et reste
lisible.

---

## 7. Licence

Fichier signé, contenu lisible et signature séparés, vérifié avec une **clé
publique embarquée dans l'exécutable**, en RSA et SHA-256.

L'application ne porte que la clé publique : elle ne peut pas fabriquer de
licence.

**Vérification entièrement locale, aucun appel réseau.** Les postes du service
n'ont pas internet, et une vérification en ligne les bloquerait tous le jour où
le service de licence tomberait.

La licence est rattachée au numéro FINESS d'inscription des réglages. Elle ne
contient aucune donnée personnelle ni de santé : établissement, numéro, dates
d'émission et d'expiration.

**Trente jours de tolérance** après l'échéance, de quoi couvrir une période de
transmission entière.

Sans licence valable, **seules les écritures de fichiers sont suspendues**, avec
un refus explicite. Cinq routes sont concernées, et elles seules. La lecture, les
contrôles, les épisodes, l'identitovigilance et la revue restent disponibles :
un département d'information médicale ne doit jamais perdre la vue sur ses propres
données à cause d'une échéance commerciale.

Une licence dont la signature ne correspond pas n'est pas recopiée sur le poste.
Chaque import, accepté ou refusé, est tracé.

---

## 8. Installation et mise à jour

### 8.1 Installation

Un installateur par poste, sous le profil de l'utilisateur, **sans droits
d'administration**. La bibliothèque d'installation s'initialise avant toute
fenêtre, et le premier lancement après installation ou mise à jour est tracé au
journal.

### 8.2 Mise à jour

Par un **dossier du réseau de l'établissement**, pas par internet. Le chemin se
renseigne depuis l'écran À propos.

Tant qu'il n'est pas renseigné, Pinel ne cherche rien et le dit. Le téléchargement
ne prend que la différence avec la version installée, de l'ordre de quelques
dizaines de kilo-octets contre une centaine de méga-octets pour un paquet complet.

Quatre états sont distingués à l'écran : version portable, aucun dossier
configuré, dossier injoignable, à jour ou mise à jour disponible.

Un poste isolé continue de fonctionner avec la version qu'il a.

### 8.3 Fabrication et publication

`python tools/packager.py` produit l'installateur et les paquets dans `dist/`.
L'option de publication copie la sortie sur le partage et n'y garde que les cinq
dernières versions. Prérequis sur le poste de fabrication uniquement : l'outil
d'empaquetage, installé en outil global .NET.

### 8.4 Décision attendue

**Les droits d'écriture sur le partage de mise à jour.** Un poste va y chercher
un exécutable. Les paquets sont signés et vérifiés par la bibliothèque
d'installation, mais le contrôle d'accès au partage reste une décision de la
direction informatique.

### 8.5 Signature de code

**L'installateur n'est pas signé numériquement à ce jour.** Sans certificat,
Windows affiche un éditeur inconnu et un outil de détection sur les postes peut
bloquer l'installation. Un certificat est à fournir, et la voie la plus cohérente
est un certificat d'établissement.

---

## 9. Mode ligne de commande

L'exécutable ouvre la fenêtre sans argument, et joue l'outil demandé avec un
verbe. Il se rattache à la console de l'appelant, sans quoi une application
fenêtrée n'aurait aucune sortie standard : c'est ce qui rend ces verbes
utilisables depuis le planificateur de tâches.

### 9.1 Traitement sans fenêtre

```
Pinel.exe --headless --scan <dossier> [--validate] [--export <fichier>] [--json <rapport>]
```

Codes de sortie : **0** rien de bloquant, **1** paramètre invalide ou chemin
refusé, **2** au moins une anomalie bloquante.

Les chemins sont confinés aux dossiers autorisés. Sans option de rapport, celui-ci
est écrit sous un nom généré dans le dossier de sortie, **jamais sur la sortie
standard**.

### 9.2 État des mises à jour

```
Pinel.exe maj
```

Codes de sortie : **0** à jour, **10** une mise à jour attend.

### 9.3 Les dix verbes d'outils

| Verbe | Ce qu'il fait |
|---|---|
| `formats-importer` | Convertit un classeur officiel de formats en descriptifs |
| `roles` | Liste les champs que la pseudonymisation transforme |
| `anonymiser` | Produit une copie pseudonymisée d'une arborescence |
| `controler` | Passe les contrôles et affiche un décompte par code |
| `apprendre` | Tire les règles des paires avant et après correction |
| `regles` | Liste les règles apprises |
| `regle` | Décision du service sur une règle : valider, rejeter, proposer |
| `suggerer` | Compte les lignes concernées ; avec un dossier, écrit les copies corrigées |
| `entrainer` | Entraîne le modèle, ne remplace le précédent que s'il fait mieux |
| `scorer` | Signale les lignes que le service supprimerait probablement |

Codes de sortie : **0** succès, **1** erreur d'argument, d'accès, de format ou
d'état.

**Aucun de ces verbes n'écrit de donnée patient sur la console** : chemins, noms
de format et décomptes seulement.

Aucun script intermédiaire n'est nécessaire : l'exécutable se déclare directement
dans le planificateur, ce qui évite les fichiers de commandes que les outils de
détection des postes bloquent.

---

## 10. Prérequis

| Élément | Exigence |
|---|---|
| Système | Windows 10 ou 11, 64 bits |
| Composant | WebView2, présent d'origine sur Windows 11 et Windows 10 à jour |
| Disque | environ 250 Mo |
| Mémoire | 4 Go, davantage sur de très gros lots |
| Droits | aucun droit d'administration |
| Réseau | aucun accès internet |

---

## 11. Protocole de recette en environnement isolé

Proposé à la direction informatique, à dérouler sur un poste témoin.

1. **Empreinte.** Relever l'empreinte SHA-256 de l'installateur reçu et la
   comparer à celle annoncée.
2. **Installation.** Installer sous un compte sans privilège. Vérifier qu'aucune
   élévation n'est demandée.
3. **Premier lancement.** Vérifier l'ouverture de la fenêtre et la présence d'une
   entrée de premier lancement au journal.
4. **Connexions sortantes.** Observer le trafic. Attendu : **aucune connexion
   vers internet**. Si un dossier de mise à jour est configuré, un accès au
   partage indiqué apparaîtra, et lui seul.
5. **Port à l'écoute.** Vérifier que 8787 n'écoute que sur l'adresse de bouclage
   et n'est joignable depuis aucun autre poste.
6. **Confinement.** Tenter une lecture hors des dossiers déclarés, y compris par
   une jonction NTFS pointant ailleurs. Attendu : refus explicite.
7. **Journal.** Passer un traitement, puis vérifier qu'aucune suite de neuf
   chiffres ou plus ne subsiste dans le journal.
8. **Contenu du stockage.** Inspecter `%LOCALAPPDATA%\Pinel-DIM`, en vérifiant
   nommément la présence et les droits de `licence.lic`, `anonymisation.key`,
   `regles-apprises.json` et `modele\`.
9. **Licence.** Retirer la licence et vérifier que les contrôles fonctionnent
   toujours et que seules les écritures sont refusées.
10. **Désinstallation.** Désinstaller, puis vérifier que `Pinel-DIM` **subsiste**
    avec les données, et décider si c'est le comportement souhaité.

---

## 12. Qualité

**244 tests automatisés, tous verts.** Périmètre couvert : formats et découpage,
conformité de la matrice aux descriptifs officiels, registre par défaut, contrôles
de qualité, rédaction des anomalies et du journal, épisodes, structure, fichiers
complémentaires, confinement des chemins, mode sans fenêtre, licence,
pseudonymisation, apprentissage des règles, modèle.

La priorité a été donnée aux **erreurs silencieuses** : celles qui produisent un
fichier d'apparence correcte. Plusieurs tests existent pour rendre rouge un
comportement qui ne lèverait rien en production.

---

## 13. Limites connues

- **Mono-poste.** Aucun partage de session, aucun travail à plusieurs sur un même
  lot.
- **Installateur non signé.** Voir 8.5.
- **Le port 8787 est inscrit dans le code**, sans réglage.
- **Une conversion de page HTML en PDF existe dans le service local mais n'est
  reliée à aucun bouton.** Elle passe par le composant d'affichage, qui exécute
  les scripts de la page. À retirer ou à encadrer avant d'y exposer un bouton.
- **La reconnaissance par le contenu et la reconnaissance par le nom coexistent.**
  L'écran de conversion utilise encore la seconde. L'unification reste à faire.
- **Renommages possibles.** Les intitulés d'écrans et de fichiers produits sont
  des propositions.

---

## 14. Contact

Adam BELOUCIF
Ingénieur PMSI, Département d'Information Médicale
[adam.beloucif@psysudparis.fr](mailto:adam.beloucif@psysudparis.fr)

GH Fondation Vallée - Paul Guiraud, GHT Psy Sud Paris
