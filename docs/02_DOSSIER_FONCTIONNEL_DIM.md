# Pinel - Dossier fonctionnel

Version 1.0.0. Destinataire : Département d'Information Médicale du GHT Psy Sud
Paris. Objet : vérifier que l'application répond au cahier des charges du
22 décembre 2025, et documenter les règles de gestion appliquées.

---

## 1. Périmètre

Le cahier des charges du DIM demande quatre chantiers. S'y ajoute la reprise de
la moulinette Excel utilisée pour les fichiers complémentaires transports.

| Demande | État | Où c'est décrit |
|---|---|---|
| Moulinettes à format, RPS et RAA en priorité, sortie CSV à séparateur point-virgule | Traité | Chapitre 3 |
| Épisodes de prise en charge ambulatoire, par patient, date, UM et site | Traité | Chapitre 4 |
| Fichier de structure du GHT mis en forme et représentation graphique | Traité en partie | Chapitre 6 |
| Pistes d'intelligence artificielle sur l'exhaustivité du recueil ambulatoire | Non traité | Chapitre 9 |
| Reprise de la moulinette Excel FICHCOMP transports | Traité | Chapitre 7 |

Pinel ne se connecte à aucun système du système d'information, ni CPage, ni
DxCare, ni PMSI-Pilot, ni BIQuery. Il lit des fichiers déjà extraits et produit
des fichiers. Le dépôt aux tutelles reste manuel.

### 1.1 Les noms ne sont pas définitifs

Le nom de l'application, les intitulés des écrans, les noms des fichiers
produits et les libellés des colonnes des exports sont des propositions. Le DIM
peut demander toute autre dénomination. Les libellés de colonnes des exports
viennent des descriptifs de format déposés par le DIM, ils se changent sans
intervention sur l'application.

---

## 2. Utilisateurs et usage attendu

Techniciens d'information médicale et médecins du DIM, sur les postes du
département. L'application est mono-poste et mono-utilisateur : pas de compte,
pas d'annuaire, pas de serveur. Chaque session travaille sur les fichiers que
l'utilisateur a rendus accessibles, et rien d'autre.

Rythme d'usage attendu : mensuel pour la conversion et les contrôles avant
transmission, trimestriel pour la structure, ponctuel pour les épisodes et les
fichiers complémentaires.

---

## 3. Conversion des formats ATIH

### 3.1 Reconnaissance des fichiers

Les fichiers sont reconnus par leur nom, puis rattachés à un des 23 formats
connus. La matrice porte, pour chaque format, sa longueur de ligne canonique,
son domaine et son année d'apparition.

| Domaine | Formats |
|---|---|
| Psychiatrie | RPS, RAA, RPSA, R3A, EDGAR, FICUM-PSY, RSF-ACE-PSY |
| SSR et SMR | RHS, SSRHA, RAPSS, FICHCOMP-SMR |
| HAD | RPSS, RAPSS-HAD, FICHCOMP-HAD, SSRHA-HAD |
| Transversal | VID-HOSP, ANO-HOSP, FICHCOMP |
| MCO | RSS, RSFA, RSFB, RSFC |

Le format FICHSUP-PSY a été supprimé au 1er janvier 2021 et remplacé par FICHCOMP.
Pinel reconnaît toujours ce format pour les données antérieures.

Le format RSF-ACE du cahier des charges est traité sous deux entrées, RSF-ACE-PSY
pour l'activité externe psychiatrique et RSFA pour l'activité externe MCO. Le
DIM confirmera laquelle est attendue en routine.

Deux formats cités au cahier des charges ne sont pas encore reconnus par leur
nom de fichier : VID-IPP, et FichComp temps partiel. FichComp transports et les
autres fichiers complémentaires sont traités par l'entrée générique FICHCOMP.

### 3.2 Les descriptifs de format, réponse au changement annuel

Le cahier des charges souligne que les formats changent chaque année. Pinel
embarque les positions de tous les formats reconnus, issues d'un ancien processus
d'extraction. Le DIM peut déposer des descriptifs pour une année donnée, recopiés
des descriptifs officiels de l'ATIH. Ces descriptifs remplacent les positions par
défaut.

Un descriptif est un fichier texte, une ligne par champ :

```
# format: RPS
# annee: 2025
nom;debut;longueur;libelle
FINESS;1;9;FINESS de l'etablissement
IPP;22;20;Identifiant permanent du patient
DATE_ACTE;42;8;Date de l'acte
```

Règle de sélection, dans cet ordre :

1. le descriptif de l'année exacte du fichier, déduite de son nom ;
2. à défaut, le descriptif antérieur le plus récent ;
3. à défaut, un descriptif sans année s'il en existe un.

Un descriptif postérieur à l'année du fichier n'est jamais appliqué : les
positions ayant pu changer entre-temps, le découpage serait faux sans le dire.
Dans ce cas, le fichier est exporté avec sa ligne brute dans une colonne unique,
et le signale à l'utilisateur.

### 3.3 Ce que produit la conversion

Un CSV par fichier source, séparateur point-virgule, encodage UTF-8 avec
indicateur d'ordre des octets. Trois colonnes de traçabilité en tête, fichier
source, numéro de ligne et format, puis une colonne par champ décrit.

Les valeurs commençant par un caractère qu'Excel interpréterait comme une
formule sont neutralisées. Si un fichier de sortie de même nom existe déjà, il
sera écrasé. Le DIM doit gérer l'organisation de ses dossiers de sortie et
conserver ses originaux en lieu sûr.

### 3.4 Limites connues sur les formats

**Formats anonymes déclarés avec des positions d'identifiant.** Pinel déclare des
positions d'IPP (identifiant permanent du patient) et de date de naissance pour
les formats RPSA et R3A. Or, ces formats sont anonymes par construction : ils ne
contiennent ni IPP ni date de naissance en clair. Ces champs sont remplacés par
une anonymisation irréversible selon l'arrêté du 23 décembre 2016. Déposer un
descriptif qui prétend extraire un IPP d'un fichier RPSA produira une colonne
vide ou aberrante. C'est un défaut connu : les positions par défaut de ces deux
formats ne correspondent pas à leur contenu réel.

**Identification du format par le nom du fichier.** Aujourd'hui, Pinel reconnaît
le format en cherchant le sigle dans le nom du fichier. Le type d'enregistrement
devrait se lire sur chaque ligne du fichier lui-même, pas sur son nom global.
Cette lecture par ligne est en cours de correction et lèvera cette limite.

---

## 4. Épisodes de prise en charge ambulatoire

### 4.1 Le besoin

Dans le format national, l'activité ambulatoire de psychiatrie n'est rattachée
ni à un séjour ni à un dossier. Le cahier des charges demande une maille
intermédiaire, l'épisode, pour mesurer par exemple une durée de présence au SAU,
qui doit valoir 0 ou 1 jour, au-delà de quoi le DIM considère qu'il y a un
problème d'aval ou d'organisation.

### 4.2 La règle appliquée

Un épisode regroupe les journées consécutives d'un même patient, dans une même
unité médicale et un même site.

| Paramètre | Valeur par défaut | Effet |
|---|---|---|
| Tolérance | 0 jour | Une journée sautée ferme l'épisode |
| Changement d'unité médicale | Rupture | Un nouvel épisode commence |
| Changement de site | Rupture | Un nouvel épisode commence |
| Fin d'année civile | Fermeture | Un épisode ne franchit jamais le 31 décembre |

La fermeture annuelle est la règle retenue par le DIM : le recueil étant annuel,
un épisode à cheval sur deux exercices n'aurait pas de sens. Les quatre
paramètres sont modifiables à l'écran, et la règle effectivement appliquée est
écrite dans le fichier produit.

Seuls les formats de recueil ambulatoire, RAA et R3A, entrent dans le calcul.
Les formats d'hospitalisation complète en sont exclus : une durée de présence
calculée sur un séjour complet ne voudrait rien dire.

### 4.3 L'identifiant d'épisode

Construit sur le patient, le site, l'unité médicale et la date de début, par
exemple `123-FV94-UM01-20250310`. Deux calculs sur le même lot produisent les
mêmes identifiants. Les caractères non alphanumériques des codes sont remplacés
et non supprimés, pour que deux codes distincts comme UM-01 et UM01 ne se
confondent jamais.

### 4.4 Les dates

Les deux conventions du recueil sont acceptées, AAAAMMJJ pour l'activité et
JJMMAAAA pour les fichiers complémentaires. Une date dont l'année sort de la
plage 1990 à 2100 est rejetée, même si elle s'analyse techniquement : la ligne
est écartée plutôt que de fabriquer un épisode faux.

---

## 5. Identitovigilance

Pour chaque identifiant patient, l'application conserve toutes les dates de
naissance rencontrées et les fichiers où elles apparaissent. Un identifiant
portant plus d'une date distincte est signalé comme conflit.

La date retenue par défaut, dite pivot, est la plus fréquente. Le technicien
peut en choisir une autre. L'arbitrage vit en mémoire de session : il n'est
inscrit sur disque qu'au moment d'un export.

Deux sorties en découlent : un export CSV des conflits, et un export assaini,
copie du fichier ATIH dans laquelle la date pivot remplace les valeurs
divergentes, à largeur fixe préservée. Le fichier d'origine n'est jamais
modifié.

---

## 6. Structure du GHT

Le fichier de structure est lu en CSV ou en TSV. Les colonnes sont reconnues
même si les intitulés varient, l'arbre des rattachements est reconstruit, et le
type de secteur ARS est déduit du code : G pour adulte, I pour infanto-juvénile,
D pour UMD, P pour UHSA, Z pour intersectoriel. Le type se propage aux niveaux
inférieurs quand un niveau ne le porte pas.

L'écran affiche le nombre d'unités, le nombre de racines, la profondeur de
l'arbre et la répartition par type de secteur.

Un export à plat produit un CSV, une ligne par unité, avec le code, le libellé,
le niveau, le parent, la profondeur, le type de secteur et le chemin
hiérarchique complet.

Deux limites à connaître. Le gabarit d'import attendu par PMSI-Pilot n'a pas été
communiqué : l'export actuel est un export générique à plat, les colonnes
seront renommées ou réordonnées dès que le gabarit sera fourni. La
représentation graphique demandée pour le groupe FICOM n'est pas encore
produite.

---

## 7. Fichiers complémentaires

Reprise de la moulinette Excel du DIM.

**Nettoyage d'un classeur transports.** Les blocs d'en-tête répétés sont
retirés, la date de commande est propagée sur les lignes qui la laissent vide.
Le classeur produit contient deux feuilles, la version nettoyée et l'original
avec les lignes retirées surlignées. Le fichier d'origine n'est pas modifié. Si
aucune feuille du classeur ne porte le bloc d'en-tête attendu, l'application
refuse de traiter plutôt que de produire un classeur vide d'apparence correcte.

**Contrôle d'un fichier complémentaire.** Deux gabarits sont connus :

| Type | Longueur | Quantité |
|---|---|---|
| Médicaments, code UCD | 53 caractères | 7 caractères, valeur au millième |
| Dispositifs médicaux implantables, code LPP | 50 caractères | 4 caractères, valeur entière |

Sont contrôlés la longueur de ligne, le FINESS, le code, la quantité et la date
au format JJMMAAAA. Une valeur qui ne tient pas dans la largeur du format
provoque un refus explicite : ni troncature, ni saturation. Deux numéros de
séjour différents ne doivent jamais être écrits à l'identique dans un fichier
transmis.

---

## 8. Contrôles qualité

Six contrôles fichier par fichier, un contrôle croisé entre fichiers.

| Contrôle | Ce qu'il détecte |
|---|---|
| FINESS | Code absent, non numérique, longueur inattendue |
| Date de naissance | Date impossible, ou saisie en JJMMAAAA là où AAAAMMJJ est attendu |
| Anonymisation | Identifiant en clair resté dans un fichier censé être anonyme |
| NIR de chaînage | NIR absent ou non numérique dans le VID-HOSP |
| Doublons | Lignes strictement identiques dans un même fichier |
| Année du fichier | Dates incohérentes avec l'année portée par le nom de fichier |
| Chaînage, croisé | Identifiant présent dans l'activité et absent du VID-HOSP |

Chaque anomalie porte un niveau : Blocker, Error, Warning, Info. Un Blocker
signifie qu'il ne faut pas transmettre en l'état.

Les contrôles de conformité DRUIDES au sens large, hors périmètre du cahier des
charges, ne font pas partie de cette version.

---

## 9. Hors périmètre de cette version

Le quatrième chantier du cahier des charges, fiabiliser l'exhaustivité du
recueil ambulatoire à partir des signaux d'activité de DxCare et DxPlanning,
n'est pas développé. Il suppose un accès en lecture au système d'information,
donc une instruction préalable par la direction des ressources numériques. La
notion d'épisode du chapitre 4 en constitue le socle : elle fournit la maille à
laquelle l'exhaustivité pourra être mesurée.

Restent également à traiter : le gabarit d'import PMSI-Pilot, la représentation
graphique de la structure, les formats VID-IPP et FichComp temps partiel.

---

## 10. Contact

Adam BELOUCIF
Apprenti ingénieur PMSI, Département d'Information Médicale
Téléphone 01 42 11 70 60, courriel adam.beloucif@psysudparis.fr
