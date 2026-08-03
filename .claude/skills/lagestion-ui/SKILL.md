---
name: lagestion-ui
description: Design system de lagestion. À charger dès qu'on crée ou modifie
  un composant, une page ou un style dans app/ ou admin/.
---

# Design system lagestion

Outil métier utilisé tous les jours. Densité, alignement, hiérarchie.
Maquettes de référence : `docs/design/` (index dans son README).
Elles se lisent, elles ne se copient jamais.

## Ce qui porte le design
La donnée elle-même. Aucun ornement : la qualité vient de l'écart de taille
entre les niveaux d'information, de l'alignement, des filets fins et du
rythme vertical. Un écran se lit en trois temps : de quoi il s'agit, ce
qu'il faut décider, les détails.

## Typographie — Inter, exclusivement
32/600 titre de page · 28/600 chiffre clé · 15/500 titre de bloc
14/400 texte courant · 13/400 texte dense de tableau
12/600 #5B6472 titre de section · 12/500 labels et badges
Interlignage 1.4 texte, 1.2 titres. `tabular-nums` sur tous les chiffres,
montants, heures et dates.
Casse de phrase normale : majuscule en début de phrase et aux noms propres.
Pas de Title Case, jamais de texte tout en minuscules, jamais de majuscules
espacées.

## Couleurs
--text-primary #0F1115 · --text-secondary #5B6472 · --text-muted #8A93A0
--bg #FFFFFF · --surface #F7F8F9 · --border #E6E8EB
--accent #1F6F5C · --accent-weak #EAF3F0 · --accent-hover #17513F
--success #1F6F5C · --warning #A8630B · --danger #B42318 · --info #3C5A99

Règle d'accent, stricte : l'accent n'apparaît QUE sur le bouton primaire,
l'entrée active de la navigation, un badge de statut confirmé, et un chiffre
clé unique par écran. Jamais dans un paragraphe, jamais pour surligner des
mots, jamais sur un titre. Aucun dégradé, nulle part.

## Structure de page
- Bandeau de titre : titre 32/600, métadonnées 14/400 secondaire en dessous,
  action primaire alignée à droite, filet 1px en dessous, padding 32px.
- Corps en deux colonnes : colonne principale 720px max, rail droit 360px
  collant (seule zone à fond --surface). Le rail remplit l'écran large sans
  étirer le texte.
- Sections : pas de carte englobant la page. Titre 12/600 secondaire, filet,
  contenu. 32px entre sections.
- Cartes réservées aux objets répétables (mission, prestataire, facture).
- Blocs de synthèse : label à gauche secondaire, valeur à droite en
  tabular-nums, filets entre les lignes, montant final en 28/600.
- Badges : 12/500, texte à la couleur de statut, fond très clair, rayon 6px.
- Aucune zone vide de plus de 25% de la largeur.

## Comportement de mise en page
- Sidebar : fixe, pleine hauteur de fenêtre, ne défile pas avec le contenu.
  Sa liste défile si elle dépasse. Le bloc compte utilisateur reste en bas.
- Rail droit : collant, se fige à 24px du haut de la zone de contenu une
  fois le bandeau de titre dépassé, défile indépendamment s'il dépasse.
- Bandeau de titre : défile normalement, non collant.
- Seule la colonne principale porte le défilement de la page.
- Sous 768px : sidebar remplacée par la barre basse, rail droit repositionné
  au-dessus de la colonne principale et non collant.

## Espacement
Grille de 4px : 4, 8, 12, 16, 24, 32, 48.
Rayons 6px (boutons, inputs, badges), 8px (cartes), 12px (modales).
Filet 1px OU fond différent, jamais les deux. Une carte dans la page a un
filet, pas d'ombre. Ombre réservée aux éléments flottants :
0 1px 2px rgba(16,24,40,0.05).

## Navigation
Sous 768px : barre basse fixe à 4 entrées.
À partir de 768px : sidebar verticale fixe, 240px (rétractable à 64px).
Logo en haut, entrées icône + libellé alignées à gauche, entrée active sur
fond --accent-weak et texte --accent, compte utilisateur en bas.
Aucune barre de navigation horizontale en haut, jamais.

## Composants
Primitives shadcn/ui uniquement : Button, Input, Select, Dialog, Sheet,
Table, Tabs, Badge, Card, Form, Toast, Dropdown, Calendar.
Boutons 36px. UNE seule action primaire visible par écran, jamais dupliquée.
react-hook-form + zod sur tous les formulaires. Label au-dessus du champ,
jamais de placeholder à la place d'un label.
États vides : titre 15/500, phrase 14/400 secondaire, une action. Pas
d'illustration, pas d'emoji, pas de grande icône grise.
Chargement : squelettes aux dimensions réelles du contenu attendu, jamais
de spinner plein écran.

## Spécifique à app/ (prestataire, responsive)
Mobile : une colonne, cible tactile 44px, action primaire en bas pleine
largeur. Desktop : conteneur centré 1120px max, deux colonnes quand c'est
pertinent, action primaire en haut à droite. Contraste renforcé, l'écran est
lu en extérieur. Un écran répond à une seule question.
Dates en toutes lettres côté mobile ("jeu. 14 mars, 8h–18h").

## Spécifique à admin/ (back-office, desktop)
Pleine largeur à côté de la sidebar, pas de conteneur centré. Rail droit
uniquement sur les écrans de travail sur un objet unique, jamais sur les
écrans de tableau. Lignes 40px, cellules 8px/12px, 12 à 25 lignes visibles.
Montants et heures à droite en tabular-nums, texte à gauche, total en pied.
Barre de filtres unique au-dessus du tableau. Actions de ligne dans un menu
en fin de ligne. Sélection multiple avec barre d'actions groupées en bas.

## Vocabulaire
Prestataire, mission, disponibilité, rémunération, tarif horaire, facture.
Jamais : road, salaire, employé, planning imposé, contrat de travail.
Libellés en français, casse de phrase, sans point final.

## Interdits
Dégradés · glassmorphism · ombres larges · emojis · couleurs Tailwind brutes ·
accent dans le texte courant · texte tout en minuscules · carte englobant la
page · barre de navigation horizontale · icône dans chaque ligne · quatre
cartes de stats identiques sans hiérarchie · titre centré · illustration dans
un état vide · plus d'une police · boutons dupliqués · animation au-delà de
150ms ou sur autre chose qu'opacity/transform.

## Auto-vérification
1. Une seule action primaire, non dupliquée ?
2. Toutes les couleurs viennent des variables CSS ?
3. Primitives shadcn utilisées là où elles existent ?
4. Chiffres en tabular-nums ?
5. Casse de phrase, pas de tout-minuscule ?
6. L'accent n'apparaît que là où il est autorisé ?
7. Espacements sur la grille de 4 ?
8. Aucun élément de la liste des interdits ?