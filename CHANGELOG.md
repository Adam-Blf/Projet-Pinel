# CHANGELOG

Tous les changements notables pour Pinel sont documentés dans ce fichier, du plus récent au plus ancien.

Le format s'inspire de [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/), et le versioning suit [Semantic Versioning](https://semver.org/lang/fr/).

Règle de tenue de ce fichier : n'y figure que ce qui est livré et prouvé par un build et des
tests verts. Les chantiers en cours vivent dans `tasks/todo.md`, pas ici. Un journal qui
annonce ce qui n'existe pas encore ne sert plus à personne.

---

## Non publié

### Ajouté

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
