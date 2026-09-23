# Pinel - Sécurité et conformité

Version 1.0.0. Destinataires : Direction des Ressources Numériques et, si elle
est sollicitée, la personne déléguée à la protection des données du GHT Psy Sud
Paris.

Ce document décrit la conformité par construction du logiciel. Il ne constitue
pas un avis juridique : la qualification finale revient au DPO.

---

## 1. Données traitées

| Élément | Contenu |
|---|---|
| Nature | Données de santé à caractère personnel, article 9 du RGPD |
| Catégories | Identifiant permanent du patient, date de naissance, numéro de séjour, unité médicale, codes diagnostics, éléments de chaînage dérivés du NIR |
| Personnes concernées | Patients pris en charge dans les établissements du GHT, dont des patients hospitalisés sans consentement |
| Origine | Fichiers déjà extraits par le système d'information, jamais de saisie directe |
| Finalité | Fiabiliser la transmission réglementaire aux tutelles et l'analyse d'activité du DIM |
| Destinataires | Aucun. Les résultats restent sur le poste du DIM |
| Sous-traitant | Aucun |

Aucune donnée nouvelle n'est collectée. L'application change les moyens
techniques d'un traitement existant, le recueil PMSI, inscrit au registre de
l'établissement.

---

## 2. Ce qui ne sort jamais du poste

Pinel ne se connecte à aucun système du système d'information, ni CPage, ni
DxCare, ni PMSI-Pilot, ni BIQuery. Il lit des fichiers déjà extraits et produit
des fichiers. Le dépôt aux tutelles reste manuel.

Le service HTTP interne n'écoute que sur 127.0.0.1, jamais sur une interface
réseau. Aucune télémétrie, aucune recherche de mise à jour, aucun appel de
contrôle ou de vérification vers l'extérieur. L'interface n'appelle aucun CDN :
polices et icônes sont embarquées, l'application fonctionne à l'identique sur un
poste coupé du réseau.

La conversion d'une page HTML en PDF charge la page dans un navigateur embarqué
(WebView2) qui exécute les scripts présents dans le HTML. Si le HTML contient des
appels externes (chargement de Web Fonts depuis un serveur tiers, traceurs, etc.),
ceux-ci seraient effectués. Le DIM doit donc soumettre uniquement des HTML
nettoyés et sans dépendances externes.

---

## 3. Minimisation et durée de conservation

Les fichiers lus et l'index patient vivent en mémoire du processus. Ils sont
effacés par la commande Réinitialiser et disparaissent à la fermeture de
l'application. Aucune base de données n'est créée.

Seuls les fichiers que l'utilisateur demande explicitement à produire sont
écrits sur disque, dans le dossier de sortie qu'il a désigné.

Le fichier de réglages ne contient que des chemins. Le journal d'audit ne
contient aucune donnée patient.

---

## 4. Emplacements autorisés

L'application ne lit et n'écrit que dans des emplacements déclarés par
l'utilisateur, ou imposés par la DRN via la variable `PINEL_WORKSPACE`. Tout le
reste est refusé, y compris les remontées par `..`, les flux de données alternés
NTFS, les chemins étendus, et les liens ou jonctions NTFS pointant hors zone.

Cette dernière protection mérite d'être signalée : sur un partage du GHT où
plusieurs comptes écrivent, la création d'une jonction ne demande aucun droit
d'administration. Les chemins sont donc résolus jusqu'à leur cible réelle avant
d'être autorisés.

---

## 5. Protection du service interne

| Mesure | Mise en œuvre |
|---|---|
| Isolation | Écoute sur la boucle locale uniquement |
| Authentification | Jeton aléatoire de 32 octets par session, exigé sur toutes les routes sauf le test de disponibilité |
| Réattribution de nom DNS | Liste blanche stricte de l'en-tête Host |
| Origines | Restreintes à l'origine interne de l'interface |
| En-têtes | `Cache-Control: no-store`, `X-Content-Type-Options: nosniff` |
| Injection de formule | Valeurs CSV commençant par un caractère interprété par Excel neutralisées |
| Fuite technique | La plupart des endpoints capturent les erreurs courantes. Les exceptions non gérées peuvent renvoyer un détail technique selon la configuration ASP.NET Core. Recommandation : activer le mode Production et tester avec la gestion d'erreurs activée. |
| Écrasement | Si un fichier de sortie de même nom existe déjà, il sera écrasé. Le DIM doit gérer l'organisation de ses dossiers de sortie. |

L'exigence d'un en-tête d'autorisation déclenche une vérification préalable du
navigateur : une page web tierce ne peut pas déclencher d'action sur le service
sans connaître le jeton de session.

---

## 6. Traçabilité, article 30 du RGPD

Le journal d'audit enregistre, pour chaque opération sensible : horodatage UTC,
poste, compte Windows, route, méthode, code de retour et volumétrie. Rotation
quotidienne, format JSON Lines lisible dans un éditeur de texte.

Il répond à la question qui a fait quoi et quand, jamais sur qui. Aucun chemin
de fichier, aucun identifiant patient, aucun contenu n'y transite.

Emplacement : `%LOCALAPPDATA%\Pinel\audit-AAAAMMJJ.log`. La durée de
conservation reste à fixer avec la DRN.

---

## 7. Anonymisation

Un contrôle dédié détecte les identifiants restés en clair dans les formats
censés être anonymes, RPSA, R3A, RAPSS, RAPSS-HAD, SSRHA, SSRHA-HAD. Une telle
présence est un incident à signaler, la génération du fichier étant à reprendre
depuis le logiciel de groupage.

L'export assaini produit une copie d'un fichier ATIH dans laquelle la date de
naissance pivot remplace les valeurs divergentes, à largeur fixe préservée. Le
fichier d'origine n'est jamais modifié.

---

## 8. Droits des personnes

| Droit | Exerçable | Modalité |
|---|---|---|
| Information | Oui | Par l'information générale de l'établissement sur le traitement PMSI |
| Accès | Oui | Par la procédure existante de l'établissement, Pinel n'est pas une source distincte |
| Rectification | Oui | La correction se fait dans le système d'information source, puis se propage aux extractions |
| Effacement | Limité | Obligation légale de transmission, exception prévue par le RGPD |
| Opposition | Limité | Même motif, traitement relevant d'une mission d'intérêt public |
| Portabilité | Sans objet | Base légale non contractuelle et non consentie |

---

## 9. Analyse d'impact

La grille de référence retient neuf critères, deux suffisent à rendre une
analyse d'impact obligatoire.

| Critère | Présent | Commentaire |
|---|---|---|
| Données sensibles, article 9 | Oui | Données de santé, diagnostics psychiatriques |
| Personnes vulnérables | Oui | Patients en psychiatrie, dont des soins sans consentement |
| Traitement à grande échelle | Oui | Volumétrie annuelle du PMSI de territoire |
| Croisement de jeux de données | Oui | Rapprochement des fichiers d'activité et du fichier de chaînage |
| Évaluation ou scoring | Non | Aucun profilage de patient |
| Décision automatisée | Non | Aucune décision de soins n'est prise par l'application |
| Surveillance systématique | Non | Aucun suivi de personnes |
| Usage innovant | Non | Aucune fonction d'intelligence artificielle dans cette version |
| Entrave à un droit ou à un service | Non | Sans effet sur la prise en charge |

Quatre critères sont réunis. La position proposée : l'analyse d'impact est due
au niveau du traitement PMSI existant, pas au niveau de l'application prise
isolément. Pinel n'ouvre aucune finalité nouvelle et ne crée aucune collecte, il
change les moyens techniques d'un traitement déjà inscrit au registre. Ce qui
appelle, en pratique, la mise à jour de la fiche de registre du traitement PMSI
et l'actualisation de l'analyse d'impact existante sur les volets sécurité et
destinataires.

Si l'établissement ne dispose pas encore d'analyse d'impact sur son traitement
PMSI, la question dépasse cette application et mérite d'être posée pour
elle-même.

---

## 10. Risques et mesures

| Risque | Vraisemblance | Gravité | Mesure |
|---|---|---|---|
| Copie de données de santé hors du poste DIM | Moyenne | Élevée | Confinement aux emplacements déclarés, journal d'audit |
| Lecture hors zone par lien ou jonction NTFS | Faible | Élevée | Résolution des liens jusqu'à leur cible avant autorisation |
| Réidentification depuis un fichier anonymisé | Faible | Élevée | Contrôle dédié de détection d'identifiant en clair |
| Persistance non maîtrisée | Faible | Moyenne | Aucune écriture automatique, aucune base, index en mémoire |
| Accès depuis un autre poste | Très faible | Élevée | Écoute sur la boucle locale, jeton de session, liste blanche d'hôtes |
| Altération d'un fichier de transmission | Faible | Moyenne | Le fichier source n'est jamais modifié, les corrections produisent une copie |
| Donnée fausse et silencieuse dans un export | Faible | Élevée | Refus explicite plutôt que troncature ou saturation, descriptif postérieur jamais appliqué, 108 tests |

---

## 11. Mesures organisationnelles

Sur un poste partagé, utiliser Réinitialiser entre deux utilisateurs : l'index
patient en mémoire est vidé.

Les fichiers exportés sont écrits en clair dans le dossier de sortie. Leur
protection relève des droits NTFS du partage et de la politique de
l'établissement, hors périmètre de l'application. Aucune purge automatique n'est
prévue. Si une politique de conservation ou de chiffrement du partage est
souhaitée, elle est à définir par la DRN.

---

## 12. Points à trancher avec la DRN

| Point | Décision attendue |
|---|---|
| Durée de conservation du journal d'audit | À fixer |
| Mise à jour du registre des traitements | DPO, avec le DIM |
| Actualisation de l'analyse d'impact PMSI existante | DPO, avec le DIM |
| Signature de l'exécutable par un certificat de l'établissement | DRN |
| Emplacements imposés par stratégie de groupe | DRN |
| Usage éventuel de `PINEL_BRIDGE_TOKEN`, déconseillé sur poste partagé | DRN |
| Politique de conservation des fichiers exportés | DRN |

---

## 13. Contact

Adam BELOUCIF
Apprenti ingénieur PMSI, Département d'Information Médicale
Téléphone 01 42 11 70 60, courriel adam.beloucif@psysudparis.fr
