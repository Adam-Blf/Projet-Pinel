# Sécurité et conformité

Version 1.0.0.

Ce document s'adresse au délégué à la protection des données et à la direction
des systèmes d'information. Il décrit ce que Pinel traite, ce qu'il protège,
comment, et ce qui reste à trancher.

> **Ce document ne vaut pas avis juridique.** Il expose des faits techniques
> vérifiables et propose une lecture. Les qualifications juridiques appartiennent
> au délégué à la protection des données de l'établissement.

Les sigles sont développés à leur première apparition et figurent dans le
[glossaire](06_GLOSSAIRE.md).

---

## 1. Les données traitées

| Question | Réponse |
|---|---|
| **Nature** | Données de santé, catégorie particulière au sens de l'article 9 du règlement général sur la protection des données, qui interdit par principe leur traitement sauf exceptions énumérées |
| **Catégories** | Identifiant patient interne, date de naissance, sexe, dates d'actes et de séjours, unités médicales, codes de diagnostic et d'activité, éléments de chaînage dérivés du numéro de sécurité sociale |
| **Personnes concernées** | Patients pris en charge par les établissements du groupement, dont des personnes hospitalisées sans consentement, qui relèvent d'un régime juridique protecteur et comptent comme public vulnérable dans la grille du chapitre 9 |
| **Origine** | Fichiers déjà extraits par la chaîne de production. Pinel ne collecte rien |
| **Finalité** | Produire et contrôler le recueil national obligatoire, et le fiabiliser. Finalité **exclusivement administrative et financière**, jamais clinique, voir le chapitre 11 |
| **Base légale** | **À préciser par le délégué à la protection des données**, voir 1.1 |
| **Destinataires** | Personne. Pinel ne transmet rien |
| **Sous-traitant** | Aucun dans l'établissement d'origine : aucun tiers ne traite de données par l'intermédiaire de Pinel. **La question se repose si Pinel est installé ailleurs**, voir 1.2 |

**Position proposée** : le traitement PMSI existe déjà au registre des activités
de traitement de l'établissement. Pinel est un outil de ce traitement, pas un
traitement nouveau. Cette lecture appartient au délégué à la protection des
données.

### 1.1 La base légale reste à trancher, et ce n'est pas un détail

Deux fondements sont possibles au titre de l'article 9, paragraphe 2, du
règlement, qui liste les exceptions à l'interdiction de traiter des données de
santé :

- le **sous h**, gestion des systèmes et services de soins de santé ;
- le **sous i**, motifs d'intérêt public dans le domaine de la santé publique.

Et au titre de l'article 6, le traitement peut relever d'une **obligation légale**
(sous c), le recueil étant rendu obligatoire par les textes, ou d'une **mission
d'intérêt public** (sous e).

Ce choix a une conséquence directe : **le droit d'opposition de l'article 21 ne
s'applique pas du tout à un traitement fondé sur une obligation légale**, alors
qu'il s'applique de façon limitée à une mission d'intérêt public. Le tableau du
chapitre 8 en dépend.

Ce point est à trancher par le délégué à la protection des données, et le présent
document sera repris en conséquence.

### 1.2 Si Pinel est installé dans un autre établissement

Tout ce document raisonne en établissement unique. Le mécanisme de licence est
pourtant déjà rattaché au numéro FINESS, donc conçu pour plusieurs
établissements.

Le jour où Pinel est installé ailleurs, une question doit être tranchée avant
l'installation : **l'éditeur devient-il sous-traitant au sens de l'article 28 du
règlement ?**

Les éléments en faveur du non sont réels : l'outil est local, aucun flux ne
remonte vers l'éditeur, aucune donnée ne quitte le poste, et l'éditeur n'a aucun
accès aux traitements. Un fournisseur de logiciel installé n'est pas
automatiquement sous-traitant. Mais la réponse dépend aussi des prestations
associées, une assistance à distance ou une reprise de données feraient basculer
la qualification.

**Cette question n'est pas tranchée ici.** Elle demande l'avis d'un conseil
juridique, et un contrat de sous-traitance devra être signé si la réponse est
oui.

---

## 2. Ce qui ne sort jamais du poste

La version précédente de ce document affirmait « aucune télémétrie, aucune
recherche de mise à jour, aucun appel de contrôle ou de vérification vers
l'extérieur ». **Deux de ces trois affirmations sont devenues fausses**, et il
vaut mieux les remplacer par la description exacte des mécanismes que de laisser
un lecteur les découvrir par lui-même.

| Mécanisme | Existe-t-il ? | Sort-il du poste ? |
|---|---|---|
| Télémétrie, statistiques d'usage remontées à l'éditeur | **non** | - |
| Connexion à un système du système d'information | **non** | - |
| Appel à un réseau de diffusion de contenu pour des polices ou des scripts | **non**, tout est embarqué | - |
| Recherche de mise à jour | **oui** | Vers un **dossier d'un partage de l'établissement**, et seulement si un chemin a été renseigné. Jamais vers internet |
| Vérification de licence | **oui** | **Non.** Un fichier local, vérifié avec une clé publique embarquée dans l'exécutable |

Le service web interne écoute sur l'adresse de bouclage et n'est joignable depuis
aucun autre poste.

**Une réserve à conserver** : une fonction de conversion de page HTML en PDF
existe dans le service interne. Elle passe par le composant d'affichage, qui
exécute les scripts de la page rendue. Aucun bouton de l'interface ne l'appelle
aujourd'hui. Elle doit être retirée ou encadrée avant d'y exposer une commande.

---

## 3. Minimisation et conservation

Le principe de minimisation veut qu'on ne traite que ce qui est strictement
nécessaire à la finalité.

**Ce qui vit en mémoire seulement** : les fichiers lus et l'index des patients. À
la fermeture ou à la réinitialisation, ils disparaissent. Aucune base de données
n'est créée.

**Ce qui est écrit sur disque** : uniquement les fichiers que l'utilisateur
demande, dans le dossier qu'il a désigné.

**Ce qui persiste sous `%LOCALAPPDATA%\Pinel-DIM`**, et qu'il faut examiner un par
un :

| Fichier | Contient | Qualification |
|---|---|---|
| `settings.json` | Chemins autorisés, **et l'identité de l'établissement** : nom, numéros FINESS, constantes de transport | Pas de donnée patient. Ce n'est plus « que des chemins » |
| `formats\` | Descriptifs de format | Aucune donnée |
| `audit-AAAAMMJJ.log` | Journal d'audit | Voir chapitre 6 |
| `licence.lic` | Licence signée | Établissement et dates, aucune donnée personnelle |
| `maj.json` | Chemin du partage de mise à jour | Aucune donnée |
| **`regles-apprises.json`** | **Règles de correction et empreintes des paires traitées** | **Des codes, jamais un identifiant ni une date.** Voir 3.1 |
| **`modele\`** | **Modèle appris sur des lignes d'activité réelles** | **Des codes et des décomptes.** Voir 3.1 |
| **`anonymisation.key`** | **32 octets aléatoires, chiffrés par le service Windows DPAPI au compte courant** | **C'est un secret.** Voir 3.2 |

### 3.1 Les deux garanties d'apprentissage

Ce sont celles qu'un délégué à la protection des données doit pouvoir lire, et
elles sont inscrites dans le code.

**Les règles apprises ne portent que des codes.** Les champs de type identifiant
et les champs de type date sont exclus de l'apprentissage par construction. Le
fichier des règles peut donc être relu, discuté, versionné et transmis sans
emporter de donnée de patient.

**Le modèle ne conserve ni identifiant ni date.** L'identifiant du patient et la
date de l'acte servent à calculer des variables de répétition - est-ce le
troisième acte du même jour - puis ne sont pas conservés dans les variables
retenues.

### 3.2 La clé de pseudonymisation

C'est le seul secret que l'application détient.

- Sa **perte** rend irrécupérable le chaînage des copies pseudonymisées déjà
  produites.
- Sa **compromission** rend la pseudonymisation réversible pour qui détient ces
  copies.

Elle est protégée par le service Windows qui chiffre un secret de telle façon que
seul le compte utilisateur l'ayant créé puisse le relire. Sa sauvegarde, sa
rotation et son sort lors d'un changement de poste sont des décisions attendues,
voir le chapitre 12.

### 3.3 Aucune purge automatique

Pinel ne supprime rien de lui-même. Ni les exports produits, ni les règles, ni le
modèle, ni le journal. La politique de conservation est une décision de
l'établissement, voir le chapitre 12.

---

## 4. Confinement des emplacements

Pinel ne lit et n'écrit que dans les dossiers déclarés. Sont refusés : les
remontées d'arborescence, les contenus cachés attachés à un fichier Windows, les
chemins étendus qui désactivent la normalisation, et les liens ou jonctions
pointant hors de la zone.

La résolution des liens est faite **avant** la comparaison : un dossier autorisé
contenant une jonction vers ailleurs n'ouvre pas cet ailleurs.

---

## 5. Protection du service interne

| Risque | Mesure |
|---|---|
| Accès depuis un autre poste | Écoute sur l'adresse de bouclage uniquement |
| Commande par une page web tierce | Jeton de 32 octets tiré à chaque démarrage, exigé sur toutes les routes sauf le test de vie |
| Réattribution de nom vers l'adresse locale | Liste blanche de l'en-tête d'hôte |
| Appel depuis une autre origine | Une seule origine autorisée, trois méthodes |
| Mise en cache d'une réponse sensible | En-tête interdisant la mise en cache |
| Réinterprétation du type de contenu | En-tête l'interdisant |
| Fuite technique par une page d'erreur détaillée | Environnement forcé en production, vérifiable au démarrage du service |
| Exécution d'une formule par un tableur | Neutralisation à l'écriture des valeurs qu'Excel interpréterait comme une formule |
| Écrasement d'un fichier de sortie | Refus ou nom dérivé, selon l'opération |

---

## 6. Traçabilité

Le journal répond à la question *qui a fait quoi, et quand*, jamais *sur qui*.

Champs : horodatage, opération, méthode, code de réponse, volume, et
éventuellement un détail et un **dossier**.

### 6.1 La garantie exacte, et ce qui a changé

La version précédente affirmait qu'aucun chemin de fichier ne figurait au
journal. **C'est faux, et le choix est assumé** : un chemin de dossier n'est pas
une donnée de patient, et c'est précisément ce qui rend un acte imputable, car il
nomme l'espace de travail sur lequel on a agi.

La garantie réelle, mécanique et couverte par des tests, est la suivante :

> **Toute suite de neuf chiffres ou plus est remplacée par un marqueur avant
> écriture au journal.**

Le seuil vient du fait que le numéro d'établissement fait neuf chiffres, et que
l'identifiant patient comme le numéro de sécurité sociale sont plus longs. En
dessous, une suite de chiffres est un compte, une année ou une taille.

Un nom de fichier qui contiendrait un identifiant patient est donc masqué par la
même règle.

Rotation quotidienne. Chaque ligne est un enregistrement complet, lisible au fil
de l'eau.

---

## 7. Anonymisation et pseudonymisation

### 7.1 La distinction, qui est décisive

| | Pseudonymisation | Anonymisation |
|---|---|---|
| Retour à la personne | Possible pour qui détient la clé | Impossible pour tout le monde |
| Statut juridique | **Reste une donnée personnelle**, soumise au règlement | N'est plus soumise au règlement |

**Ce que Pinel produit est de la pseudonymisation**, pas de l'anonymisation. Le
chapitre qui suit doit être lu avec cette qualification en tête.

### 7.2 Le contrôle des fichiers censés être anonymes

Un contrôle vérifie qu'aucun identifiant en clair n'est resté dans un fichier qui
ne devrait en porter aucun. Il porte sur **deux formats, RPSA et R3A**, et non
six comme annoncé précédemment : les quatre autres formats anonymes ne déclarent
aucun champ patient dans les descriptifs 2026, le contrôle n'aurait rien à
regarder.

Une précision qui a sa place ici : la fenêtre du cryptage irréversible de
l'identifiant est déclarée **dans le contrôle lui-même**, délibérément, et non
dans la table des formats. Ces formats y déclarent zéro identifiant précisément
pour qu'aucun analyseur n'en extraie quoi que ce soit.

### 7.3 L'export à date de naissance unifiée

Une copie du fichier où la date de naissance retenue remplace les valeurs
divergentes, à largeur fixe préservée. C'est une correction d'identitovigilance,
pas une anonymisation.

### 7.4 Le dispositif de pseudonymisation

Destiné au travail hors contexte de production : mise au point, démonstration,
recherche. Cinq garanties.

**Liste blanche au caractère près.** Chaque ligne de sortie part d'une copie
entièrement masquée de la ligne d'origine, puis seuls les caractères couverts par
un champ connu du descriptif sont réécrits. Un champ ajouté par un éditeur, une
zone de remplissage remplie, une fin de ligne en trop restent masqués. C'est une
garantie plus forte qu'une liste de ce qu'il faut cacher : ici, **ce qui n'est pas
explicitement connu reste caché**.

**Classement par libellé officiel, pas par position.** Dix rôles sont reconnus,
de l'identifiant patient au code postal. Un libellé ambigu penche toujours vers le
traitement le plus protecteur.

**Chaînage préservé.** Un calcul irréversible sous clé locale : la même valeur
donne toujours le même pseudonyme, de même longueur et de même nature. Le même
patient reste reconnaissable d'un fichier à l'autre sans être identifiable.

**Contrôle de fuite final.** Toute valeur d'identifiant vue en entrée est
recherchée dans les champs recopiés tels quels, et comptée par champ dans le
rapport **sans jamais être affichée**.

**Fichier non reconnu, fichier non copié.** Ce dont le format n'est pas établi
reste sur le poste d'origine.

### 7.5 Le refus d'écrire dans un dossier synchronisé

Pinel refuse d'écrire une copie pseudonymisée vers les emplacements **reconnus**,
dans leur configuration par défaut, comme dossiers synchronisés par les
principaux services de stockage grand public.

Le fondement est écrit dans le code : l'hébergement de données de santé pour le
compte d'un tiers est soumis à certification par le code de la santé publique. Un
dossier synchronisé vers un service grand public place les fichiers chez un
hébergeur non certifié, sans que personne ne l'ait décidé.

> **Cette détection réduit le risque, elle ne l'élimine pas.** Elle reconnaît des
> emplacements connus. Un chemin de synchronisation personnalisé, un client moins
> courant, ou un lecteur réseau qui synchronise lui-même vers un nuage en amont,
> ne sont **pas couverts**. La vigilance sur l'emplacement de sortie choisi reste
> nécessaire.

La garde a été vue en refus sur un cas réel avant d'être considérée comme
acquise. La référence exacte de l'article du code de la santé publique est à
revérifier avant toute publication de ce document hors de l'établissement, la
disposition ayant fait l'objet de plusieurs textes d'application.

### 7.6 Pourquoi l'apprentissage lit des fichiers réels

Question qu'un délégué à la protection des données posera, et à laquelle il vaut
mieux répondre par avance.

L'apprentissage des règles et l'entraînement du modèle lisent les fichiers dans
leur état réel, et non une copie déjà pseudonymisée. Le motif est technique :
les variables de répétition du modèle - est-ce le troisième acte du même patient
le même jour - **ont besoin de l'identifiant et de la date au moment du calcul**.
Une fois ces variables calculées, ni l'identifiant ni la date ne sont conservés.

Conséquence en termes de conformité : **la phase d'entraînement traite des
données de santé**, même si l'artefact produit n'en contient aucune. C'est cette
phase, et non le modèle final, qui doit figurer au registre des activités de
traitement.

---

## 8. Droits des personnes

| Droit | Exerçable sur Pinel ? | Modalité |
|---|---|---|
| Information | Au niveau du traitement PMSI | Notice de l'établissement |
| Accès | Oui | Par le département d'information médicale, sur les fichiers sources |
| Rectification | Oui | Dans la chaîne de production, en amont |
| Effacement | Limité | Le recueil répond à une obligation légale |
| Limitation | Limité | Même motif |
| Opposition | **Dépend de la base légale retenue**, voir ci-dessous | - |
| Portabilité | Non applicable | Ce droit ne vaut que pour les traitements fondés sur un contrat ou un consentement, ce qui n'est pas le cas ici |

**Sur le droit d'opposition.** Si le fondement retenu est l'obligation légale, le
droit d'opposition de l'article 21 **ne s'applique pas du tout** : il n'est pas
seulement limité. Si le fondement retenu est la mission d'intérêt public,
l'établissement peut refuser l'opposition pour un motif légitime et impérieux
prévalant sur les intérêts de la personne. Le fondement est à confirmer, voir 1.1.

Pinel ne crée aucun droit nouveau et n'en retire aucun. Il travaille sur des
fichiers dont le traitement est déjà encadré.

Le personnel qui utilise Pinel est tenu au secret professionnel prévu par le code
de la santé publique, au même titre que pour tout accès aux données de santé de
l'établissement. Aucune clause propre à Pinel n'est nécessaire.

---

## 9. Analyse d'impact

> **Avertissement, et il conditionne tout ce chapitre.** Les fonctions
> d'apprentissage ne doivent pas être activées sur un poste de production tant
> que le délégué à la protection des données n'a pas rendu son avis sur la mise
> à jour de l'analyse d'impact du traitement PMSI. Cette condition est reprise au
> chapitre 14, et elle fait foi.

L'analyse d'impact relative à la protection des données est obligatoire pour les
traitements susceptibles d'engendrer un risque élevé. La grille de référence de
la Commission nationale de l'informatique et des libertés compte neuf critères, et
**deux critères réunis suffisent** à rendre l'analyse obligatoire.

| Critère | Réuni ? | Précision |
|---|---|---|
| Évaluation ou notation | **Non, avec réserve motivée**, voir 9.1 | Le modèle évalue la plausibilité d'un enregistrement administratif, pas un aspect de la personne |
| Décision automatisée avec effet juridique | **Non**, voir 9.2 | Aucune règle ne modifie seule un fichier transmis |
| Surveillance systématique | Non | Aucune observation de personnes |
| Données sensibles | **Oui** | Données de santé |
| Grande échelle | **Oui** | Volumétrie annuelle du recueil d'un groupement |
| Croisement de jeux de données | **Oui** | Le chaînage croise activité et identité |
| Personnes vulnérables | **Oui** | Patients en psychiatrie, dont des personnes hospitalisées sans consentement |
| **Usage innovant** | **Oui** | **Le produit embarque un modèle d'apprentissage automatique et un moteur d'induction de règles.** La version précédente de ce document répondait « non, aucune fonction d'intelligence artificielle dans cette version ». Cette réponse est devenue fausse |
| Obstacle à l'exercice d'un droit | Non | Aucun |

**Cinq critères sont réunis, et non quatre.** La correction porte sur l'usage
innovant.

**Conséquence.** Le seuil de deux critères était déjà largement dépassé
auparavant : la conclusion ne change donc pas dans son principe. L'analyse
d'impact est due **au niveau du traitement PMSI de l'établissement**, pas au
niveau de l'application, et elle existe probablement déjà.

**Ce qui change en revanche.** Si une analyse existe, elle a été conduite sur un
traitement sans fonction d'apprentissage. L'ajout d'un modèle et d'un moteur de
règles est une évolution substantielle, et elle appelle une mise à jour, même si
la conclusion reste la même.

### 9.1 Pourquoi le critère d'évaluation reste à non, et ce que cet argument ne dit pas

L'argument facile serait : le modèle ne voit ni identifiant ni date, donc il
n'évalue personne. **Cet argument ne suffit pas**, et il vaut mieux le dire.

La ligne évaluée correspond à un acte réel d'un patient réel, dans le fichier que
le technicien a sous les yeux au moment de la revue. Le lien avec la personne
n'est pas absent : il se recompose à l'usage.

L'argument qui tient est autre. Le modèle évalue **la plausibilité d'un
enregistrement administratif** - la probabilité qu'une ligne corresponde à une
suppression - et non un aspect de la personne au sens de la doctrine : ni sa
santé, ni son comportement, ni sa fiabilité, ni sa situation économique, ni ses
déplacements. Ce n'est donc pas une absence de lien avec la personne, c'est une
absence d'évaluation **de** la personne.

Cette distinction doit être validée par le délégué à la protection des données.

### 9.2 Le dispositif qui rend le « non » défendable sur la décision automatisée

L'article 22 du règlement vise une décision fondée **exclusivement** sur un
traitement automatisé. Voici pourquoi ce n'est pas le cas :

1. Une règle apprise naît **proposée**, jamais validée.
2. Une règle proposée n'entre dans les suggestions que si elle réunit un nombre de
   cas et une confiance élevés, et **être suggérée ne veut dire qu'être comptée**.
3. Seule une règle **validée par une personne du service** peut modifier une copie.
4. Cette modification demande une commande explicite avec un dossier de
   destination explicite.
5. Le fichier d'origine n'est jamais touché.
6. Le modèle, lui, ne corrige jamais rien : il signale des lignes à relire.

**L'intervention humaine est réelle, pas de façade**, et c'est ce qu'exige la
doctrine : la personne voit le décompte des lignes concernées avant de décider,
dispose d'un statut de rejet à égalité avec celui de validation, et sa décision
n'est jamais reconduite automatiquement lors d'un réapprentissage.

---

## 10. Le règlement européen sur l'intelligence artificielle

Pinel embarque **deux systèmes d'intelligence artificielle** au sens du règlement
(UE) 2024/1689 : le moteur d'induction de règles et le modèle de détection des
suppressions. Le fait que l'un des deux ne soit pas un réseau de neurones ne le
sort pas du champ : la définition n'est liée à aucune technique.

### 10.1 Classification proposée

**Système à risque minimal**, hors de l'annexe III du règlement, qui liste les
usages à haut risque.

Aucune catégorie de cette annexe ne correspond à un contrôle qualité administratif
d'un recueil de financement hospitalier. Les catégories les plus proches - accès
aux prestations et services essentiels, évaluation du risque en assurance santé -
visent des décisions individuelles affectant l'accès d'une personne à une
prestation, pas la fiabilisation interne d'un recueil.

Pinel n'est pas davantage un composant de sécurité d'un dispositif médical,
puisqu'il n'est pas un dispositif médical, voir le chapitre 11.

**Argument de repli, à connaître.** Si un auditeur rattachait un jour Pinel à
l'annexe III par analogie, le règlement prévoit une dérogation pour les systèmes
qui exécutent une tâche procédurale étroite, ou qui détectent des écarts par
rapport à une décision humaine déjà prise sans la remplacer. Les deux fonctions de
Pinel y correspondent : elles signalent, elles ne décident jamais, et une décision
humaine préexiste toujours, puisque c'est la correction que le service applique
déjà.

**Attention au verrou de cette dérogation** : elle ne joue jamais si le système
réalise un profilage de personnes physiques. C'est une raison de plus, indépendante
de l'analyse d'impact, de documenter pourquoi le modèle évalue des enregistrements
et non des personnes, voir 9.1.

### 10.2 Les obligations qui s'appliquent malgré tout

Deux obligations générales valent pour tout système d'intelligence artificielle, y
compris à risque minimal.

**Culture de l'intelligence artificielle.** L'établissement, en tant que
déployeur, doit s'assurer que le personnel qui utilise Pinel dispose d'une
connaissance suffisante du fonctionnement, des risques et des limites des
fonctions d'apprentissage. C'est une obligation légère mais réelle. **Elle n'est
pas couverte aujourd'hui** : un briefing du service, même court, suffirait à en
apporter la preuve, et il reste à faire.

**Transparence envers les personnes physiques.** Elle vise les systèmes qui
interagissent directement avec des personnes, qui génèrent du contenu de synthèse,
ou qui font de la reconnaissance d'émotion. Pinel n'est dans aucun de ces cas : il
n'interagit qu'avec le personnel du service, sur des données déjà enregistrées, et
ne génère aucun contenu adressé à un patient. **Cette exclusion est écrite ici
plutôt que supposée.**

### 10.3 Ce qui ferait basculer la classification

Deux évolutions, et il faut les surveiller :

- l'usage des sorties du modèle pour modifier **sans intervention humaine** un
  fichier transmis aux autorités, ce que l'architecture actuelle exclut ;
- une extension vers une décision individuelle affectant l'accès d'un patient à
  une prestation, qui rattacherait le système à l'annexe III.

La classification est à réexaminer si Pinel est distribué à un établissement qui
en ferait un usage différent.

---

## 11. Le règlement sur les dispositifs médicaux

Pinel **n'est pas un dispositif médical** au sens du règlement (UE) 2017/745.

Le fondement n'est pas l'absence générale de finalité médicale, c'est le **test de
finalité** posé par le règlement : un dispositif médical se définit par la
finalité que lui donne son fabricant - diagnostic, prévention, surveillance,
prédiction, pronostic, traitement, atténuation d'une maladie, compensation d'un
handicap.

Pinel n'a aucune de ces finalités. Sa finalité déclarée et exclusive est la
fiabilisation d'un recueil administratif et financier.

**Le point sensible, à anticiper parce qu'un auditeur le soulèvera** : le modèle
utilise le chapitre du diagnostic principal comme variable. Cela n'introduit pas
de finalité médicale, parce que la variable sert à juger la plausibilité d'un
enregistrement, jamais à produire, suggérer ou modifier une information clinique
sur un patient déterminé. Aucune sortie de Pinel n'est versée à un dossier patient
ni présentée à un professionnel de santé en contexte de soin.

Cette lecture s'appuie sur le test de finalité et sur les documents d'orientation
européens qui excluent du champ des dispositifs médicaux les logiciels à finalité
administrative ou de facturation. **Elle demande confirmation par un avocat
spécialisé**, et un réexamen si les fonctions d'apprentissage étaient un jour
réutilisées à des fins de pilotage clinique.

---

## 12. Risques et mesures

| Risque | Vraisemblance | Gravité | Mesure |
|---|---|---|---|
| Lecture ou écriture hors du périmètre prévu | faible | élevée | Confinement aux dossiers déclarés, résolution des liens avant comparaison |
| Commande du service local par une page tierce | faible | élevée | Jeton par démarrage, liste blanche d'hôte, origine unique |
| Identifiant patient recopié dans un message ou un journal | faible | élevée | Masquage mécanique de toute suite de neuf chiffres ou plus, couvert par des tests |
| Export laissé en clair dans un dossier partagé | moyenne | élevée | Mesure organisationnelle seulement, voir chapitre 13. Un incident relève de la procédure de notification de violation de l'établissement |
| **Réidentification par recoupement** | **à réévaluer selon l'usage** | élevée | Voir 12.1 |
| Dépôt dans un dossier synchronisé vers un nuage grand public | moyenne | élevée | Refus à l'écriture sur les emplacements reconnus, **détection non exhaustive**, voir 7.5 |
| Perte ou compromission de la clé de pseudonymisation | faible | élevée | Protection par le service Windows au compte courant. Sauvegarde et rotation à décider |
| Altération du canal de mise à jour | faible | élevée | Paquets signés et vérifiés, partage interne. Droits d'écriture à décider |
| Installateur non signé bloqué ou contourné | moyenne | moyenne | Certificat de signature à obtenir |

### 12.1 La réidentification par recoupement

Ce risque mérite son paragraphe, parce que la réponse évidente n'y répond pas.

La pseudonymisation empêche le retour à l'identité sans la clé. **Elle ne protège
pas contre une réidentification par recoupement de quasi-identifiants** - âge,
diagnostic rare, commune, date singulière - avec des données dont dispose le
destinataire d'un export.

La maîtrise relève de la personne qui diffuse l'export hors du service. Pour tout
export destiné à un usage externe, **un seuil de suppression des cellules à faible
effectif est à appliquer**, selon le principe déjà retenu ailleurs dans le service
pour les exports de recherche.

Aucun mécanisme automatique ne l'applique aujourd'hui dans Pinel. C'est une
mesure organisationnelle, et c'est une faiblesse à connaître.

### 12.2 Ce qui n'est pas prévu et ne le sera pas

Aucune donnée traitée par Pinel n'est destinée à une plateforme nationale de
données de santé, ni à une mutualisation entre établissements. Pinel produit des
fichiers locaux, et leur diffusion relève des décisions de l'établissement.

---

## 13. Mesures organisationnelles

Elles ne sont pas techniques, et c'est pour cela qu'il faut les écrire.

1. **Réinitialiser entre deux utilisateurs** sur un poste partagé.
2. **Les exports sont écrits en clair.** Leur protection relève des droits du
   système de fichiers sur le dossier de sortie.
3. **Aucune purge automatique.** Rien ne disparaît tant que personne ne le retire.
4. **La clé de pseudonymisation suit le compte utilisateur.** Un changement de
   poste sans transfert rend irrécupérable le chaînage des copies déjà produites.
5. **Un export diffusé hors du service demande un contrôle de faible effectif**,
   voir 12.1.
6. **Un incident relève de la procédure de notification de violation de données
   de l'établissement**, dans les délais prévus par le règlement.

---

## 14. Points à trancher

| # | Sujet | Décision attendue de |
|---|---|---|
| 1 | **Base légale exacte du traitement, et conséquence sur le droit d'opposition**, voir 1.1 | Délégué à la protection des données |
| 2 | **Mise à jour de l'analyse d'impact au titre des fonctions d'apprentissage**, voir chapitre 9 | Délégué à la protection des données |
| 3 | **Avis préalable avant tout usage des fonctions d'apprentissage en production** | Délégué à la protection des données |
| 4 | **Inscription au registre de la phase d'entraînement**, qui traite des données de santé, voir 7.6 | Délégué à la protection des données |
| 5 | **Durée de conservation des règles apprises et des modèles.** Proposition de départ : conserver les règles tant qu'elles servent, et ne garder que les trois derniers modèles de l'historique | Délégué à la protection des données |
| 6 | **Statut de l'éditeur en cas d'installation dans un autre établissement**, voir 1.2 | Conseil juridique |
| 7 | **Classification au titre du règlement sur l'intelligence artificielle**, voir 10.1 | Conseil juridique |
| 8 | **Formulation opposable de l'exclusion du champ des dispositifs médicaux**, voir chapitre 11 | Conseil juridique |
| 9 | Sort de la clé de pseudonymisation : sauvegarde, rotation, changement de poste | Direction informatique et délégué |
| 10 | Emplacement et droits d'écriture du partage de mise à jour | Direction informatique |
| 11 | Certificat de signature de code de l'installateur | Direction informatique |
| 12 | Conservation du journal d'audit : durée, sauvegarde, accès | Direction informatique |
| 13 | Droits du système de fichiers sur le dossier de sortie | Direction informatique |
| 14 | Comportement à la désinstallation : conserver ou effacer les données | Les deux |
| 15 | **Briefing du service sur le fonctionnement, les risques et les limites des fonctions d'apprentissage**, voir 10.2 | Département d'information médicale |
| 16 | Politique de conservation des exports | Département d'information médicale |

---

## 15. Protection dès la conception

Le règlement impose de penser la protection des données dès la conception et par
défaut. Les mesures décrites dans ce document en sont l'application concrète, et
il vaut la peine de les rassembler :

| Mesure | Où |
|---|---|
| Liste blanche au caractère près : ce qui n'est pas connu reste masqué | 7.4 |
| Identifiants et dates exclus de l'apprentissage par construction | 3.1 |
| Masquage mécanique des identifiants au journal, couvert par des tests | 6.1 |
| Aucun message d'anomalie ne reproduit une valeur lue | Dossier fonctionnel, 8.4 |
| Confinement aux dossiers déclarés, refusé par défaut | 4 |
| Refus d'écrire vers un hébergeur non certifié reconnu | 7.5 |
| Aucun flux sortant vers internet | 2 |
| Validation humaine obligatoire avant toute correction | 9.2 |

Aucune de ces mesures n'a été ajoutée après coup pour répondre à une exigence :
elles sont dans la conception, et plusieurs ont été vues en refus avant d'être
considérées comme acquises.

---

## 16. Contact

Adam BELOUCIF
Ingénieur PMSI, Département d'Information Médicale
[adam.beloucif@psysudparis.fr](mailto:adam.beloucif@psysudparis.fr)

GH Fondation Vallée - Paul Guiraud, GHT Psy Sud Paris
