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

Deroule complet rejoue le 23/09/2026 depuis la cle USB rebranchee, huit etapes,
branche feat/fichiers-reels-2026. Donnees dans C:/Users/adamb/PinelDonnees, hors
de Documents qui est synchronise Google Drive. Resultats mesures :

- Copie : 1 407 fichiers, 4,2 Go.
- Formats ATIH 2026 PSY et MCO : 51 descriptifs, longueurs conformes aux
  fichiers reels.
- Pseudonymisation : 440 fichiers en 1 min 35, 967 ecartes faute de format
  reconnu, controle de fuite limite a 18 montants ressemblant a un identifiant,
  tous masques.
- Controles sur PSY M4 envoi 3, lot accepte par e-PMSI : 90 anomalies, contre
  162 878 avant les corrections du 22/09.
- Regles apprises : 62, toutes au-dessus de 90 pour cent de confiance, 28
  suggerees. VID-HOSP sejour facturable 1 vers 0 (959 cas) et motif vers 9
  (970 cas) a 100 pour cent.
- Modele des suppressions RAA : apprentissage M1 a M3, controle M6 jamais vu,
  AUPRC 0,998 pour un hasard a 0,005, 100 pour cent de vraies suppressions sur
  les 500 lignes les plus suspectes. Sur M7 jamais vu : 692 lignes signalees
  sur 150 687, toutes des repetitions du meme jour, nature A dans l'UM 5420.
- 210 tests verts.

A FAIRE PAR LE DIM
1. Relire les regles (`pinel regles`) et trancher : `pinel regle <n> valider`
   ou `rejeter`. Rien n'est applique a un fichier transmis sans validation.
2. Completer a la main ce qui n'est pas generalisable : 31 NIR non numeriques
   dans un VID-HOSP, un NIR bouche-trou partage par 217 patients dans un
   VID-IPP, le type d'unite laisse a 000 pour quelques UM par mois.
3. Faire corriger le parametrage Druides : le sejour facturable sort a 1 alors
   que le DIM le repasse a 0 tous les mois, 959 lignes en un seul envoi.

FAIT DANS LA FOULEE, le 23/09 apres-midi (PR 2, branche poussee)
- Ecran de revue dans l'application : regles avec leur confiance, decision du
  DIM regle par regle, lignes concernees, lignes signalees par le modele.
- Un seul executable : Pinel.exe ouvre la fenetre sans argument et joue les
  outils avec un verbe. Pinel.Cli est devenu une bibliotheque.
- Installateur Velopack sous le profil utilisateur, sans droits
  d'administration, mises a jour depuis un partage reseau : 30 Ko par
  correction contre 104 Mo en paquet complet. tools/packager.py.
- Donnees deplacees de %LOCALAPPDATA%/Pinel, dossier que l'installateur
  s'approprie, vers %LOCALAPPDATA%/Pinel-DIM, avec reprise automatique d'une
  installation anterieure.
- Identite de l'etablissement en reglage : Pinel ne suppose plus aucun
  etablissement, il sert n'importe quel DIM.
- Licence verifiee hors ligne, rattachee au FINESS, 30 jours de tolerance.
  Sans licence : lecture et controles disponibles, ecritures suspendues.
  tools/licence.py emet, la cle privee vit dans ~/.secrets.
- Icone dessinee (assets/icone-pinel.svg) avec variante des petites tailles,
  tools/make_icon.py. Derniers pictogrammes Icons8 retires.
- Systeme de design : docs/05_SYSTEME_DE_DESIGN.md et page vivante
  web/systeme.html. Echelle typographique recalee sur 16 px. Captures de la
  documentation refaites.
- 221 tests verts.

SUITE
- Point bloquant, juridique : la vente sous licence suppose un ecrit de
  l'etablissement (article L113-9 CPI, l'apprenti est salarie). Trois taches
  Notion creees, la premiere est de geler une preuve d'anteriorite.
- Dossier DPO et DSI avant tout usage hors de ce poste.
- Signature de code de l'installateur : aujourd'hui non signe, un EDR
  hospitalier peut le bloquer.
- Deux captures de la documentation montrent encore le violet de la revue
  (02_revue_des_corrections.png et 05_theme_sombre.png), la couleur est passee
  a l'olive apres coup. A refaire au prochain passage sur l'interface : le
  serveur de previsualisation a ete arrete par manque de memoire systeme.
- Factoriser les sept declarations locales de l'encodage ISO-8859-1 des
  controles sur PmsiEncoding.Latin1.
- Un test echoue par intermittence sous forte charge disque. Deux occurrences
  le 23/09, la seconde sur un passage de 2 min 06 la ou la suite tourne en 35 s.
  Aucune reproduction sur les passages suivants, et le nom du test n'a pas ete
  capture. Prochaine fois : relancer avec --logger trx pour avoir le nom avant
  de chercher. Piste la plus probable, une attente trop courte dans un test qui
  sonde le disque.

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
