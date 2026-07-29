# Pinel - Dossier technique

Version 1.0.0. Destinataire : Direction des Ressources Numériques du GHT Psy Sud
Paris. Objet : répondre aux questions posées avant toute exécution, et permettre
un test en environnement isolé.

---

## 1. Réponses directes aux questions posées

| Question de la DRN | Réponse |
|---|---|
| Objectif | Rendre exploitables les fichiers PMSI à largeur fixe, et produire les sorties demandées par le cahier des charges du DIM du 22 décembre 2025 |
| Cas d'usage | Conversion mensuelle d'un lot en CSV, calcul d'épisodes ambulatoires, mise à plat de la structure du GHT, nettoyage et contrôle des fichiers complémentaires, identitovigilance |
| Utilisateurs | Techniciens d'information médicale et médecins du DIM, quelques postes, tous au DIM |
| Langage | C# sur .NET 8, interface HTML et CSS servie localement, aucune dépendance à Python |
| Accès réseau | Aucun. Un service HTTP tourne dans le processus, lié à 127.0.0.1 uniquement |
| Stockage des données | Aucune base. Les données lues vivent en mémoire, seuls les fichiers exportés à la demande sont écrits |
| Flux vers le système d'information | Aucun. Pinel ne se connecte ni à CPage, ni à DxCare, ni à PMSI-Pilot, ni à BIQuery |
| Base de données | Aucune. Aucun client ODBC, Oracle ou SQL Server n'est embarqué |

---

## 2. Architecture

Un seul processus, un seul fichier exécutable.

| Composant | Rôle | Technologie |
|---|---|---|
| Pinel.Core | Lecture des formats, exports, épisodes, structure, contrôles, identitovigilance | Bibliothèque .NET 8 |
| Pinel.Desktop | Fenêtre, interface, service HTTP interne, mode ligne de commande | WPF, WebView2, ASP.NET Core |
| Interface | Écrans, tableaux, formulaires | HTML, CSS et JavaScript, sans framework |
| Pinel.Tests | Vérification automatisée | xUnit, 108 tests |

Chaîne d'appel : l'interface s'exécute dans un composant WebView2, appelle le
service HTTP local, qui appelle la bibliothèque métier, qui lit et écrit des
fichiers. Aucun maillon ne sort du poste.

```
Utilisateur
   |
Fenetre WPF  -- WebView2 -->  Interface HTML locale
   |                              |
   |                        HTTP 127.0.0.1:8787 + jeton de session
   |                              |
   +--- boites de dialogue        v
        Windows            Service interne  -->  Coeur metier  -->  Fichiers
```

### 2.1 Dépendances

| Dépendance | Usage | Origine |
|---|---|---|
| .NET 8 | Exécution | Embarqué dans l'exécutable, rien à installer |
| Microsoft.Web.WebView2 | Rendu de l'interface | Composant Microsoft, présent sur Windows 11 et Windows 10 à jour |
| ClosedXML | Lecture et écriture des classeurs Excel | Bibliothèque libre, licence MIT |

Aucune autre dépendance tierce. Aucune télémétrie, aucune recherche de mise à
jour, aucun serveur de licence.

### 2.2 Interface sans dépendance externe

L'interface n'appelle aucun CDN. Les polices et les icônes sont embarquées dans
l'exécutable et extraites au premier lancement. L'affichage est donc identique
sur un poste coupé du réseau, ce qui se vérifie en une minute lors de la
recette.

---

## 3. Flux et accès

| Flux | Origine | Destination | Port | Nature |
|---|---|---|---|---|
| Interface vers service interne | Processus Pinel | 127.0.0.1 | 8787 TCP | Interne au poste, jeton exigé |
| Lecture des fichiers PMSI | Processus Pinel | Emplacements déclarés par l'utilisateur | Sans objet | Lecture |
| Écriture des sorties | Processus Pinel | Dossier de sortie déclaré | Sans objet | Écriture |

Le service est lié à l'adresse de bouclage. Il n'écoute sur aucune interface
réseau et n'est joignable depuis aucun autre poste, y compris sur le même
sous-réseau.

### 3.1 Protection du service interne

- jeton aléatoire de 32 octets généré à chaque démarrage, exigé sur toutes les
  routes sauf le test de disponibilité ;
- liste blanche de l'en-tête Host, qui ferme la réattribution de nom DNS ;
- origines restreintes à l'origine interne de l'interface ;
- en-têtes `Cache-Control: no-store` et `X-Content-Type-Options: nosniff` sur
  toutes les réponses.

Le jeton peut être imposé par la variable d'environnement `PINEL_BRIDGE_TOKEN`.
Ce mécanisme est déconseillé sur un poste partagé : une variable machine est
lisible par tout compte local. À n'utiliser que si la DRN en exprime le besoin,
et alors en variable utilisateur.

---

## 4. Confinement des fichiers

L'application ne lit et n'écrit que dans des emplacements déclarés :

- l'espace de travail local `%LOCALAPPDATA%\Pinel\travail`, toujours autorisé ;
- les dossiers ajoutés par l'utilisateur dans l'écran Emplacements autorisés,
  dossiers locaux, lecteurs réseau montés ou partages UNC ;
- les dossiers ajoutés par la DRN via la variable `PINEL_WORKSPACE`, séparés par
  des points-virgules, posables par stratégie de groupe.

Tout chemin hors de ces emplacements est refusé. Sont également refusés les
remontées par `..`, les flux de données alternés NTFS, les chemins étendus
préfixés `\\?\` et `\\.\`.

Un point mérite d'être signalé à la DRN : les liens et les jonctions NTFS sont
résolus avant contrôle. Un lien posé dans un partage autorisé et pointant hors
zone est refusé. La création d'une jonction ne demande pas de droits
d'administration, ce contrôle est donc nécessaire sur un partage où plusieurs
comptes écrivent.

---

## 5. Stockage local

| Emplacement | Contenu | Donnée patient |
|---|---|---|
| `%LOCALAPPDATA%\Pinel\settings.json` | Emplacements autorisés, dossier de sortie, dossier des descriptifs | Non |
| `%LOCALAPPDATA%\Pinel\formats\` | Descriptifs de format déposés par le DIM | Non |
| `%LOCALAPPDATA%\Pinel\travail\` | Espace de travail par défaut | Seulement si l'utilisateur y exporte |
| `%LOCALAPPDATA%\Pinel\audit-AAAAMMJJ.log` | Journal d'audit | Non |
| `%LOCALAPPDATA%\Pinel\interface\{version}\` | Interface extraite au premier lancement | Non |

Aucune base de données n'est créée. Les fichiers lus et l'index patient vivent
en mémoire du processus et disparaissent à la fermeture. Seuls les fichiers que
l'utilisateur demande explicitement à produire sont écrits.

---

## 6. Journal d'audit

Une ligne JSON par opération sensible : horodatage UTC, nom du poste, compte
Windows, route appelée, méthode, code de retour, volumétrie de la réponse.
Rotation quotidienne. Écriture non bloquante.

Aucune donnée patient n'y figure, par construction : la fonction
d'enregistrement n'accepte que ces champs, aucun chemin de fichier ni contenu
n'y transite.

Sont tracées les opérations de scan, de traitement, d'export, de calcul
d'épisodes, de consultation et de modification des conflits d'identité, de
contrôle, de structure, de fichiers complémentaires, d'inspection de ligne et de
conversion en PDF.

---

## 7. Mode ligne de commande

Pour un traitement planifié ou un test sans interface :

```
Pinel.exe --headless --scan <dossier> [--validate] [--export <fichier.csv>] [--json <rapport.json>]
```

Codes de sortie : 0 si aucun blocage, 1 en cas de paramètre invalide ou d'erreur
d'entrée-sortie, 2 si au moins une anomalie de niveau Blocker est relevée. Ce
mode n'ouvre ni fenêtre ni composant WebView2. Il se déclare dans le
Planificateur de tâches Windows en action directe sur l'exécutable, sans script
intermédiaire, les EDR hospitaliers bloquant fréquemment les scripts.

---

## 8. Prérequis et déploiement

Windows 10 ou 11 en 64 bits, composant WebView2, environ 250 Mo d'espace disque,
4 Go de mémoire. Aucun droit d'administration, aucune installation.

L'exécutable se copie dans un répertoire du profil utilisateur ou dans un
partage en lecture seule. Il se distribue seul : aucun fichier ne doit
l'accompagner.

Désinstallation : supprimer l'exécutable et le dossier `%LOCALAPPDATA%\Pinel`.

---

## 9. Qualité

108 tests automatisés couvrent la couche métier : reconnaissance des formats,
lecture positionnelle, sélection des descriptifs par année, export CSV,
épisodes, fichiers complémentaires, structure, confinement des chemins,
identitovigilance, journal d'audit. Au dernier passage, 108 réussis, 0 échec.

Les tests portent en priorité sur les cas où une erreur serait silencieuse : un
descriptif inadapté appliqué sans le dire, une quantité tronquée, deux
identifiants d'épisode confondus, une date lue dans la mauvaise convention.

---

## 10. Protocole de recette en environnement isolé

Proposition, à adapter aux procédures de la DRN.

1. Vérifier l'empreinte SHA-256 de l'exécutable, fournie avec la livraison.
2. Analyser le binaire avec les outils de l'établissement.
3. Lancer l'application sur un poste ou une machine virtuelle sans accès réseau,
   avec un jeu de fichiers de test anonymisés. L'interface doit s'afficher
   normalement, ce qui prouve l'absence de dépendance externe.
4. Observer les connexions sortantes pendant une session complète. Attendu :
   aucune.
5. Vérifier les ports en écoute. Attendu : 8787 sur 127.0.0.1 uniquement, libéré
   à la fermeture.
6. Déclarer un emplacement de travail, puis tenter d'ouvrir un fichier hors de
   cet emplacement, et vérifier le refus.
7. Exécuter le mode ligne de commande avec `--validate --json rapport.json`,
   contrôler le rapport et le code de sortie.
8. Vérifier le contenu écrit dans `%LOCALAPPDATA%\Pinel`, en particulier
   l'absence de donnée patient dans le journal d'audit et dans les réglages.

---

## 11. Limites connues

- L'application est mono-poste : ni comptes, ni droits, ni travail à plusieurs
  sur un même lot.
- Le certificat de signature est à fournir par l'établissement. Le binaire livré
  est accompagné de son empreinte SHA-256, à recalculer après signature.
- Une capacité de conversion d'une page HTML en PDF existe dans le service
  interne mais n'est reliée à aucun bouton de l'interface : elle est présente,
  non exposée.
- Les positions de champ des formats ATIH ne sont pas fournies avec
  l'application. Elles proviennent des descriptifs officiels que le DIM dépose.
- Les emplacements et les noms d'écrans peuvent être renommés à la demande de la
  DRN ou du DIM, sans conséquence technique.

---

## 12. Contact

Adam BELOUCIF
Apprenti ingénieur PMSI, Département d'Information Médicale
Téléphone 01 42 11 70 60, courriel adam.beloucif@psysudparis.fr
