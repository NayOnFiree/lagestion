# lagestion

App de gestion de staffing événementiel. Une agence place des prestataires
indépendants sur des événements.

## Structure
- `api/`    .NET 10, Web API C#, EF Core + Npgsql
- `app/`    React + Vite + TS — front prestataire, mobile-first
- `admin/`  React + Vite + TS — back-office, desktop

## Commandes
- Base : `docker compose up -d` (Postgres, port 5433)
- API : `cd api/LaGestion.Api && dotnet run --launch-profile http` → :5080
- App : `cd app && npm run dev` → :5173
- Admin : `cd admin && npm run dev` → :5174
- Types API : `cd app && npm run gen:api` (API démarrée)

## Règles non négociables
- **Multi-tenant** : toute entité métier porte un `AgencyId`. Tout accès aux
  données est filtré dessus, sans exception.
- **Vocabulaire** : l'entité s'appelle `Contractor` dans le code, "prestataire"
  dans l'UI. Jamais "road" (jargon), jamais "auto-entrepreneur" comme rôle
  (c'est un statut juridique, simple champ sur la fiche).
  Interdit dans le code et l'UI : "salaire", "employé", "contrat de travail",
  "planning imposé". On dit : rémunération, prestation, mission, tarif horaire,
  disponibilités. Risque de requalification URSSAF.
- **Facturation** : le prestataire reste l'émetteur de sa facture, avec SA
  propre numérotation continue. L'app pré-remplit et génère le PDF, elle ne
  renumérote jamais. Pas d'auto-facturation.
- **Notifications** : le mail est le canal fiable et obligatoire. Le push web
  est un confort (non garanti sur iOS). Rien de critique ne repose sur le push.
- Pas de workspace npm ni de package partagé : `app` et `admin` sont
  indépendants, la duplication est assumée. Les types API viennent de
  l'OpenAPI, jamais écrits à la main.

## Design
Toute création ou modification d'UI dans `app/` ou `admin/` suit le skill
`.claude/skills/lagestion-ui/SKILL.md`. Le charger avant d'écrire un
composant. Typo Inter uniquement, shadcn/ui pour les primitives.

## Méthode de travail
- Avant toute tâche non triviale : proposer un plan et attendre validation.
- Ne jamais ajouter de dépendance ou de fonctionnalité non demandée.
- En cas d'ambiguïté : s'arrêter et poser la question, ne rien inventer.
- Après chaque tâche : cocher la ligne correspondante dans `docs/roadmap.md`.

## Documents
- `docs/product.md` — spec fonctionnelle (écrans et features)
- `docs/architecture.md` — modèle de données et décisions techniques
- `docs/roadmap.md` — liste des tâches, dans l'ordre