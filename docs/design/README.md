# Maquettes de référence — lagestion

Exports Claude Design. **Référence visuelle uniquement.**

## Règle d'usage — à respecter strictement

Ces fichiers ne sont PAS du code source. Ils sont produits par un outil de
maquettage : styles en ligne, aucune classe, aucun composant, runtime maison
(`support.js`). Ils ignorent nos tokens, Tailwind et shadcn/ui.

- ✅ Les LIRE pour relever les valeurs exactes : espacements, largeurs de
  colonnes, tailles et graisses de police, ordre des blocs, libellés,
  contenu des tableaux.
- ❌ Ne JAMAIS les copier, importer, adapter ou convertir dans `app/` ou
  `admin/`. Chaque écran est reconstruit avec les composants partagés du
  projet (`AppShell`, `PageHeader`, `RightRail`, `SectionBlock`,
  `SummaryList`, `KeyFigure`, `StatusBadge`...).
- Les valeurs du skill `.claude/skills/lagestion-ui/SKILL.md` priment en cas
  d'écart avec une maquette, sauf mention explicite dans ce README.

`support.js` doit rester dans ce dossier, à côté des fichiers HTML : chaque
export le charge en chemin relatif (`./support.js`). Ne pas le déplacer,
ne pas le renommer, ne pas l'éditer.

Pour visualiser une maquette : ouvrir le fichier HTML dans un navigateur.

## App prestataire

| # | Écran | Fichier |
|---|---|---|
| 01 | Accueil | `app-01-accueil.html` |
| 01 | Accueil — états | `app-01-accueil-etats.html` |
| 02 | Missions — liste | `app-02-missions-liste.html` |
| 02 | Missions — états (vide, chargement, erreur) | `app-02-missions-liste-etats.html` |
| 03 | Fiche mission | `app-03-fiche-mission.html` |
| 03 | Fiche mission — états | `app-03-fiche-mission-etats.html` |
| 04 | Disponibilités | `app-04-disponibilites.html` |
| 04 | Disponibilités — états | `app-04-disponibilites-etats.html` |
| 05 | Profil | `app-05-profil.html` |
| 06 | Coffre à documents | `app-06-documents.html` |
| 07 | Pointage | _à exporter_ |
| 08 | Factures — liste | `app-08-factures-liste.html` |
| 09 | Établir une facture | `app-09-facture-nouvelle.html` |
| 10 | Notifications | `app-10-notifications.html` |
| 11 | Menu / réglages | _à exporter_ |
| 12 | Connexion | `app-12-connexion.html` |

## Back-office admin

| # | Écran | Fichier |
|---|---|---|
| 01 | Tableau de bord | `admin-01-tableau-de-bord.html` |
| 02 | Événements — liste | `admin-02-evenements-liste.html` |
| 03 | Fiche événement | `admin-03-fiche-evenement.html` |
| 04 | Création d'un événement | `admin-04-evenement-creation.html` |
| 05 | Staffing d'un poste | `admin-05-staffing.html` |
| 06 | Suivi des réponses | `admin-06-suivi-reponses.html` |
| 07 | Réseau — liste des prestataires | `admin-07-reseau.html` |
| 08 | Fiche prestataire | `admin-08-fiche-prestataire.html` |
| 09 | Heures à valider | `admin-09-heures-a-valider.html` |
| 10 | Factures reçues | `admin-10-factures-recues.html` |
| 11 | Réglages — utilisateurs | `admin-11-reglages-utilisateurs.html` |

## Écarts connus entre les maquettes et le skill

Relevés dans les exports, à arbitrer avant la refonte :

- Sidebar : 232px dans les maquettes, 240px dans le skill.
- Taille de texte de base : 13px dans les maquettes, 14px dans le skill.
- Entrées de navigation : hauteur 30px, rayon 5px dans les maquettes ;
  le skill prévoit la grille de 4px et un rayon de 6px.
- Inter est chargé depuis Google Fonts dans les exports ; dans le projet il
  passe par `@fontsource-variable/inter`.

Tant que l'arbitrage n'est pas tranché, appliquer les valeurs du skill.
