# Commencer ici

Vous venez d'ouvrir la documentation de Pinel. Cette page vous dit en cinq
minutes ce qu'est ce logiciel, s'il est fait pour vous, et où aller ensuite.

Si un mot vous arrête, il est expliqué dans le [glossaire](06_GLOSSAIRE.md).

---

## Le problème, en une image

Les hôpitaux décrivent leur activité à l'État dans des fichiers texte où tout est
collé, sans virgule ni tabulation pour séparer les informations. Voici une ligne
de la même forme que les vraies, avec des valeurs volontairement factices :

```
000000000AAAAAAAAAAAAAAAAAAAA19000101M00000000000000
|________||__________________||______||||__________|
 FINESS    identifiant patient  naiss.  sexe  date
```

Personne ne lit une telle ligne à l'oeil nu. Pourtant tout y est, et on le
retrouve parce que chaque information occupe une position fixée d'avance : les
neuf premiers caractères sont toujours le numéro de l'établissement, les vingt
suivants l'identifiant du patient, et ainsi de suite jusqu'au bout.

Ces positions ne sont pas les vraies. Elles changent selon le format et selon
l'année, et elles se lisent dans les documents publiés par l'agence technique de
l'information sur l'hospitalisation, jamais de mémoire.

Ouvert dans Excel, ce fichier donne une colonne unique et illisible. Pour le
travailler, il faut le découper. C'est ce que fait Pinel, et c'est le point de
départ de tout le reste.

---

## Ce que Pinel fait

| | |
|---|---|
| **Découper** | Transforme les fichiers de l'État en tableaux lisibles dans Excel, une colonne par information. |
| **Contrôler** | Passe une dizaine de vérifications sur vos fichiers et vous dit ce qui bloquerait une transmission. |
| **Regrouper** | Reconstruit des parcours de soins à partir d'actes isolés, ce que le format national ne sait pas faire. |
| **Apprendre** | Observe les corrections que votre service fait chaque mois et vous propose de les systématiser. Rien n'est appliqué sans votre accord. |
| **Ranger** | Met en forme le fichier de structure de votre groupement, et nettoie les classeurs de transport. |

## Ce que Pinel ne fait pas

Il ne se connecte à **aucun** de vos logiciels. Il lit des fichiers que vous lui
donnez, et il écrit des fichiers dans un dossier que vous désignez. Rien d'autre.

Il ne remplace ni le dossier patient, ni la gestion administrative, ni vos outils
de pilotage. Il ne transmet rien aux tutelles : le dépôt reste votre geste.

Et il ne sort jamais de votre poste. Pas de serveur, pas de nuage, pas de compte
en ligne.

---

## Est-ce fait pour vous ?

**Oui**, si vous travaillez dans un département d'information médicale, en
psychiatrie surtout, et que vous manipulez des fichiers PMSI à la main ou dans
des classeurs Excel maison.

**Oui**, si vous êtes à la direction des systèmes d'information d'un hôpital et
qu'on vous demande d'évaluer cet outil.

**Non**, si vous cherchez un logiciel qui produit les fichiers PMSI. Pinel
travaille en aval : il lit ce que votre chaîne produit déjà.

---

## Démarrer, en cinq étapes

1. **Installer.** Lancez le fichier d'installation. Il s'installe dans votre
   profil utilisateur, sans droit d'administration. Les mises à jour arrivent
   ensuite toutes seules, par petits correctifs.

2. **Déclarer votre établissement.** Ouvrez l'écran des réglages et saisissez le
   numéro FINESS de votre établissement. Pinel ne suppose aucun hôpital en
   particulier : c'est vous qui le lui dites.

3. **Déclarer vos dossiers.** Pinel refuse de lire ou d'écrire où que ce soit
   tant que vous ne l'avez pas autorisé. Déclarez le dossier où sont vos fichiers,
   et celui où vous voulez recevoir les résultats. Cette contrainte est
   volontaire : elle vous protège d'une fausse manipulation sur des données de
   santé.

4. **Convertir un premier lot.** Désignez un dossier, lancez l'analyse, puis
   l'export. Vous obtenez un fichier Excel par fichier d'origine.

5. **Regarder les contrôles.** Avant toute transmission, passez par l'écran des
   contrôles. Il vous dit ce qui est bloquant et ce qui ne l'est pas.

Le détail de chaque étape, avec les copies d'écran, est dans le
[guide utilisateur](01_GUIDE_UTILISATEUR.md).

---

## Où aller ensuite

| Vous êtes | Lisez |
|---|---|
| Technicien ou médecin du DIM | [Guide utilisateur](01_GUIDE_UTILISATEUR.md), puis le [dossier fonctionnel](02_DOSSIER_FONCTIONNEL_DIM.md) quand vous voudrez comprendre les règles appliquées. |
| Direction des systèmes d'information | [Dossier technique](03_DOSSIER_TECHNIQUE_DSI.md) : architecture, flux, ports, prérequis, déploiement. |
| Délégué à la protection des données | [Sécurité et conformité](04_SECURITE_ET_CONFORMITE.md) : RGPD, minimisation, mesures de sécurité. |
| Curieux, ou perdu dans le vocabulaire | [Glossaire](06_GLOSSAIRE.md). |
| Concerné par l'apparence du logiciel | [Système de design](05_SYSTEME_DE_DESIGN.md). |

---

## Deux choses à savoir avant de vous engager

**La licence.** Pinel se vérifie hors ligne, contre le numéro FINESS de votre
établissement. Sans licence valide, la lecture et les contrôles continuent de
fonctionner : c'est l'écriture des fichiers produits qui s'arrête. Une tolérance
de trente jours évite qu'un renouvellement tardif ne bloque un service un jour de
transmission.

**D'où vient ce logiciel.** Pinel a été écrit dans un département d'information
médicale, pour ce département, à partir de ses fichiers réels et de ses
corrections réelles. Ce n'est pas un produit conçu en laboratoire : chaque
contrôle existe parce qu'une anomalie précise est passée au travers un jour.

Le nom vient de Philippe Pinel, le médecin qui a fait retirer les chaînes des
aliénés à Bicêtre en 1793.
