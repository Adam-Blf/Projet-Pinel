# Dossier fonctionnel

Version 1.0.0.

Ce document décrit **les règles que Pinel applique**, et pourquoi. Il s'adresse
au département d'information médicale qui doit vérifier que l'outil fait ce qu'il
annonce, et qui doit pouvoir défendre chaque chiffre produit.

Il est écrit pour être lisible sans être du métier. Les sigles sont développés à
leur première apparition, et tous figurent dans le [glossaire](06_GLOSSAIRE.md).

Le cahier des charges de référence est celui du département d'information
médicale du GHT Psy Sud Paris, daté du 22 décembre 2025. Le produit sert
aujourd'hui n'importe quel département d'information médicale, voir le
chapitre 2.2.

---

## 1. Ce qui était demandé, et où ça en est

Le cahier des charges demande quatre chantiers. S'y ajoute la reprise d'un
classeur Excel utilisé pour les transports.

| Demande | État | Chapitre |
|---|---|---|
| Convertir les fichiers du format national en tableaux exploitables, RPS et RAA en priorité | Fait | 3 |
| Reconstruire des épisodes de prise en charge en ambulatoire | Fait | 4 |
| Mettre en forme le fichier de structure du groupement, et le représenter graphiquement | Fait en partie, la représentation graphique manque | 6 |
| Pistes d'intelligence artificielle sur l'exhaustivité du recueil | **Traité autrement que demandé**, voir 9 et 10 | 9, 10 |
| Reprise du classeur Excel des transports | Fait | 7 |

### 1.1 Sur le quatrième chantier

La demande visait la fiabilisation de l'exhaustivité du recueil ambulatoire à
partir des signaux d'activité du dossier patient informatisé. **Ce chantier-là
n'est pas fait**, et il ne peut pas l'être sans un accès en lecture au système
d'information, donc sans une instruction préalable par la direction informatique.

Deux fonctions d'apprentissage ont en revanche été développées, sur un terrain
voisin et sans aucun accès au système d'information : l'apprentissage des
corrections du département (chapitre 9) et un modèle de détection des
suppressions (chapitre 10). Elles ne remplacent pas la demande d'origine. Elles
attaquent le même problème par l'autre bout : au lieu de chercher ce qui manque
dans le recueil, elles apprennent ce que le service corrige déjà.

### 1.2 Les noms ne sont pas définitifs

Le nom de l'application, les intitulés des écrans, les noms des fichiers produits
et les libellés des colonnes sont des propositions. Le département peut demander
toute autre dénomination. Les libellés de colonnes viennent des descriptifs de
format déposés par le service : ils se changent sans intervention sur
l'application.

---

## 2. Qui s'en sert, et sur quoi

### 2.1 Les utilisateurs

Techniciens d'information médicale et médecins du département, sur les postes du
service. L'application est mono-poste et mono-utilisateur : pas de compte, pas
d'annuaire, pas de serveur. Chaque session travaille sur les fichiers que la
personne lui désigne.

Rythme d'usage attendu : mensuel pour les transmissions, trimestriel pour les
campagnes, ponctuel pour les contrôles.

### 2.2 L'établissement n'est pas codé dans le logiciel

C'est un changement de nature du produit, intervenu le 23 septembre 2026, et il
faut le dire clairement.

Jusqu'à cette date, les constantes d'un établissement précis, la Fondation
Vallée, étaient inscrites dans le code et servaient de valeurs par défaut. **Un
autre établissement qui aurait utilisé Pinel aurait transmis son activité sous le
numéro FINESS de celui-là.**

Désormais, l'identité de l'établissement est un réglage : nom affiché, numéro
FINESS d'inscription e-PMSI, numéro FINESS géographique, et les constantes des
transports. Il n'y a **aucune valeur par défaut**, et Pinel refuse de produire un
fichier complémentaire de transports tant que ces constantes ne sont pas
renseignées.

C'est autant une correction de sécurité qu'une ouverture commerciale.

### 2.3 Ce à quoi Pinel ne se connecte pas

Aucun flux vers la facturation, le dossier patient, les outils de pilotage ou
l'entrepôt de requêtes. Pinel lit des fichiers déjà extraits et produit des
fichiers. Le dépôt aux autorités reste un geste manuel.

Deux chemins réseau existent néanmoins, et il faut les nommer plutôt que de
laisser croire qu'aucun n'est jamais ouvert :

- un **dossier de mise à jour** sur un partage de l'établissement, que l'on
  renseigne ou non, et que Pinel ne cherche pas tant qu'il n'est pas renseigné ;
- une **licence**, vérifiée entièrement sur le poste, sans aucun appel réseau.

Aucun des deux ne sort vers internet.

---

## 3. Convertir les fichiers du format national

### 3.1 Comment un fichier est reconnu

Deux mécanismes coexistent, et c'est volontaire.

**Par le contenu.** Pinel mesure la longueur des lignes d'un échantillon et la
compare à celle attendue de chaque format, en tenant compte des zones qui se
répètent. Si plus de quatre lignes sur cinq correspondent à un format et à un
seul, le format est établi. Le nom du fichier ne sert alors qu'à départager une
égalité.

**Par le nom.** Pinel cherche le sigle du format dans le nom du fichier.

Le premier mécanisme est le plus sûr et sert la pseudonymisation, l'apprentissage
et le modèle. Le second est celui de l'écran de conversion.

Depuis le 23 septembre 2026, **le scan retient aussi les fichiers sans
extension** et passe par le détecteur de contenu. Deux défauts mesurés sur les
fichiers réels le justifiaient : des fichiers PMSI nommés `vh` ou `VIDHOSP_PSY`
disparaissaient du scan sans le moindre message, et trois conventions de nommage
d'un site du groupement n'étaient pas reconnues, dont celle du VID-IPP, ce qui
faisait ressortir des milliers de patients comme non chaînés.

### 3.1 bis Les exports JSON de plateforme

Certaines plateformes livrent un export JSON où chaque enregistrement rassemble
un patient ou un séjour avec **toutes ses lignes ATIH** et sa clé de chaînage.
Ce n'est pas un format de plus : c'est un groupement déjà fait.

Pinel en sort les lignes telles quelles, zones répétées comprises, pour que le
reste de la chaîne les traite normalement. Les lignes qui décrivent une unité
médicale ne sont écrites qu'une fois : l'export les attache à chacun de ses
patients, ce qui produisait 21 563 lignes pour 108 unités distinctes.

### 3.2 Les formats connus

**Vingt-sept formats.** Sept d'entre eux portent un identifiant de patient en
clair, les vingt autres n'en portent aucun.

| Domaine | Formats |
|---|---|
| Psychiatrie | RPS, RAA, RPSA, R3A, VID-IPP, HOSP-FACT, FICHCOMP-ISO, FICHCOMP-TP, FICHSUP-PSY, FICUM-PSY, RSF-ACE-PSY |
| Transversal | VID-HOSP, ANO-HOSP, HOSP-PMSI, FICHCOMP |
| Soins médicaux et de réadaptation | RHS, SSRHA, RAPSS, FICHCOMP-SMR |
| Hospitalisation à domicile | RPSS, RAPSS-HAD, FICHCOMP-HAD, SSRHA-HAD |
| Médecine, chirurgie, obstétrique | RSS, RSFA, RSFB, RSFC |

Deux formats acceptent aussi leurs longueurs de transition : le RPS en 142 et 148
caractères, le RAA en 86 et 90.

**EDGAR n'est pas dans cette liste et n'y reviendra pas.** Ce n'est pas un format
de fichier mais une typologie d'actes ambulatoires - entretien, démarche, groupe,
accompagnement, réunion - codée à l'intérieur du RAA. L'avoir traité comme un
format était une erreur, et un garde-fou dans le code empêche de la refaire.

### 3.3 D'où viennent les positions des champs

Question décisive, parce qu'une position fausse produit un fichier qui a l'air
correct.

Jusqu'au 28 août 2026, les positions venaient d'un ancien script, sans date ni
source. **Elles ont depuis été comparées entrée par entrée aux classeurs
officiels publiés par l'agence technique de l'information sur l'hospitalisation**
pour l'exercice 2026, psychiatrie, médecine, soins de réadaptation,
hospitalisation à domicile, et leurs pendants anonymes.

La règle qui en découle, et qui vaut pour toute la suite : **une position qui n'a
pas pu être établie sur un descriptif officiel est déclarée absente, jamais
devinée.**

### 3.4 Les descriptifs déposés par le service

Les formats changent tous les ans. Le service dépose donc, dans un dossier prévu
pour cela, un fichier de descriptif par format et par année. Ajouter un millésime
ne demande aucune nouvelle version de l'application.

La règle de sélection se lit en trois temps :

1. le descriptif de l'année exacte du fichier, s'il existe ;
2. sinon, le descriptif sans année, s'il existe ;
3. sinon, rien. **Jamais un descriptif d'une année postérieure**, qui découperait
   un fichier ancien avec des positions qu'il ne connaît pas.

La forme du fichier et la marche à suivre sont dans le
[guide utilisateur, chapitre 4.3](01_GUIDE_UTILISATEUR.md). Un raccourci existe :
la commande `pinel formats-importer` convertit un classeur officiel de l'agence
en descriptifs, sans aucune recopie manuelle.

### 3.5 Ce que produit la conversion

Un fichier par fichier source, à séparateur point-virgule, en UTF-8 avec la
marque de trois octets en tête qui permet à Excel de lire les accents sans
assistant d'importation.

Trois colonnes de traçabilité ouvrent chaque ligne : le fichier source, le numéro
de ligne et le format reconnu.

Les valeurs commençant par un signe qu'Excel interpréterait comme une formule
sont neutralisées à l'écriture.

### 3.6 Ce qui a été corrigé, et qu'il faut savoir

Deux défauts étaient annoncés dans la version précédente de ce document. Ils sont
corrigés. Les mentionner sert à comprendre ce qui a changé dans les fichiers
produits.

**Les formats anonymes déclaraient des positions d'identifiant.** Les formats
RPSA et R3A sont anonymes par construction : le descriptif officiel n'y déclare
qu'un cryptage irréversible de l'identifiant, et aucune date de naissance.
Pourtant, Pinel leur attribuait des positions d'identifiant et de date de
naissance venues de l'ancien script, et sortait deux colonnes qui n'avaient aucun
contenu réel.

Ces vingt formats sans identifiant ne reçoivent plus **aucune** position par
défaut. Sans descriptif déposé, la conversion sort la ligne brute et le signale,
plutôt que d'inventer un découpage.

**La date de naissance était contrôlée à l'envers.** Le contrôle attendait la
forme AAAAMMJJ et signalait comme fautive chaque ligne écrite en JJMMAAAA. Or
tous les descriptifs 2026 qui portent une date de naissance la déclarent en
**JJMMAAAA**. Le contrôle signalait donc comme erreur la totalité d'un lot
pourtant accepté par la plateforme de dépôt. C'est désormais l'inversion qui est
repérée, et en avertissement.

---

## 4. Les épisodes de prise en charge

### 4.1 Le besoin

Dans le format national, un acte ambulatoire n'est rattaché ni à un séjour ni à
un dossier : c'est une ligne isolée. Impossible de compter des parcours, de
mesurer une durée de suivi ou de repérer une rupture.

L'épisode est la maille manquante.

### 4.2 La règle appliquée

Un épisode regroupe les journées consécutives d'un même patient, dans la même
unité médicale et sur le même site. Il se ferme dès qu'une journée est sautée, et
dans tous les cas au 31 décembre puisque le recueil est annuel.

Quatre paramètres, et la règle appliquée est écrite dans le fichier produit :

| Paramètre | Défaut | Effet |
|---|---|---|
| Tolérance en jours sans venue | 0 | Nombre de jours sans venue qui ne rompent pas l'épisode |
| Rupture au changement d'unité médicale | oui | Un transfert ouvre un nouvel épisode |
| Rupture au changement de site | oui | Idem entre deux sites |
| Fermeture au 31 décembre | oui | Aucun épisode ne chevauche deux exercices |

> **La tolérance à zéro est un choix de service, pas une valeur technique.** À
> zéro, un suivi hebdomadaire en centre médico-psychologique produit un épisode
> par consultation, tous de durée nulle, ce qui ne décrit aucun parcours. Cette
> valeur est à trancher par le département.

### 4.3 L'identifiant d'épisode

Construit sur le patient, le site, l'unité médicale et la date de début. Il est
déterministe : deux calculs sur le même lot donnent les mêmes identifiants, ce
qui permet de comparer deux exports.

Les caractères non alphanumériques y sont **remplacés et non supprimés**, sans
quoi deux valeurs différentes pourraient produire le même identifiant.

### 4.4 Les dates

Deux conventions coexistent dans les formats nationaux, et elles ne sont pas
interchangeables :

| Où | Convention |
|---|---|
| Dates de naissance des descriptifs 2026 | JJMMAAAA |
| Dates d'activité | AAAAMMJJ |

Plage acceptée : 1990 à 2100 pour l'activité.

Ce point mérite l'attention parce qu'une convention prise pour l'autre rend
invalide un fichier entier sans qu'aucune valeur ne paraisse aberrante.

### 4.5 Ce qu'il faut pour calculer

Un descriptif déclarant au minimum un identifiant patient et une date. S'il
manque, Pinel **liste les fichiers écartés** plutôt que de produire des épisodes
faux.

---

## 5. Identitovigilance

Quand un même identifiant de patient porte deux dates de naissance selon les
fichiers, le chaînage casse et la personne est comptée deux fois.

Pinel conserve **toutes** les dates observées par identifiant, avec leur nombre
d'occurrences et les fichiers d'origine. Il signale les conflits et propose la
date la plus fréquente comme date de référence. Le service peut en choisir une
autre.

Deux exports en découlent : le constat, sous forme de tableau, et une copie du
fichier où la date retenue remplace les valeurs divergentes, à largeur fixe
préservée.

> Un arbitrage automatique reste une hypothèse. Sur un patient suivi de longue
> date, la vérification dans le dossier patient reste nécessaire.

Les arbitrages vivent en mémoire de session. Ils ne sont écrits qu'au moment d'un
export.

---

## 6. Structure du groupement

Lecture d'un fichier en CSV ou en TSV, avec reconnaissance des colonnes même si
leurs intitulés varient. Pinel reconstruit l'arbre des rattachements et déduit le
type de chaque secteur d'après son code : G pour adulte, I pour infanto-juvénile,
D pour unité pour malades difficiles, P pour unité hospitalière spécialement
aménagée, Z pour intersectoriel.

Export à plat de la structure relevée.

**Deux limites** : la représentation graphique de l'arbre n'est pas produite, et
la reconnaissance des colonnes reste faillible sur un fichier dont les intitulés
s'écartent fortement des usages.

---

## 7. Transports et fichiers complémentaires

### 7.1 Nettoyage d'un classeur de transports

Les exports de la chaîne de facturation répètent le bloc d'en-tête toutes les
quelques lignes et laissent la date de commande vide sur les lignes de
continuation.

Pinel produit une copie nettoyée : blocs répétés retirés, date propagée, et une
seconde feuille qui conserve l'original avec les lignes retirées surlignées.
**Le fichier d'origine n'est jamais modifié.**

### 7.2 Contrôle d'un fichier complémentaire

Deux gabarits, selon qu'il s'agit de médicaments ou de dispositifs médicaux
implantables. Longueur de ligne, numéro d'établissement, code, quantité et date
sont vérifiés, et les anomalies listées avec leur numéro de ligne.

### 7.3 Les trois défauts du classeur d'origine, corrigés volontairement

Le classeur Excel repris portait trois défauts dans la colonne qui construit la
ligne transmise. La décision a été prise de les corriger et de documenter l'écart.

1. **Décalage de 196 lignes.** Chaque ligne était composée avec les données d'un
   autre patient. Au-delà d'un certain rang, les références pointaient dans le
   vide.
2. **Date lue dans une colonne vide.** La formule prenait la date dans une
   colonne étiquetée « identifiant à ajouter » et jamais remplie. La date de
   transport est ailleurs.
3. **Libellé de largeur variable au milieu d'un format à largeur fixe**, ce qui
   décalait tout ce qui suivait.

**Conséquence à annoncer** : les lignes produites par Pinel ne sont pas
identiques à celles du classeur actuel. C'est voulu.

---

## 8. Les contrôles de qualité

Sept contrôles. Six regardent un fichier à la fois, le septième croise les
fichiers entre eux.

### 8.1 Les six contrôles par fichier

| Contrôle | Ce qu'il détecte |
|---|---|
| Numéro d'établissement | Un FINESS qui n'est pas neuf chiffres à sa position attendue |
| Date de naissance | Date non numérique, année hors plage, jour ou mois inexistant, et date écrite à l'envers |
| Fichiers anonymes | Un identifiant en clair resté dans un fichier qui ne devrait en porter aucun |
| NIR de chaînage | Numéro absent, mal formé, ou dont la clé de contrôle ne tombe pas juste |
| Lignes en double | Lignes strictement identiques, et volume anormal |
| Cohérence de l'année | Année future dans le nom, dates de naissance postérieures à l'année déclarée |

### 8.2 Le contrôle croisé

Il vérifie que chaque patient présent dans l'activité est bien chaîné, et
signale dans les deux sens : les patients sans chaînage, et les chaînages que
plus aucune activité ne référence.

**La référence est la réunion du VID-HOSP et du VID-IPP.** Jusqu'au 22 septembre
2026, seul le VID-HOSP était consulté. Or les patients vus en ambulatoire sont
chaînés par le VID-IPP : le contrôle levait 162 454 erreurs sur les RAA d'un lot
pourtant accepté, une par ligne. Un patient manquant ne produit désormais qu'une
seule anomalie, à sa première ligne.

### 8.3 Deux exceptions mesurées, et leur motif

**Les doublons de RAA ne sont pas une erreur.** Une ligne de RAA ne porte ni
heure ni identifiant d'acte : deux entretiens réellement distincts le même jour
produisent deux lignes strictement identiques. Sur un lot réel accepté par la
plateforme, 13 % des lignes de RAA étaient dans ce cas. Le volume est donc
signalé en avertissement pour le RAA, et reste une erreur pour tout autre format.

**Le contrôle d'anonymisation ne porte que sur RPSA et R3A.** Les quatre autres
formats anonymes ne portent aucun champ patient dans les descriptifs 2026 : le
contrôle n'aurait rien à regarder et ne produirait que du bruit.

### 8.4 Les niveaux de gravité

| Niveau | Ce que ça veut dire |
|---|---|
| Blocker | Ne pas transmettre en l'état |
| Error | Faute à corriger |
| Warning | Doute à lever |
| Info | Remarque |

**Aucun message d'anomalie ne reproduit une donnée de patient.** Les messages
portent la position du champ et la nature de l'écart, jamais la valeur lue. Une
garde le vérifie mécaniquement.

---

## 9. L'apprentissage des corrections du service

Ce chapitre décrit une fonction qui n'existait pas dans la version précédente du
produit. Elle mérite d'être comprise avant d'être utilisée.

### 9.1 L'observation de départ

Chaque mois, le service corrige à la main les mêmes choses dans les mêmes
fichiers. Ces corrections ne sont écrites nulle part : elles vivent dans la
mémoire des personnes.

### 9.2 Ce que Pinel en fait

Il retrouve dans l'arborescence de travail les paires de fichiers avant et après
correction, d'après les conventions de nommage effectivement relevées sur les
lots réels : suffixes de correction, sous-dossier d'archivage des originaux,
essais successifs d'un même mois.

Il aligne les lignes et en tire des énoncés de la forme : *dans le format F, le
champ C passe de la valeur A à la valeur B*, éventuellement sous condition
*quand le champ K vaut V*.

### 9.3 Les garde-fous, et leur motif

| Garde-fou | Motif |
|---|---|
| Seuls les champs de **code** sont appris, jamais un identifiant ni une date | Le fichier des règles peut être relu, discuté et versionné sans porter de donnée de patient |
| Une ligne dont plus d'un quart des champs change d'un coup est écartée | Ce n'est pas une correction champ par champ mais une remise en forme. Un décalage d'un seul caractère produirait autant de fausses règles que la ligne compte de champs |
| Une paire de fichiers n'est comptée qu'une fois, par empreinte de contenu | Une même correction recopiée dans trois dossiers ne doit pas compter trois fois |
| Seuil de candidature : 2 cas et 95 % de confiance. Seuil d'établissement : 5 cas cumulés | Une correction vue deux fois peut être une coïncidence, il faut qu'elle se répète |
| Une correction appliquée au cas par cas n'est pas généralisée : elle est **listée à part** | Le service a ses raisons de traiter certains dossiers autrement. Forcer un motif là où il n'y en a pas produirait une règle fausse |
| Le statut décidé par le service n'est jamais modifié par un nouvel apprentissage | Une décision humaine ne se défait pas toute seule |
| Chaque apprentissage réévalue les règles connues | Une correction que le service cesse d'appliquer voit sa confiance baisser |

### 9.4 Ce qui est appliqué, et ce qui ne l'est jamais

C'est le point à retenir de tout ce chapitre.

**Aucune règle ne modifie un fichier transmis aux autorités.** Ni
automatiquement, ni après validation, ni depuis l'écran de revue.

Une règle validée entre dans le **décompte des suggestions** : le service voit
combien de lignes elle toucherait. Écrire une copie corrigée demande une commande
explicite, avec un dossier de destination explicite, et produit une **copie**. Le
fichier d'origine n'est jamais touché, et la copie conserve la largeur de chaque
champ.

Trois statuts : proposée, validée, rejetée.

### 9.5 Mesure du 23 septembre 2026

Sur les lots réels 2026 : **62 règles** trouvées, toutes au-dessus de 90 % de
confiance, 28 autres seulement proposées. Les deux plus massives concernent le
VID-HOSP, à 100 % de confiance sur 959 et 970 cas.

Une de ces règles révèle un problème en amont plutôt que dans les fichiers : le
séjour facturable sort à 1 du logiciel de production, et le service le repasse à
0 tous les mois. C'est un paramétrage à corriger à la source, pas une règle à
appliquer.

---

## 10. Le modèle de détection des suppressions

### 10.1 Ce qu'il fait

Il apprend, à partir des lots déjà corrigés, quelles lignes de RAA le service a
l'habitude de **supprimer**, et signale les lignes ressemblantes sur un lot neuf.

Technique : arbres de décision boostés, exécutés sur le poste, sans Python ni
accès réseau.

### 10.2 Ce qu'il regarde, et ce qu'il ne regarde pas

Des codes et des décomptes seulement : forme d'activité, unité médicale, secteur,
mode légal, nature, lieu, modalité, chapitre du diagnostic principal, plus l'âge,
le jour de la semaine, le nombre d'intervenants et des rangs de répétition.

**Jamais un identifiant, jamais une date.** L'identifiant et la date de l'acte
servent à calculer les variables de répétition, puis ne sont pas conservés.

### 10.3 Comment il est mesuré

Validation dans le temps : le dernier mois corrigé est mis de côté et le modèle
apprend sur les précédents. C'est la seule mesure honnête de ce qu'il vaudra le
mois suivant.

Un modèle fraîchement entraîné **ne remplace celui en place que s'il fait au
moins aussi bien** sur ce même mois de contrôle. Un réentraînement ne peut pas
dégrader silencieusement les suggestions.

Il exige au moins deux mois corrigés, un pour apprendre, un pour contrôler.

### 10.4 Mesure du 23 septembre 2026

Apprentissage sur trois mois, contrôle sur un mois jamais vu : les 500 lignes
jugées les plus suspectes étaient toutes de vraies suppressions, pour un taux de
base de cinq pour mille. Sur un second mois jamais vu, 692 lignes signalées sur
150 687, toutes des répétitions du même jour dans la même unité.

### 10.5 Ce qui est appliqué automatiquement

**Rien.** Le modèle signale et ne corrige jamais. Sa sortie est une liste de
numéros de ligne et de probabilités, sans aucune valeur de champ.

Sans modèle entraîné sur le poste, l'écran le dit et la fonction reste vide.

---

## 11. La pseudonymisation

Fonction destinée au travail sur les données hors du contexte de production :
mise au point, démonstration, recherche.

Elle produit une copie d'une arborescence où les données identifiantes sont
remplacées par des codes stables. Le chaînage est préservé d'un fichier à
l'autre : le même patient porte le même code partout.

Quatre garanties, détaillées dans le
[dossier sécurité](04_SECURITE_ET_CONFORMITE.md) :

- **liste blanche au caractère près** : ce qui n'est pas couvert par un champ
  connu reste masqué, y compris un champ ajouté par un éditeur ;
- **classement par libellé officiel** et non par position, avec la règle qu'un
  libellé ambigu penche toujours vers le traitement le plus protecteur ;
- **contrôle de fuite final** : toute valeur d'identifiant vue en entrée est
  recherchée dans les champs recopiés tels quels ;
- **refus d'écrire dans un dossier synchronisé vers un nuage grand public.**

> **Une copie pseudonymisée reste une donnée personnelle** au sens du règlement
> général sur la protection des données. Ce n'est pas de l'anonymisation.

---

## 12. Ce qui reste hors périmètre

- La fiabilisation de l'exhaustivité du recueil ambulatoire à partir des signaux
  du dossier patient, voir 1.1.
- Le gabarit d'import vers les outils de pilotage.
- La représentation graphique de la structure du groupement.
- Deux contrôles de conformité à la plateforme nationale de dépôt.

---

## 13. Contact

Adam BELOUCIF
Ingénieur PMSI, Département d'Information Médicale
[adam.beloucif@psysudparis.fr](mailto:adam.beloucif@psysudparis.fr)

GH Fondation Vallée - Paul Guiraud, GHT Psy Sud Paris
