# Roadmap

On avance dans l'ordre. Une phase = une branche = une validation.

## Phase 0 — Correctifs socle
- [x] Base de données locale documentée et reproductible (Docker reporté au
      déploiement)
- [x] Node LTS à jour + .nvmrc, plus d'avertissement EBADENGINE
- [x] global.json épinglant le SDK .NET 10
- [x] Microsoft.EntityFrameworkCore.Design ajouté
- [x] .env ignoré par git, .env.example versionné
- [x] ProblemDetails + sérialisation camelCase vérifiés
- [x] README à jour (base de données, ports, raison du TypeScript épinglé en 5.9)

## Phase 1 — Modèle de données
- [x] Trancher : un prestataire est-il rattaché à une seule agence ou à
      plusieurs ? → **une seule**, `contractors.agency_id` en clé étrangère
      directe (voir architecture.md)
- [x] Entités : agencies, users, contractors, skills, contractor_skills,
      documents, availabilities, events, positions, assignments,
      timesheets, invoices, invoice_lines, notifications
- [x] AgencyId + query filter global EF Core sur toutes les entités métier
- [x] Migration initiale
- [x] Seed de dev : 1 agence, 1 admin, 3 prestataires, 1 événement

## Phase 2 — Authentification
- [x] JWT access token + refresh token en cookie httpOnly
- [x] Rôles : contractor / admin / owner
- [x] Login sur les deux fronts, routes protégées, déconnexion
- [ ] **Reste ouvert** : deux onglets ouverts sur le même compte rafraîchissent
      en parallèle et présentent le même jeton ; le second passe pour un rejeu
      et révoque la chaîne. Corrigé dans un onglet (une seule requête en vol),
      pas entre onglets. Correctif prévu : tolérance de quelques secondes côté
      serveur sur la réutilisation du jeton immédiatement précédent.

## Phase 3 — Profil prestataire et documents
- [x] Profil : identité, statut juridique, SIRET, coordonnées, RIB, tarif
- [x] Coffre à documents (upload, date de validité, statut de validation)
- [x] Indicateur de complétude du dossier
- [x] Côté admin : validation des documents, alertes d'expiration
- [ ] **Reste ouvert** : les relances d'expiration sont affichées, pas envoyées.
      L'envoi de mails relève de la phase 9.

## Phase 4 — Disponibilités
- [ ] Déclaration par jour ou créneau, avec récurrence
- [ ] Calendrier d'accueil, 3 états : disponible / confirmé / indisponible
- [ ] Compteurs du mois (heures, montant)

## Phase 5 — Événements et postes (admin)
- [ ] CRUD événements (client, lieu, dates, confidentialité)
- [ ] Découpage en postes (intitulé, effectif, horaires, tarif, tenue, brief)
- [ ] Duplication d'un événement

## Phase 6 — Staffing et missions
- [ ] Recherche de prestataires disponibles (date + compétence + zone)
- [ ] Envoi de propositions groupées, suivi des réponses
- [ ] Côté prestataire : fiche mission, accepter / refuser avec deadline
- [ ] Remplacement d'un désistement

## Phase 7 — Heures
- [ ] Check-in / check-out
- [ ] Écart heures estimées / réelles
- [ ] Validation par l'admin

## Phase 8 — Facturation
- [ ] Sélection des missions validées du mois
- [ ] Génération du PDF avec toutes les mentions obligatoires
- [ ] Numérotation continue par prestataire, sans trou
- [ ] Statuts : déposée / validée / payée, annulation sans suppression
- [ ] Export comptable côté admin

## Phase 9 — Notifications
- [ ] Mails transactionnels (proposition, confirmation, rappel J-1,
      document expiré, facture à déposer, paiement)
- [ ] Push web en complément

## Phase 10 — Scoring
- [ ] Ponctualité, taux d'acceptation, retours terrain
- [ ] Tri des meilleurs profils côté admin

## Phase 11 — PWA
- [ ] Manifest + installation sur écran d'accueil
- [ ] Fiche mission consultable hors ligne