# Architecture — lagestion

## Stack
- API : .NET 10, Web API C#, controllers, EF Core + Npgsql
- Base : PostgreSQL 16 (service local, port 5432 en dev ; Docker en prod)
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

**Un prestataire est rattaché à une seule agence.** Tranché le 2026-08-02.
`contractors.agency_id` est une clé étrangère directe et non nullable. Un
prestataire qui travaillerait pour deux agences aurait deux fiches. Passer
en N-N plus tard imposerait de trancher, pour chaque attribut, s'il est
global ou par agence (tarif, compétences, score, numérotation de facture) —
on ne paie pas ce coût tant que le besoin n'existe pas.

**`agency_id` est dénormalisé sur toutes les tables métier**, y compris les
tables filles qui pourraient le déduire de leur parent (`documents`,
`availabilities`, `positions`, `assignments`, `timesheets`,
`invoice_lines`, `contractor_skills`). C'est la seule forme où chaque
`DbSet` est filtré indépendamment : un oubli de jointure ne peut pas faire
fuiter de données d'une autre agence. Coût : une colonne redondante et
l'obligation de la garder cohérente avec le parent, posée automatiquement à
l'insertion par le `SaveChanges` du `DbContext`.

**Le référentiel de compétences est propre à chaque agence.** `skills`
porte un `agency_id` comme le reste, chaque agence garde son vocabulaire
métier. Un socle commun pourra être extrait plus tard si la duplication
devient gênante.

**Pas de workspace npm ni de package partagé.**
Duplication assumée entre `app` et `admin`. Les types viennent de l'OpenAPI,
jamais écrits à la main. À réévaluer si la duplication devient douloureuse.

**Auth JWT maison.**
Access token court en mémoire, refresh token en cookie httpOnly + SameSite.
Rôles : contractor / admin / owner. Pas de provider externe : outil interne,
pas de besoin MFA/SSO à ce stade.

**Un événement s'annule, il ne se supprime pas.** Tranché le 2026-08-03.
`events.status` vaut `Draft`, `Published` ou `Cancelled`. Un événement a
existé, des prestataires ont pu être sollicités : l'effacer réécrirait
l'histoire. L'annulation horodate et bascule d'office les propositions en
cours en `Cancelled`, sans les supprimer non plus. Les postes, eux, restent
supprimables tant qu'aucune proposition ne les référence.

**Le tarif et les horaires d'un poste restent modifiables après
acceptation.** Tranché le 2026-08-03, contre l'avis initial. Le risque est
réel — un prestataire a accepté des conditions précises, les changer
unilatéralement est exactement ce qui nourrit une requalification — mais la
souplesse a été jugée prioritaire. Compensation : l'API renvoie la liste des
prestataires engagés dès qu'un tarif ou un horaire change, et le back-office
la montre avant de fermer le formulaire. Rien n'est bloqué, rien n'est
silencieux. Si un jour un litige survient, il faudra un journal des
modifications : il n'existe pas.

**Numérotation des factures.**
Séquence par prestataire (`contractor_id` + `sequence_index`), continue et
sans trou. Contrainte d'unicité en base. Le numéro est attribué à la
génération du PDF, pas avant. Une facture annulée n'est jamais supprimée :
elle passe en statut annulée pour ne pas créer de trou.

**Stockage des fichiers.**
Documents et PDF de factures hors base : disque local en dev, S3-compatible
en prod. Seule la clé est stockée en base. Accès via URLs signées à durée
courte, jamais de fichier public.

**Pas de pointage, pas de géolocalisation.** Tranché le 2026-08-03, revient
sur la décision initiale. Le prestataire est indépendant : il **déclare** les
heures qu'il a effectuées, il ne badge pas. Le principe 1 de `product.md` le
disait déjà — « il ne pointe pas au travail » — et un check-in horodaté le
contredisait. Sans pointage, la géolocalisation n'a plus d'objet : collecter
des coordonnées « au cas où » serait de la donnée personnelle sans finalité,
ce que le RGPD proscrit. Le jour où un litige de présence surviendra, on
saura ce qu'on cherche à prouver et on l'implémentera pour ça.

Conséquence à connaître : la **ponctualité**, listée comme critère de scoring
en phase 10, n'a plus de source de données.

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

**Envoi des mails par SMTP, via MailKit.** Tranché le 2026-08-03. Une
bibliothèque, n'importe quel fournisseur : Brevo, Mailjet, OVH ou Scaleway se
configurent par hôte, port et identifiants sans toucher au code. Une API
transactionnelle aurait offert un meilleur suivi de délivrabilité, au prix
d'un choix figé dans le code.

**Les rappels périodiques sont déclenchés par un service hébergé dans l'API**
plutôt que par un cron externe : rien à installer ni à configurer sur le
serveur. Contrepartie assumée — le service suppose une instance unique, et il
faudra un verrou partagé le jour d'une montée en charge horizontale.

## Points ouverts
- Génération PDF : bibliothèque à arbitrer au moment de la phase 8