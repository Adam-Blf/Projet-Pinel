# Pinel - etat des chantiers ouverts

Derniere mise a jour : 2026-08-28.
Branche de travail : `fix/moulinette-transports-et-correctitude`, jamais `main`.

Ce fichier existe parce qu'une intention gardee en tete n'est pas un report, c'est un oubli
programme. Tout ce qui est ouvert est ecrit ici, avec son motif.

---

## Origine du lot

Courriel du Dr Karanfilovic, medecin DIM du territoire, du 25/08/2026 :

1. "la moulinette a transports doit etre integree au reste"
2. "je ne comprends pas le paragraphe 4.3 Les descriptifs de format"
3. "J'ai l'impression que la moulinette nous oblige a nommer nos fichiers d'une certaine facon
   pour qu'elle fonctionne : est-ce bien cela ?"

Le point 3 est FONDE. Le point 1 reposait sur un malentendu de nommage, mais a revele un vrai
manque. Voir ci-dessous.

---

## Chantier 2026-09 : adaptation aux fichiers reels 2026 (checkpoint)

Decisions d'Adam du 22/09/2026 : les fichiers de D:/2026 ne sont lus qu'apres
pseudonymisation locale ; apprentissage en deux temps, regles tirees des
corrections du DIM puis modele ML local.

Fait (branche feat/fichiers-reels-2026) :
- `AtihWorkbookImporter` + `pinel formats-importer` : classeurs ATIH 2026 PSY et
  MCO convertis dans reference/formats/2026 (54 descriptifs).
- `RecordSpec` : zones repetees RPS (8 x nDA, 23 x nZA), RAA (8 x nDA),
  VID-HOSP (470 + 50 x N, compteur en 467-470). Les lignes VID-HOSP de 470, 570
  et 620 caracteres sont conformes.
- `ContentFormatDetector` : reconnaissance par longueur de ligne ; les noms
  Druides (vh_psy, vipp, fc_ic, dim_rps, PSY_RAA_HOSP_PMSI) trompaient
  l'identification par nom.
- `PmsiAnonymizer` + `pinel anonymiser` : liste blanche au caractere, cle
  DPAPI dans %LOCALAPPDATA%/Pinel/anonymisation.key, controle de fuite.
  Tests : AnonymizationTests.
- Execution du 22/09 : C:/Users/adamb/PinelDonnees/2026_brut -> 2026_pseudonymise, 440 fichiers, 2 min, zero fuite (18 montants ressemblant a un identifiant, masques). Donnees hors de Documents, synchronise Google Drive.

- Controles corriges sur le lot reel M4 envoi 3 : 162 878 anomalies -> 90.
  Restent a montrer au DIM : 31 NIR non numeriques dans VID-HOSP, 1 annee de
  naissance hors plage.

Reste, dans l'ordre :
2. Adapter Pinel sur les copies : ANO-HOSP (1584 puis 1712 car. des M3,
   AtihMatrix dit 1064), RSS groupe format 123 (zones repetees),
   identification par contenu branchee sur le scan de l'application (le scan
   ne lit encore que le nom), VID-HOSP a longueur variable dans LineInspector.
3. FAIT (22/09) : apprentissage des corrections, 40 regles dans
   C:/Users/adamb/PinelDonnees/regles-apprises.json. A FAIRE PAR LE DIM : relire
   les regles (`pinel regles`) et valider ou rejeter. Non automatisable, a
   signaler : un NIR assure partage par 217 patients dans le VID-IPP de M1
   (valeur bouche-trou), rempli a la main patient par patient.
   Suppressions de lignes RAA (135 a 599 par mois) : non deterministes, a
   confier au modele ML (etape 4).
4. EN COURS (pause du 22/09 au soir) : modele ML des suppressions RAA.
   Fait : projet src/Pinel.Ml (ML.NET 5 + LightGBM), RaaFeatureBuilder,
   DeletionModel (validation temporelle, champion / challenger, fiche modele),
   commandes `pinel entrainer` et `pinel scorer`. Premier entrainement sur
   M1-M3, controle sur M6 jamais vu : AUC 1,000, AUPRC 0,998 (hasard 0,005),
   100 % de vraies suppressions sur les 500 lignes les plus suspectes.
   Modele dans C:/Users/adamb/PinelDonnees/modele (hors depot, hors Drive).
   Reprendre ici, dans l'ordre :
   a. tests unitaires de Pinel.Ml (etiquetage, champion non degrade) ;
   b. lancer `pinel scorer` sur M7 (jamais vu) et verifier la coherence ;
   c. CHANGELOG, README (section ML), fiche vault, tache Notion ;
   d. brancher regles et modele dans l'interface (ecran de revue du DIM) ;
   e. avant tout usage sur donnees reelles hors ce poste : avis DPO et DSI.
5. Factoriser les sept declarations locales de l'encodage ISO-8859-1 des
   controles sur `PmsiEncoding.Latin1`.

## A REPONDRE AU MEDECIN, et c'est le plus urgent

### La reponse du 25/08 est a corriger

Il a ete repondu que le probleme de nommage venait "d'une erreur du modele d'entrainement".
C'est faux : il n'y a aucun modele dans Pinel, le code est du C# deterministe. La question
posee etait juste et merite une reponse exacte. A reprendre dans le prochain courriel, sans
quoi la confiance sur le reste tombe.

### Ce qu'il faut lui dire sur le nommage

Oui, aujourd'hui le nom du fichier compte, et non, ce n'est pas normal. Faits :

- Le format est deduit du seul nom du fichier (`AtihFormatIdentifier.cs:58`).
- Seuls les `.txt` et `.csv` sont regardes (`DirectoryScanner.cs:32`). Un fichier livre en
  `.ano` ou `.vid` est ignore SANS message.
- Un nom qui contient par hasard une suite reconnue est classe dans le mauvais format et
  decoupe avec les mauvaises positions, sans aucune alerte.
- L'annee dans le nom est OPTIONNELLE : `FileYearCheck.cs:41` sort sans rien dire si elle
  manque. C'est le sigle du format qui est obligatoire, pas l'annee.

### Ce qu'il faut lui dire sur la moulinette a transports

Elle etait deja integree, dans l'ecran "Fichiers complementaires", carte "Classeur
transports". Le mot "transport" n'apparaissait nulle part dans la navigation, et le seul ecran
que le code appelait "moulinette" est en realite la conversion des formats ATIH. Le vocabulaire
de l'interface a ete corrige.

En revanche le manque etait reel : le FICHCOMP transports n'existait pas en sortie. Ce qui
etait produit avait la structure d'une facture (Designation, Unite, PU HT, TVA, Somme), sans
rapport avec les 19 colonnes attendues.

---

## A VALIDER PAR LE DIM avant mise en production

Ces points ne peuvent pas etre tranches sans le metier. Ils bloquent la mise en production,
pas la poursuite du developpement.

| # | Sujet | Question precise |
|---|---|---|
| 1 | Largeur du code UF dans la ligne FICHCOMP | La valeur par defaut est 4. Aucune formule d'origine ne l'atteste. Quelle largeur ? |
| 2 | Largeur du libelle d'UF dans la ligne FICHCOMP | La valeur par defaut est 30. Meme motif. Quelle largeur ? |
| 3 | Colonne E, IPP | Marquee "colone a ajouter" et vide. D'ou vient l'IPP, et quelle regle en cas d'homonyme ? |
| 4 | Colonne H, NDA | Vide. Meme question. |
| 5 | Classe de distance `06` et code forfait `ST2` | Constants sur les 341 lignes. Le sont-ils toujours, ou se deduisent-ils du kilometrage ? |
| 6 | Correction volontaire de la colonne M | Les lignes produites ne seront PAS identiques a celles du classeur actuel. Voir la section suivante. |
| 7 | Tolerance de rupture des episodes | La valeur par defaut de 0 jour transforme un suivi CMP hebdomadaire en un episode par acte, tous de duree 0. Quelle tolerance en pratique ? |
| 8 | Canal de chainage de l'ambulatoire | Le RAA est-il chaine, et par quoi, au GHT ? |

---

## Defauts du classeur Excel d'origine, corriges volontairement

Le gabarit `Fichecomp_template.xlsx` et le classeur cible portent trois defauts dans la
colonne M, celle qui construit la ligne FICHCOMP transmise. Adam a tranche le 28/08/2026 :
on corrige et on documente l'ecart.

1. **Decalage de 196 lignes.** `M2 =CONCATENATE(A198,B198,...)`, `M3` vise la ligne 199, et
   ainsi de suite. Chaque ligne FICHCOMP etait composee avec les donnees d'un AUTRE patient.
   Au-dela de la ligne 147, les references pointent dans le vide.
2. **Date lue dans une colonne vide.** La formule prend `DAY(E)`, `MONTH(E)`, `YEAR(E)` alors
   que la colonne E est etiquetee "IPP (colone a ajouter)" et n'est jamais remplie. La date de
   transport est en colonne J.
3. **Libelle de largeur variable au milieu d'un format a largeur fixe.** Le libelle d'UF est
   concatene sans etre complete, ce qui decale tout ce qui suit.

Consequence a annoncer : les lignes produites par Pinel ne seront pas identiques a celles du
classeur actuel. C'est voulu.

---

## LE POINT LE PLUS GRAVE, en attente de decision

**Le repli honnete en ligne brute n'existe pas en production.** La documentation promet que,
sans descriptif depose, Pinel sort une colonne brute et le signale plutot qu'un decoupage
invente. C'est faux, et la chaine est verifiee ligne a ligne :

```
PinelSession.cs:79        new LayoutRegistry()      aucun argument
LayoutRegistry.cs:31      layouts ?? BuiltIn()      donc BuiltIn() s'execute
LayoutRegistry.cs:118     fabrique, pour CHACUN des 23 formats, un descriptif
                          year: null portant IPP et DATE_NAISSANCE tires de la matrice
RecordCsvExporter.cs:67   raw = layout is null || layout.Fields.Count == 0
                          2 champs presents, donc raw vaut TOUJOURS false
```

Consequence : **chaque CSV produit, pour les 23 formats, porte une colonne IPP et une colonne
DATE_NAISSANCE decoupees aux positions d'un script Python non date**, y compris sur les formats
anonymes ou l'arrete du 23 decembre 2016 etablit qu'il n'y a rien a ces positions.

Le test qui prouve la garde la prouve a cote : `MoulinetteTests.cs:107` construit son registre
avec `Array.Empty<FormatLayout>()`, donc SANS `BuiltIn()`. L'assertion est vraie dans le test
et fausse en production.

*Correction retenue, en attente du feu vert :* retirer `BuiltIn()`, et sans position etablie
ne rien sortir du CONTENU de la ligne. Attention, le repli en ligne brute serait une
AGGRAVATION : il ferait sortir le record entier, NIR compris sur VID-HOSP, la ou deux champs
sortaient. Ajouter la provenance, descriptif et millesime, dans le CSV produit.

*Ce que ca casse, et qu'il faut assumer :* les colonnes IPP et date de naissance disparaissent
des CSV des 23 formats, l'index patient demarre vide, l'export assaini est refuse, plusieurs
tests sont a reecrire.

*A faire APRES, jamais avant :* l'inference de gabarit au niveau de la ligne. Tant que
`BuiltIn()` vit, une proposition d'inference entre en concurrence avec une autorite silencieuse
qui gagne toujours.

**Deux chiffres corriges le 28/08/2026.** Les champs IPP et date de naissance ne se
chevauchent pas, ils sont ADJACENTS, les bornes de fin etant exclusives. Et la matrice porte 8
tuples de positions distincts, pas 7 : ce qui vaut 7 ce sont les plages de date de naissance,
dont une seule, `41-49`, couvre 14 formats. Meilleur exemple du defaut : `RSF-ACE-PSY` et
`RSFA` declarent un IPP en 221-241 et une date de naissance en 41-49 sur un enregistrement de
310 caracteres, soit une date de naissance a 180 caracteres de son identifiant.

---

## Defauts connus, non corriges dans ce lot

| Sujet | Etat | Motif du report |
|---|---|---|
| Blocage indefini sur le thread STA de WPF | **Corrige dans le CLI**, autres appelants non audites | `IdentityCsvExporter.ExportAsync(...).GetAwaiter().GetResult()` sur le thread STA de WPF, qui ne pompe jamais le Dispatcher, bloque pour toujours. Le mode ligne de commande figeait donc sur `--export`. Corrige par un `Task.Run`. Les autres appelants (`IdentityEndpoints`, `EpisodeEndpoints`) tournent a priori sous Kestrel donc hors du thread STA, mais ce n'est PAS prouve. A auditer. |
| `dotnet test` seul ne reconstruit pas `Pinel.Desktop` | Contrainte connue | `Pinel.Tests.csproj` ne reference pas l'assembly WPF, donc les tests du mode ligne de commande s'executent en boite noire sur le binaire deja construit. Toujours lancer `dotnet build Pinel.sln` AVANT `dotnet test`, sans quoi on mesure un binaire perime. Une garde echoue explicitement si le binaire est plus ancien que les sources. |
| CORS enregistre APRES l'authentification | Signale, non corrige | Le corriger a l'interieur du refactoring de `BridgeHost` aurait cache un changement de comportement dans un refactoring. A traiter dans un lot dedie, avec sa preuve. |
| `LogBuffer` n'a aucun producteur | Signale, non corrige | L'ecran Tracabilite affiche donc un journal toujours vide, alors qu'il est presente comme registre article 30. Deux issues : l'alimenter, ou retirer l'ecran. Montrer un cadre vide est la pire des trois. |
| Rendu HTML vers PDF execute les scripts de la page | Signale | Contredit la promesse "aucun appel a un service externe" du dossier de securite. Brider le script et le reseau sur ce WebView2, ou reformuler le dossier. |
| Journal d'audit modifiable par le compte tracé | Signale | Vit dans `%LOCALAPPDATA%`. Un journal que la personne auditee peut editer n'est pas un journal. |
| Port 8787 en dur | Signale | Sur un hote en bureau a distance, la deuxieme session ne peut pas se lier et l'application demarre muette. |
| Aucun controle n'emet de niveau bloquant sur le CONTENU | Signale | Le feu rouge "ne pas transmettre" annonce au chapitre 8 du guide ne peut jamais s'allumer sur une donnee fausse. |

---

## Questions de fond, hors perimetre technique

- **Propriete du code.** `Pinel.Desktop.csproj:14` declare `<Company>GHT Psy Sud Paris - DIM</Company>`
  et la ligne 20 `<Copyright>Copyright (c) 2026 Adam Beloucif</Copyright>`. Le meme fichier se
  contredit, il n'existe aucun fichier de licence, et le depot GitHub est public. L'article
  L113-9 du code de la propriete intellectuelle attribue de plein droit a l'employeur le
  logiciel cree par un salarie ou un apprenti dans l'exercice de ses fonctions. Adam a indique
  le 28/08/2026 qu'il considere posseder le code pour le moment. A faire trancher par les
  affaires juridiques de l'etablissement avant toute mise en production.
- **Attribution Icons8 : levee le 22/09/2026 pour l'interface.** Les icones de l'application
  sont passees a Phosphor (MIT), le texte de licence est dans "A propos". Reste : les deux
  marques de couverture des PDF (`docs/icons/guide.png`, `docs/icons/securite.png`, via
  `docs/generate_pdf.py`) sont encore des PNG Icons8, a remplacer par leur equivalent
  Phosphor rendu en PNG. Les captures `docs/screenshots/` montrent l'ancienne interface et
  sont a refaire depuis l'application lancee (le service local est requis pour les ecrans
  remplis).
- **Duree de conservation** des journaux et des fichiers exportes : non fixee. A acter avec la
  Direction des Ressources Numeriques.

---

## Ce qui n'est pas recuperable automatiquement

Les descriptifs officiels de l'ATIH ne sont pas telechargeables par un outil : le site rend
403 a toute requete automatisee. Il s'agit probablement d'une protection anti-robot et non
d'une exigence d'authentification. **A verifier a la main dans un navigateur**, puis a deposer
format par format. Sans eux, aucune position de champ ne peut etre affirmee juste.
