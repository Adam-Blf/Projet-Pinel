# Système de design

**Lecteur** : qui touche à l'interface de Pinel, aujourd'hui ou dans deux ans.

La référence qui fait foi n'est pas ce document, c'est `web/systeme.html` : une page qui charge les mêmes feuilles de style que l'application et affiche chaque composant dans chacun de ses états. Ouvrez-la à côté de l'écran sur lequel vous travaillez. Ce document dit le **pourquoi** ; la page montre le **quoi**.

---

## Le principe

L'application sert un technicien d'information médicale qui lit des colonnes de chiffres toute la journée, souvent sur un écran de bureau ordinaire, dans une pièce éclairée au néon. Tout le reste en découle : du contraste, des chiffres alignés, des couleurs qui situent, et le moins de décoration possible.

Trois règles tiennent le système :

1. **Aucune valeur en dur dans un écran.** Couleur, espacement, taille de texte, rayon, durée : tout vient d'un jeton de `css/jetons.css`. Un style en ligne est un jeton manquant.
2. **Une primitive plutôt qu'une exception.** Quand deux écrans ont besoin de la même chose, elle devient une classe dans `css/composants.css`, pas une variante locale.
3. **Ce qui n'existe pas ne se montre pas.** Un bouton qui ne fera rien, un compteur sans donnée, une action sans droit : on l'enlève ou on explique ce qui manque.

---

## Les fichiers, dans l'ordre de chargement

| Fichier | Responsabilité |
|---|---|
| `fonts/fonts.css` | Montserrat, servi en local, sous-ensembles latin et latin-ext |
| `css/jetons.css` | Toutes les valeurs : couleurs, espacements, typographie, rayons, durées, thème sombre |
| `css/mise-en-page.css` | Ossature : barre latérale, navigation, barre de titre, écrans |
| `css/composants.css` | Cartes, boutons, champs, tableaux, badges, notifications |
| `css/accents.css` | Couleur de la section ouverte, appliquée à la barre de titre et au menu |
| `css/accessibilite.css` | Contenu pour lecteurs d'écran, anneau de focus, repli du mouvement. Chargé en dernier : il prime |

---

## Jetons

**Couleurs.** Relevées sur le site institutionnel d'origine : marine `#253e80`, teal `#1d7c96`, bleu clair `#0c7db6`, ocre `#fcc764`. Chaque chantier porte sa teinte, ce qui situe l'utilisateur sans qu'il lise le titre.

Toute couleur porteuse de sens existe en deux versions. La version brute sert aux **fonds** ; la version `-ink` sert au **texte et aux bordures**, et c'est elle qui s'éclaircit en thème sombre. Sans cette séparation, le marine de la conversion disparaît sur fond sombre : c'était le cas jusqu'au 22/09/2026.

**Espacements**, raison 1,618 : `--s1` 0,35 rem à `--s6` 3,90 rem. **Typographie**, raison racine de 1,618, calée sur 16 px de texte courant : `--t-xs` 0,80 rem à `--t-2xl` 2,06 rem. L'échelle partait de 0,72 rem, soit 11,5 px sur les mentions, les chemins et les en-têtes de tableau, trop fin pour une station de travail hospitalière.

**Mouvement** : `--dur` 0,18 s, `--ease-out`. Une transition se voit sans se remarquer. Tout est coupé net pour qui a demandé moins de mouvement au niveau du système.

---

## Composants et états

Chaque composant existe dans tous ses états, et la page du système les montre côte à côte : c'est là qu'un oubli se voit.

- **Bouton** : plein, secondaire, discret, petit, indisponible, **occupé**. L'état occupé remplace l'icône par un indicateur tournant et refuse un second clic, ce qui empêche de lancer deux traitements sur le même lot.
- **Champ** : étiquette au-dessus, saisie en pleine largeur. Un contour propre (`--field-border`), distinct du filet des cartes, pour que le champ reste repérable à 3:1 même sans couleur de fond.
- **Compteur** : intitulé en petites capitales, valeur en chiffres à chasse fixe, filet de la couleur de section à gauche.
- **Tableau** : en-tête collant, hauteur bornée, chiffres à droite. Un lot d'anomalies fait deux cents lignes, l'intitulé de colonne doit rester visible.
- **Message** : information, avertissement, état vide. L'état vide dit toujours le geste qui le remplit.
- **Notification** : confirmation cinq secondes, erreur neuf secondes, avec sa propre couleur et son icône. Une erreur qu'on n'a pas eu le temps de lire n'a pas été dite.

---

## Icônes

Phosphor, graisse regular, servies en SVG local et colorées par `currentColor`. Aucune autre bibliothèque, aucun emoji en guise d'icône, aucun CDN. Un glyphe nouveau s'ajoute dans `tools/vendor_assets.py`, qui le rapatrie à version figée.

La marque de l'application vit dans `assets/icone-pinel.svg`, avec une variante simplifiée en dessous de 32 pixels. `tools/make_icon.py` en tire l'icône Windows, celle de l'onglet et les couvertures des PDF.

---

## Thème sombre

Un seul bloc de valeurs, `:root[data-theme="dark"]`, jamais dupliqué dans une media query. `js/theme.js` pose l'attribut avant le premier rendu : sans choix enregistré, Pinel suit Windows, et continue de le suivre s'il change en cours de session.

---

## Accessibilité, ce qui est tenu

- Contraste mesuré sur les deux thèmes : texte courant au-dessus de 4,5:1, pastilles d'icônes au-dessus de 4,5:1 alors que 3:1 suffirait.
- Navigation au clavier complète : lien d'évitement, anneau de focus visible partout, flèches haut et bas dans le menu selon le patron ARIA des onglets.
- L'entrée de menu active est marquée par `aria-selected`, la couleur, le filet et le fond. Jamais par la couleur seule.
- `prefers-reduced-motion` coupe toutes les transitions.

---

## Ce qu'on ne fait pas

Pas de dégradé décoratif, pas d'ombre portée pour faire joli, pas d'icône sans nom accessible quand elle est seule dans un bouton, pas de couleur inventée hors des jetons, pas de police autre que Montserrat et la pile à chasse fixe du système, pas de dépendance chargée depuis internet.
