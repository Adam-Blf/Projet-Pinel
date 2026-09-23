# CHANGELOG

Tous les changements notables pour Pinel sont documentés dans ce fichier, du plus récent au plus ancien.

Le format s'inspire de [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/), et le versioning suit [Semantic Versioning](https://semver.org/lang/fr/).

Règle de tenue de ce fichier : n'y figure que ce qui est livré et prouvé par un build et des
tests verts. Les chantiers en cours vivent dans `tasks/todo.md`, pas ici. Un journal qui
annonce ce qui n'existe pas encore ne sert plus à personne.

---

## Non publié

### Ajouté (fichiers réels 2026)

- `pinel formats-importer` : conversion des classeurs officiels de formats ATIH
  (PSY, MCO) en descriptifs Pinel. Descriptifs 2026 versionnés dans
  `reference/formats/2026`.
- Reconnaissance d'un fichier par la longueur de ses lignes, rapportée à ses
  compteurs de zones répétées, en plus du nom. Noms des exports Druides reconnus :
  `vh_psy`, `vipp`, `ipp_`, `fc_ic`, `iso_`, `fc_htpart`, `tp_`, `*HOSP_PMSI*`.
- Formats VID-IPP, HOSP-PMSI, HOSP-FACT, FICHCOMP isolement et FICHCOMP temps
  partiel ajoutés au référentiel.
- `pinel anonymiser` : copie pseudonymisée d'un lot PMSI, sous clé DPAPI propre
  au poste, liste blanche au caractère, contrôle de fuite. Refuse d'écrire dans un
  dossier synchronisé vers un nuage (Google Drive, OneDrive, Dropbox, iCloud).
- `pinel roles` : liste des champs transformés par la pseudonymisation.

### Ajouté (modèle local)

- Projet `src/Pinel.Ml` : modèle de détection des lignes RAA que le DIM supprime
  (LightGBM via ML.NET, exécuté sur le poste, sans Python ni accès réseau).
  Variables tirées des libellés du descriptif officiel, jamais d'identifiant.
  Validation temporelle sur le mois corrigé le plus récent, jamais vu à
  l'apprentissage ; un nouveau modèle ne remplace le précédent que s'il fait au
  moins aussi bien sur ce même mois, et chaque modèle garde sa fiche.
  Mesure du 23/09/2026 : apprentissage M1 à M3, contrôle M6, AUPRC 0,998 pour un
  taux de base de 0,005, et 100 % de vraies suppressions sur les 500 lignes les
  plus suspectes. Sur M7, jamais vu : 692 lignes signalées sur 150 687, toutes
  des répétitions du même jour, profil identique aux suppressions des mois
  appris.
- `pinel entrainer` et `pinel scorer`.

### Corrigé (apprentissage des corrections)

- L'induction se fait maintenant paire par paire ET sur l'ensemble des paires
  d'un format. Paire par paire seule, une correction qui ne touche qu'une ligne
  par fichier (retyper une unité médicale) n'était jamais apprise ; sur
  l'ensemble seul, les essais déjà corrigés d'un même mois comptaient comme des
  contre-exemples et effaçaient les corrections massives du VID-HOSP.
- Une ligne dont plus du quart des champs changent d'un coup est écartée de
  l'apprentissage : c'est une ligne remise en forme, et elle produisait autant
  de fausses règles qu'elle compte de champs (27 relevées sur un VID-HOSP
  décalé d'un caractère).
- Les corrections vues mais non généralisables sont listées séparément, avec le
  nombre de fois où le DIM les a appliquées et celui où il ne les a pas
  appliquées. C'est le cas du type d'unité laissé à 000, renseigné pour quelques
  unités par mois et laissé tel quel pour 160 autres : aucune règle ne le décrit.

### Corrigé (fichiers réels 2026)

- VID-HOSP n'est pas à longueur fixe : 470 caractères plus 50 par discipline de
  prestation (compteur en 467-470). Les lignes de 470, 570 et 620 caractères sont
  conformes.
- `PSY_RAA_HOSP_PMSI_*.txt` était lu comme un RAA.
- Contrôles passés sur un lot réel accepté par e-PMSI (M4, envoi 3) : 162 878
  anomalies avant correction, 90 après.
  - Chaînage : un RAA se chaîne par le VID-IPP, pas seulement par le VID-HOSP.
    Un patient manquant ne produit plus qu'une anomalie, au lieu d'une par ligne.
  - Date de naissance : le format officiel est JJMMAAAA (tous les descriptifs 2026),
    le contrôle tenait l'inverse. L'inversion AAAAMMJJ reste signalée.
  - FINESS : lu à sa position officielle par format (53 sur VID-HOSP et VID-IPP,
    qui commencent par le NIR).
  - Doublons : sur un RAA, des lignes identiques sont des actes répétés le même jour,
    signalés en avertissement. Code `ERR-DOUBLON-BULK` pour les autres formats.
- `pinel controler <dossier>` : décompte des anomalies par code, sans valeur de champ.
- Apprentissage des corrections du DIM (`pinel apprendre`, `regles`, `regle`,
  `suggerer`). Pinel retrouve les paires origine / corrigé de l'arborescence de
  travail, aligne les lignes et en tire des règles « champ A → B, quand K = V ».
  La mémoire des règles cumule les observations d'un mois sur l'autre ; une règle
  que le DIM cesse d'appliquer perd sa confiance ; une correction recopiée dans
  plusieurs envois ne compte qu'une fois. Seules les règles validées par le DIM
  corrigent une copie. Premier apprentissage sur les lots 2026 : 40 règles, dont
  VID-HOSP facturable 1 → 0 et motif → 9 (959 cas, 100 %), forme d'activité
  31 → 33 par UM, nombre d'intervenants plafonné selon la nature de l'acte.

### Modifié

- Icônes : le jeu Icons8 en PNG est remplacé par Phosphor Icons (licence MIT, graisse
  regular), servi en SVG local, net à toute densité d'écran. Plus d'obligation de lien
  d'attribution, le texte de la licence MIT est consultable dans « À propos ».
- Polices : seuls les sous-ensembles latin et latin-ext de Montserrat sont embarqués
  (cyrillique et vietnamien retirés, 63 Ko de moins). Chemins, journal et identifiants
  passent en Consolas, police système à chasse fixe.
- `tools/vendor_assets.py` rapatrie aussi les icônes, à version figée.

### Ajouté

- Bascule de thème clair ou sombre en pied de menu. Sans choix enregistré, Pinel suit le
  réglage de Windows, y compris s'il change en cours de session.
- État occupé sur tout bouton qui appelle le service local : bouton désactivé, icône
  tournante, `aria-busy`. Un double clic sur « Traiter » ne lance plus deux lots.
- Notifications d'erreur distinguées des confirmations (couleur, icône, durée d'affichage
  allongée à 9 secondes).
- En-têtes de tableau collants et hauteur bornée : les 200 anomalies d'un lot restent
  lisibles avec leurs intitulés de colonne. Mention explicite quand la liste est tronquée.
- Production de la feuille FICHCOMP transports. Le classeur nettoyé porte désormais une
  troisième feuille au format attendu par le DIM, ses 19 colonnes renseignées depuis le
  rapport nettoyé. Auparavant, la sortie avait la structure d'une facture (désignation, unité,
  prix unitaire, TVA) et n'avait aucun rapport avec le FICHCOMP transports.
- Les cinq constantes d'établissement (FINESS e-PMSI, FINESS géographique, type de prestation,
  code forfait, classe de distance) sont paramétrables. Un autre établissement du GHT n'a plus
  besoin d'une version différente du logiciel.
- Signalement explicite de tout dépassement de largeur dans la ligne FICHCOMP. Une valeur trop
  longue est remontée comme anomalie au lieu d'être tronquée en silence.
- Un README et ce CHANGELOG, qui n'existaient pas.
- Un fichier `tasks/todo.md` recensant les questions à trancher par le DIM, les défauts connus
  non corrigés et leur motif de report.

### Corrigé

- L'entrée de menu active n'était jamais mise en évidence : le style lisait `aria-current`,
  que personne ne posait, alors que la navigation pose `aria-selected`.
- Le filet de couleur à gauche des compteurs n'apparaissait pas : la propriété raccourcie
  `border`, déclarée après, l'écrasait.
- En thème sombre, les pastilles d'icône du menu (bleu marine de la conversion notamment)
  étaient presque invisibles. Elles passent par les variantes `-ink`, contraste non textuel
  mesuré entre 4,5:1 et 13:1 dans les deux thèmes.
- Valeurs issues du service local injectées sans échappement dans quatre écrans
  (contrôles, structure, fichiers complémentaires, identitovigilance).

- **Les positions de champs ont été confrontées aux descriptifs officiels de l'ATIH**, ce qui
  n'avait jamais été fait : elles venaient d'un script antérieur. Résultat, et il est nuancé.
  RPS et RAA, les deux formats du RIM-P et les seuls que ce DIM produit au quotidien, étaient
  déjà **exactement justes** et ne changent pas d'un caractère. Les autres ont été corrigés ou
  déclarés sans identifiant.
- **RPSA et R3A ne livrent plus le hachage d'anonymisation comme un identifiant de patient.**
  Ces fichiers, produits par PIVOINE, portent en positions 25 à 40 un cryptage irréversible de
  l'IPP, et aucune date de naissance. Lus aux positions du RPS, ce hachage entrait dans l'index
  patient sous l'étiquette IPP, ce qui reconstituait le lien que MAGIC et PIVOINE servent
  précisément à couper.
- Positions corrigées sur le descriptif officiel : VID-HOSP (identifiant en 354-373, longueur
  520), ANO-HOSP, FICHCOMP transports (63 caractères, aucun identifiant patient), RSF-A.
- **EDGAR retiré** de la matrice des formats : c'est une typologie d'actes ambulatoires
  (entretien, démarche, groupe, accompagnement, réunion) codée à l'intérieur du RAA, pas un
  format de fichier. Aucun fichier national ne porte ce nom.
- **Le repli en colonne brute redevient atteignable.** Un format sans identifiant déclaré ne
  reçoit plus de gabarit par défaut, donc l'export bascule sur la ligne brute annoncée au
  chapitre 4.3 du guide, qui jusque-là ne pouvait jamais se déclencher.
- **Identifiants patients retirés des messages d'anomalie.** Les contrôles recopiaient le NIR,
  l'IPP et la date de naissance dans le texte de l'anomalie, texte qui remonte à l'interface
  et qui est sérialisé dans un rapport écrit sur disque. Les messages portent désormais la
  position et l'écart constaté, jamais la valeur.
- **Trois défauts du classeur Excel d'origine, dans la colonne qui compose la ligne FICHCOMP.**
  Chaque formule visait une ligne située 196 rangs plus bas, donc composait la ligne d'un autre
  patient. La date était lue dans une colonne étiquetée identifiant patient et restée vide, au
  lieu de la date de transport. Le libellé d'unité fonctionnelle, de longueur variable, était
  concaténé sans être complété, ce qui décalait tout ce qui suivait. Conséquence à connaître :
  les lignes produites ne sont plus identiques à celles du classeur actuel.
- **Garde de confinement des chemins.** Le refus des chemins étendus portait sur la chaîne
  fournie, avant normalisation. La forme en barres obliques ne porte le préfixe qu'une fois
  normalisée, elle traversait donc la garde. Le contrôle porte maintenant sur le chemin qui
  sera réellement ouvert.

### Modifié

- Chapitre 4.3 du guide utilisateur entièrement réécrit, à la demande du médecin DIM. Il
  sépare désormais explicitement le fichier ATIH analysé, le descriptif que le DIM dépose et
  le CSV produit, et il dit franchement que la reconnaissance par le nom du fichier est une
  limite connue en cours de correction.
- Les affirmations que le code contredisait ont été corrigées dans les quatre documents.
  Étaient annoncés à tort : l'absence de toute position de champ dans le code, l'impossibilité
  d'écraser un fichier de sortie existant, et l'absence de tout appel externe.
- FICHSUP retiré des listes de formats reconnus : ce recueil est agrégé, sans ligne patient,
  et il est supprimé en psychiatrie depuis le 1er janvier 2021 au profit de FICHCOMP.
- Le pont HTTP, qui portait 533 lignes et toutes les responsabilités, est scindé par domaine
  métier. Les 25 points de terminaison sont inchangés, vérifiés un par un.

---

## [1.0.0] - 2026-07-29

### Ajouté

- Conversion des fichiers ATIH à largeur fixe en CSV.
- Regroupement des journées consécutives en épisodes de prise en charge ambulatoire.
- Lecture et export de la structure du GHT.
- Contrôles de qualité et de chaînage, identitovigilance, index patient de session.
- Nettoyage des classeurs de transports.
- Interface WPF hébergeant une interface web locale, servie par un pont HTTP sur la boucle
  locale avec authentification par jeton.
- Confinement des accès disque à des emplacements déclarés.
- Journal d'audit des actions.
- Suite de tests xUnit sur Pinel.Core.
- Quatre documents de référence, destinés à l'utilisateur, au DIM, à la DSI et au DPO.
