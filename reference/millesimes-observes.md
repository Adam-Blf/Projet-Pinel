# Longueurs de ligne observees, 2014 a 2026

Releve automatique du 23/09/2026 sur le lot OSPI : 458 fichiers texte du
GHT, trois etablissements, douze exercices. **Aucune valeur de champ n'a ete
lue** : seules des longueurs de ligne ont ete mesurees, ce que fait deja le
detecteur de format par le contenu.

Ce document existe pour une raison simple : les formats ATIH changent de
longueur presque chaque annee, et Pinel ne porte que les longueurs courantes.
Appliquer les positions d'un millesime a un fichier d'un autre produit des
anomalies fausses en masse. La garde `LineLengthGate` s'appuie sur ce constat.

Le sigle est celui devine dans le NOM du fichier. La colonne *declare* donne
ce que porte `AtihMatrix`, variantes comprises.

| Format | Declare par Pinel | 2014 | 2015 | 2016 | 2017 | 2018 | 2019 | 2020 | 2021 | 2022 | 2023 | 2024 | 2025 | 2026 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ANO-HOSP | 1064 |  |  |  |  | **825** | **846** | **936** **1283** | **936** **1283** | **1455** **1933** | **1584** **1933** | **1584** **1933** |  |  |
| FICHCOMP | 63 |  |  |  |  |  | **31** | **33** | **33** 63 | 63 | 63 | 63 | 63 | 63 |
| FICHCOMP-ISO | 113 |  |  |  |  | **0** **63** | **63** **129** | **112** **227** | **112** **227** | **112** | 113 | 113 | 113 | 113 |
| FICHCOMP-TP | 47 |  |  |  |  |  |  | **43** | **43** | 47 | 47 | 47 |  |  |
| FICUM-PSY | 38 |  |  |  |  | **28** 38 | **28** 38 | **28** 38 | **28** | **28** 38 | **28** 38 | **28** 38 | 38 |  |
| RAA | 86 ou 90 ou 96 |  |  |  |  | **92** | **92** | **92** | **93** | 96 | 96 | 96 | 96 |  |
| RPS | 142 ou 148 ou 154 |  |  |  |  | **152** | **152** | **152** | **152** | 154 | 154 | 154 | 154 |  |
| VID-HOSP | 520 | **314** | **324** |  | **369** | **432** | **452** | **452** | **452** | **468** | **468** | **468** | **518** |  |
| VID-IPP | 135 |  |  |  |  |  |  | **120** **124** | **120** | **120** 135 | 135 | 135 | 135 |  |

**En gras, les longueurs que Pinel ne declare pas.**

## Ce que ce tableau etablit

1. **Le VID-HOSP a change huit fois de structure** entre 2014 et 2026 : 314,
   324, 369, 432, 452, 468, 518, puis 520. Pinel n'en connait qu'une.
2. **Les variantes declarees pour le RPS et le RAA ne correspondent a rien**
   dans ce lot. Pinel declare RPS 142 et 148, RAA 86 et 90 ; les fichiers
   reels portent RPS 152 avant 2022, RAA 92 puis 93. Ces variantes viennent
   probablement du meme ancien script que les positions corrigees le 28/08.
3. **L'ANO-HOSP double de taille en six ans**, de 825 a 1933 caracteres.

## Ce qu'il ne faut PAS en conclure

Ces longueurs ne donnent aucune position de champ. Ajouter une longueur a la
matrice sans le descriptif officiel du millesime correspondant reviendrait a
autoriser un decoupage devine, c'est-a-dire exactement le defaut corrige le
28/08/2026. Tant qu'un descriptif n'est pas depose, la garde refuse les
controles de position et le dit : c'est le comportement voulu.
