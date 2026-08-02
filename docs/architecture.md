# Architecture — lagestion

## Stack
- API : .NET 10, Web API C#, controllers, EF Core + Npgsql
- Base : PostgreSQL 16 (Docker, port 5433 en dev)
- Fronts : React + Vite + TypeScript, Tailwind, React Router, TanStack Query
- Types partagés : générés depuis l'OpenAPI de l'API (`npm run gen:api`)

## Décisions

**Deux fronts séparés plutôt qu'une app à rôles.**
Les usages n'ont rien en commun (mobile 3 minutes sur un parking vs desktop
toute la journée). Coût : un peu de duplication (auth, client API), assumée.
Bénéfice : chaque UI est optimisée, et l'app prestataire pourra être
remplacée par du natif sans toucher au reste.

**Multi-tenant dès le départ, mono-agence en usage.**
Toute entité métier porte un `AgencyId`, filtré côté API par query filter
global EF Core. Aucun sélecteur d'agence dans l'UI tant qu'on est seuls.
Coût aujourd'hui : négligeable. Coût si ajouté plus tard : réécriture.

**Pas de workspace npm ni de package partagé.**
Duplication assumée entre `app` et `admin`. Les types viennent de l'OpenAPI,
jamais écrits à la main. À réévaluer si la duplication devient douloureuse.

**Auth JWT maison.**
Access token court en mémoire, refresh token en cookie httpOnly + SameSite.
Rôles : contractor / admin / owner. Pas de provider externe : outil interne,
pas de besoin MFA/SSO à ce stade.

**Numérotation des factures.**
Séquence par prestataire (`contractor_id` + `sequence_index`), continue et
sans trou. Contrainte d'unicité en base. Le numéro est attribué à la
génération du PDF, pas avant. Une facture annulée n'est jamais supprimée :
elle passe en statut annulée pour ne pas créer de trou.

**Stockage des fichiers.**
Documents et PDF de factures hors base : disque local en dev, S3-compatible
en prod. Seule la clé est stockée en base. Accès via URLs signées à durée
courte, jamais de fichier public.

**Géolocalisation.**
Uniquement au check-in/check-out, ponctuelle, jamais de suivi continu.
Consentement explicite. RGPD.

## Modèle de données

- **agencies** — id, name, siret, address, contact, created_at
- **users** — id, agency_id, email, password_hash, role, first_name,
  last_name, phone, is_active
- **contractors** — id, agency_id, user_id, legal_status, siret, address,
  iban, default_hourly_rate, base_city, travel_radius_km, score, notes
- **skills** / **contractor_skills** — référentiel de compétences, N-N
- **documents** — id, contractor_id, type, file_key, issued_at, expires_at,
  status, reviewed_by, reviewed_at
- **availabilities** — id, contractor_id, date, starts_at, ends_at, status
- **events** — id, agency_id, client_name, title, address, access_notes,
  starts_at, ends_at, is_confidential
- **positions** — id, event_id, label, headcount, starts_at, ends_at,
  hourly_rate, dress_code, brief
- **assignments** — id, position_id, contractor_id, status
  (proposé / accepté / refusé / confirmé / annulé), proposed_at,
  response_deadline, responded_at
- **timesheets** — id, assignment_id, planned_hours, checkin_at, checkout_at,
  actual_hours, status, validated_by, validated_at
- **invoices** — id, agency_id, contractor_id, number, sequence_index,
  period_start, period_end, issued_at, total_amount, vat_exempt, status,
  pdf_key
- **invoice_lines** — id, invoice_id, assignment_id, label, hours,
  unit_rate, amount
- **notifications** — id, agency_id, user_id, channel (mail/push), template,
  payload, status, sent_at

Toutes les tables métier portent `agency_id`, `created_at`, `updated_at`.
Aucune suppression physique sur invoices, timesheets et assignments : statut
annulé, pour la traçabilité.

## Points ouverts
- Prestataire rattaché à une seule agence ou à plusieurs, le jour où on vend
  l'outil ? (aujourd'hui : une seule)
- Fournisseur d'envoi de mails à choisir
- Génération PDF : bibliothèque à arbitrer au moment de la phase 8