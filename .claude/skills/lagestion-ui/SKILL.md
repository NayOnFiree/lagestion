---
name: lagestion-ui
description: Design system de lagestion. À charger dès qu'on crée ou modifie
  un composant, une page ou un style dans app/ ou admin/.
---

# Design system lagestion

Objectif : une UI qui ressemble à un outil métier utilisé tous les jours,
pas à une démo. Référence mentale : Linear, Vercel, Height. Pas Bootstrap,
pas Material, pas de landing page.

## Typographie — Inter, uniquement

Inter via @fontsource-variable/inter, jamais de CDN Google Fonts.
Aucune autre famille. Pas de serif, pas de display, pas de police mono
décorative : pour les chiffres on utilise Inter avec `tabular-nums`.

font-feature-settings: "cv11", "ss01"; font-variant-numeric: tabular-nums;

Échelle, et rien d'autre :
- 12px / 500 — labels, en-têtes de colonnes, méta
- 13px / 400 — texte dense de tableau
- 14px / 400 — texte courant (taille par défaut)
- 15px / 500 — titres de carte, libellés forts
- 20px / 600 — titre de page
- 28px / 600 — chiffre héro d'un KPI

Interlignage 1.4 sur le texte, 1.2 sur les titres. Letter-spacing : -0.01em
au-dessus de 20px, 0 partout ailleurs. Jamais de `uppercase` avec
letter-spacing élargi : c'est la signature du template générique.

## Couleurs

Trois niveaux de texte, pas quatre :
--text-primary:   #0F1115
--text-secondary: #5B6472
--text-muted:     #8A93A0

Surfaces :
--bg:      #FFFFFF
--surface: #F7F8F9   (zones secondaires, en-têtes de tableau)
--border:  #E6E8EB

Accent unique :
--accent:      #1F6F5C
--accent-weak:  #EAF3F0

Statuts (usage strict, jamais décoratif) :
--success: #1F6F5C   --warning: #A8630B
--danger:  #B42318   --info:    #3C5A99

Règles : une seule couleur d'accent par écran. Le vert d'accent est réservé
aux actions primaires et aux états confirmés. Un tableau n'est jamais
coloré ligne par ligne. Pas de dégradé, nulle part, jamais.

## Espacement et bordures

Grille de 4px. Valeurs autorisées : 4, 8, 12, 16, 24, 32, 48.
Rayons : 6px (boutons, inputs, badges), 8px (cartes), 12px (modales).
Jamais de `rounded-full` sauf avatars et pastilles de statut.
Jamais de `rounded-2xl`.

Séparation : bordure 1px OU fond différent, jamais les deux sur le même
élément. Ombres : une seule, `0 1px 2px rgba(16,24,40,0.05)`, réservée aux
éléments flottants (dropdown, popover, modale). Une carte posée dans la page
n'a pas d'ombre, elle a une bordure.

## Composants

Primitives shadcn obligatoires : Button, Input, Select, Dialog, Sheet,
Table, Tabs, Badge, Card, Form, Toast, Dropdown, Calendar.
Si tu écris un composant qui duplique une primitive shadcn existante,
c'est une erreur — utilise la primitive.

Boutons : hauteur 36px (32px en dense), padding horizontal 12px.
Variantes : `default` (accent), `outline`, `ghost`. Une seule action
primaire visible par écran. Pas d'icône dans les boutons de texte sauf si
elle porte du sens (ajouter, télécharger, filtrer).

Formulaires : react-hook-form + zod, systématiquement. Label au-dessus,
message d'erreur en dessous en 12px danger. Jamais de placeholder à la
place d'un label.

États vides : ce sont de vrais composants — un titre en 15px/500, une
phrase d'explication en 14px secondary, une action. Pas d'illustration,
pas d'emoji, pas de grande icône grise centrée.

Chargement : skeletons aux dimensions réelles du contenu attendu.
Pas de spinner plein écran.

## Interdits (les tells d'une UI générée)

- Dégradés, `backdrop-blur` décoratif, glassmorphism
- Ombres portées larges (`shadow-lg`, `shadow-xl`)
- Emojis dans l'interface
- Couleurs Tailwind brutes (`bg-blue-500`, `text-gray-400`) : tout passe
  par les variables CSS
- Icônes dans chaque item de menu et chaque cellule de tableau
- Cartes de statistiques identiques alignées par 4 sans hiérarchie
- Titres centrés dans une page d'application
- Animations au-delà de 150ms, ou sur autre chose que opacity/transform
- Bordures colorées à gauche des cartes
- Plus d'une famille de police, ou une police de fallback visible
- Texte en `text-gray-500` sur fond `gray-100`

## Vocabulaire dans l'UI

Prestataire, mission, disponibilité, rémunération, tarif horaire, facture.
Jamais : road, salaire, employé, planning imposé, contrat de travail.
Les libellés sont en français, sans majuscule à chaque mot, sans point final.

## Spécifique à app/ (prestataire, responsive)

Conçue mobile d'abord, mais elle doit tenir sur desktop sans avoir l'air
d'une app téléphone agrandie.

- Breakpoints : < 768px mobile · 768–1279px tablette · ≥ 1280px desktop
- Conteneur : pleine largeur en mobile (padding 16px), max-width 1120px
  centré au-delà de 1280px. Jamais une colonne étroite perdue au milieu
  d'un écran large.
- Navigation : barre basse fixe à 4 entrées en mobile → barre haute
  horizontale en tablette → sidebar rétractable en desktop.
- Mise en page : une colonne en mobile → deux colonnes en desktop
  (liste à gauche, détail à droite pour missions, factures, documents).
  Le calendrier passe de la vue liste compacte à la vue mois complète.
- Action principale : bouton fixe en bas pleine largeur en mobile →
  bouton aligné en haut à droite de la page en desktop. Pas de bouton
  flottant sur grand écran.
- Cible tactile 44px en mobile, 36px acceptable en desktop.
- Rien d'important ne dépend d'un survol (le survol n'existe pas au doigt).
- Contraste renforcé partout : l'écran est lu en extérieur, au soleil.

## Spécifique à admin/ (back-office, desktop)

- Shell : SidebarProvider > AppSidebar + SidebarInset (shadcn).
- Densité : lignes de tableau à 40px, padding cellule 8px 12px,
  page en p-6. C'est un outil de travail, pas une brochure : on préfère
  voir 25 lignes sans scroller que 8 lignes aérées.
- Tous les nombres, montants, heures et dates : `tabular-nums`.
- En-têtes de tableau collants, tri sur les colonnes pertinentes,
  actions de ligne dans un menu au survol, pas cinq boutons visibles.
- Filtres dans une barre unique au-dessus du tableau, jamais dans un
  panneau latéral qui pousse le contenu.
- Les montants sont alignés à droite, le texte à gauche.

## Auto-vérification avant de valider un composant

1. Une seule action primaire ? 
2. Toutes les couleurs viennent des variables CSS ?
3. Les primitives shadcn sont utilisées là où elles existent ?
4. Les chiffres sont en tabular-nums ?
5. Aucun élément de la liste des interdits ?
6. Les espacements sont sur la grille de 4 ?
7. Le vocabulaire respecte la règle "prestataire, jamais road/salaire" ?

Si un point échoue, corrige avant de valider.