# Glossaire

Le PMSI parle une langue à lui. Ce glossaire la traduit. Il est fait pour être
lu en diagonale, quand un mot bloque la lecture d'un autre document.

## Les dix mots qui suffisent

Si vous ne deviez en retenir que dix, ce sont ceux-là. Le reste se cherche au
moment où il bloque.

| Mot | En une phrase |
|---|---|
| PMSI | Le recensement national de l'activité des hôpitaux, dont dépend leur financement. |
| ATIH | L'agence qui définit les formats de fichiers et reçoit les transmissions. |
| DIM | Le service de l'hôpital qui fabrique et contrôle ces transmissions. |
| Fichier à largeur fixe | Un fichier où tout est collé, et où chaque information occupe une position fixée d'avance. |
| Descriptif de format | Le document qui dit quelle information occupe quelle position, pour une année donnée. |
| FINESS | Le numéro d'identification d'un établissement de santé, neuf chiffres. |
| IPP | Le numéro qu'un hôpital attribue à un patient, et qui ne change plus. |
| Chaînage | Reconnaître qu'un patient vu en mars et un patient vu en octobre sont la même personne, sans jamais transmettre son identité. |
| Épisode | Un parcours reconstruit à partir d'actes isolés, que le format national ne sait pas relier. |
| Anomalie | Ce qu'un contrôle signale, avec un niveau de gravité qui va de la remarque au blocage. |

Le reste du document reprend ces mots et en ajoute d'autres, groupés par sujet.

---

## Le décor

**PMSI**
Programme de médicalisation des systèmes d'information. C'est le recensement
national de l'activité des hôpitaux. Chaque établissement décrit ce qu'il a fait
pour chaque patient, dans un format imposé, et transmet le tout à l'État
plusieurs fois par an. De ces transmissions découle une grande partie du
financement de l'hôpital.

**ATIH**
Agence technique de l'information sur l'hospitalisation. L'organisme public qui
définit les formats de fichiers, publie leurs spécifications chaque année et
reçoit les transmissions. Quand un document parle du « format ATIH », il parle
d'une structure de fichier publiée par cette agence.

**DIM**
Département d'information médicale. Le service de l'hôpital qui fabrique et
contrôle ces transmissions. Il est composé de médecins et de techniciens
d'information médicale. C'est le service pour lequel Pinel est écrit.

**RIM-P**
Recueil d'information médicalisée en psychiatrie. Le PMSI appliqué à la
psychiatrie. Il a ses propres formats, distincts de ceux de la médecine et de la
chirurgie, parce que l'activité psychiatrique se compte autrement : beaucoup de
consultations, de visites à domicile et de suivis longs, peu de séjours au sens
classique.

**GHT**
Groupement hospitalier de territoire. Plusieurs hôpitaux publics d'un même
territoire, obligés depuis 2016 de coopérer et de partager certaines fonctions,
dont l'information médicale. Un DIM de territoire couvre donc plusieurs
établissements.

**FINESS**
Le numéro d'identification d'un établissement de santé, neuf chiffres. C'est
l'équivalent du numéro SIRET pour l'hôpital. Il apparaît dans presque tous les
fichiers PMSI, et une erreur sur ce numéro fait rejeter toute une transmission.

**ARS**
Agence régionale de santé. L'échelon régional qui supervise les établissements.
Elle reçoit certains fichiers, anonymes, que l'ATIH ne reçoit pas.

**e-PMSI**
La plateforme en ligne sur laquelle les établissements déposent leurs fichiers.
Elle exécute ses propres contrôles et accepte ou rejette la transmission. Quand
un document dit « un lot accepté par e-PMSI », il parle d'un envoi qui a passé
ces contrôles.

---

## Les fichiers

**Fichier à largeur fixe**
Un fichier texte sans séparateur : les champs se suivent collés, et on sait où
commence chacun parce que sa position est fixée d'avance. Une ligne de RPS fait
154 caractères, et le caractère numéro 22 commence toujours l'identifiant du
patient. Ouvert dans Excel, un tel fichier donne une seule colonne illisible.
C'est le problème que Pinel résout.

**Descriptif de format**
Le document qui dit, pour un format et une année donnés, quel champ commence à
quelle position et sur combien de caractères. Sans lui, un fichier à largeur fixe
est une suite de caractères sans sens. L'ATIH le publie chaque année.

**Millésime**
L'année d'un format. Les formats changent tous les ans, donc un fichier de 2024
ne se découpe pas avec les positions de 2026. Le millésime est ce qui permet de
choisir le bon découpage.

**RPS**
Résumé par séquence. La ligne qui décrit une période d'hospitalisation en
psychiatrie : qui, où, quand, sous quelle forme de prise en charge.

**RAA**
Recueil d'activité ambulatoire. La ligne qui décrit un acte réalisé hors
hospitalisation : consultation au centre médico-psychologique, visite à domicile,
entretien infirmier. C'est le gros du volume en psychiatrie.

**RPSA, R3A**
Les versions anonymes du RPS et du RAA, destinées à l'ARS. Par construction,
elles ne contiennent ni identifiant ni date de naissance : ces informations ont
été remplacées par un code irréversible.

**VID-HOSP, VID-IPP, ANO-HOSP, HOSP-PMSI**
Les fichiers de chaînage. Ils ne décrivent pas de soins : ils font le lien entre
l'identité administrative du patient et le code anonyme qui le suivra dans les
fichiers transmis. Sans eux, impossible de savoir que deux séjours concernent la
même personne.

**FICHCOMP, FICHSUP**
Fichiers complémentaires. Ils portent ce que le fichier principal ne sait pas
dire : un médicament coûteux, un transport, une mesure d'isolement. FICHSUP a été
remplacé par FICHCOMP au 1er janvier 2021.

**RSS, RSF**
Les formats de la médecine, chirurgie et obstétrique. Pinel les reconnaît, mais
ce n'est pas son terrain principal.

---

## Les identifiants des personnes

**IPP**
Identifiant permanent du patient. Le numéro qu'un hôpital attribue à une personne
et qui ne change plus. Il est interne à l'établissement : le même patient porte un
IPP différent dans un autre hôpital.

**NDA**
Numéro de dossier administratif. Le numéro d'un passage précis, pas d'une
personne. Un patient a un IPP et autant de NDA que de venues.

**NIR**
Le numéro de sécurité sociale, quinze chiffres. C'est une donnée directement
identifiante, donc particulièrement sensible. Il sert au chaînage, jamais à
l'analyse.

**Chaînage**
Le fait de reconnaître qu'un patient vu en mars et un patient vu en octobre sont
la même personne, sans jamais transmettre son identité. On y parvient en
transformant le NIR par un calcul irréversible : deux transformations du même NIR
donnent le même code, et aucun calcul ne permet de remonter au NIR. Un chaînage
cassé compte une personne deux fois.

**Identitovigilance**
La surveillance de la qualité des identités. Quand le même IPP porte deux dates
de naissance selon les fichiers, il y a une erreur de saisie quelque part, et le
chaînage casse. L'identitovigilance consiste à repérer et arbitrer ces conflits.

**Pseudonymisation**
Remplacer les données identifiantes par des codes stables, de sorte qu'on puisse
encore suivre un patient d'un fichier à l'autre sans savoir qui il est. Ce n'est
pas l'anonymisation : un lien reste possible pour qui détient la clé. C'est
pourquoi les fichiers pseudonymisés restent des données personnelles au sens du
RGPD.

**Anonymisation**
Rendre le retour à la personne impossible, pour tout le monde, y compris pour
celui qui a fabriqué le fichier. Les formats RPSA et R3A sont anonymisés en ce
sens.

**k-anonymat**
Une mesure de protection : un fichier est k-anonyme quand chaque combinaison de
caractéristiques y apparaît au moins k fois. Avec k valant 5, personne n'est seul
de son profil, donc personne n'est reconnaissable par recoupement. Utile pour un
usage de recherche.

---

## L'activité

**Épisode de prise en charge**
Dans le format national, un acte ambulatoire n'est rattaché ni à un séjour ni à
un dossier : c'est une ligne isolée. L'épisode est une maille reconstruite qui
regroupe les venues consécutives d'un même patient, dans la même unité. Il permet
de raisonner en parcours plutôt qu'en actes.

**UM, unité médicale**
La plus petite unité d'organisation à laquelle une activité se rattache : un
service, un centre médico-psychologique, une unité d'hospitalisation. Chaque UM a
un code.

**CMP**
Centre médico-psychologique. La structure de consultation de secteur, gratuite,
qui porte l'essentiel du suivi ambulatoire en psychiatrie publique.

**Secteur**
Le découpage géographique de la psychiatrie publique française : chaque zone est
rattachée à une équipe qui en a la charge. Les codes de secteur portent une lettre
de type, G pour adulte, I pour infanto-juvénile, D pour unité pour malades
difficiles, P pour unité hospitalière spécialement aménagée, Z pour
intersectoriel.

**Exhaustivité du recueil**
La question de savoir si tout ce qui a été fait a bien été déclaré. Un acte
réalisé mais non saisi est invisible dans le PMSI, donc non financé et absent des
statistiques de santé publique.

---

## Les logiciels du décor

**DxCare**
Le dossier patient informatisé. C'est là que les soignants écrivent. Pinel ne s'y
connecte pas et ne le remplace pas.

**CPage**
Le logiciel de gestion administrative et financière de l'hôpital. Pinel ne s'y
connecte pas et ne le remplace pas.

**PMSI-Pilot, BIQuery**
Des outils de pilotage et de requêtage utilisés par les DIM. Pinel produit des
fichiers qu'on peut y charger, il ne les remplace pas.

**Druides**
Le logiciel qui produit les fichiers PMSI de psychiatrie dans certains
établissements. Quand un document parle d'un « paramétrage Druides à corriger »,
il parle d'un réglage en amont de Pinel.

---

## Le vocabulaire de Pinel

**Emplacement autorisé**
Un dossier que vous avez explicitement déclaré. Pinel refuse de lire ou d'écrire
ailleurs. Ce n'est pas une contrainte administrative : c'est ce qui garantit
qu'une erreur de manipulation ne fait pas sortir un fichier de données de santé
du périmètre prévu.

**Anomalie et niveau de gravité**
Ce qu'un contrôle signale. Quatre niveaux, du plus grave au plus anodin :
*Blocker* veut dire qu'il ne faut pas transmettre en l'état, *Error* qu'il y a une
faute à corriger, *Warning* qu'il y a un doute à lever, *Info* qu'il s'agit d'une
remarque.

**Règle apprise**
Pinel compare les fichiers avant et après les corrections du DIM, et en déduit ce
que le service corrige systématiquement. Chaque règle trouvée porte le nombre de
cas observés et un pourcentage de confiance. Rien n'est appliqué tant que le DIM
n'a pas validé la règle.

**Confiance d'une règle**
La part des cas où la correction a effectivement été appliquée. Une règle à 100 %
sur 959 cas veut dire que le DIM a fait la même correction 959 fois sans jamais
faire autrement. Une règle à 70 % veut dire qu'il y a des exceptions, donc qu'il
faut regarder avant de valider.

**Ligne signalée**
Une ligne que le modèle statistique juge susceptible d'être supprimée par le DIM,
d'après ce qu'il a observé des mois précédents. C'est une suggestion de relecture,
jamais une suppression.

**Journal d'audit**
Le relevé de ce que l'application a fait : quel écran, quelle action, quel volume,
quand. Il ne contient aucune donnée de patient, et une garde le vérifie
mécaniquement.

---

## Pour aller plus loin

- [Commencer ici](00_COMMENCER_ICI.md), si vous ne savez pas par où entrer.
- [Guide utilisateur](01_GUIDE_UTILISATEUR.md), pour l'usage au quotidien.
- [Dossier fonctionnel](02_DOSSIER_FONCTIONNEL_DIM.md), pour les règles métier.
- [Dossier technique](03_DOSSIER_TECHNIQUE_DSI.md), pour l'installation et
  l'exploitation.
- [Sécurité et conformité](04_SECURITE_ET_CONFORMITE.md), pour le RGPD et la
  protection des données.
