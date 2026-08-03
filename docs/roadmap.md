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
- [x] Relances d'expiration envoyées par mail — livré en phase 9

## Phase 4 — Disponibilités
- [x] Déclaration par jour ou créneau, avec récurrence
- [x] Calendrier d'accueil, 3 états : disponible / confirmé / indisponible
- [x] Compteurs du mois (heures, montant)
- [ ] **Reste ouvert** : la récurrence est matérialisée sur six mois glissants.
      Personne ne prolonge l'horizon aujourd'hui — à traiter quand une tâche
      planifiée existera, ou en re-déclarant à la main d'ici là.

## Phase 5 — Événements et postes (admin)
- [x] CRUD événements (client, lieu, dates, confidentialité)
      — la suppression est une annulation : statut `Cancelled`, jamais d'effacement
- [x] Découpage en postes (intitulé, effectif, horaires, tarif, tenue, brief)
- [x] Duplication d'un événement
- [ ] **Reste ouvert** : modifier le tarif ou les horaires d'un poste déjà
      accepté est permis et signalé, mais rien n'en garde trace. Un journal
      des modifications sera nécessaire le jour d'un litige.

## Phase 6 — Staffing et missions
- [x] Recherche de prestataires disponibles (date + compétence)
- [ ] **Filtre par zone non livré.** Il n'y a ni coordonnées ni géocodage :
      le rayon de déplacement déclaré est inexploitable. La ville et le rayon
      sont affichés dans la liste, à l'admin d'en juger. Décision du
      2026-08-03, à reprendre avec le géocodage.
- [x] Envoi de propositions groupées, suivi des réponses
- [x] Côté prestataire : fiche mission, accepter / refuser avec deadline
- [x] Remplacement d'un désistement
- [ ] **Reste ouvert** : une proposition dont le délai est dépassé reste
      « en attente » en base, l'expiration n'étant que calculée à la lecture.
      Le rappel J-1 est envoyé depuis la phase 9, mais rien ne relance une
      proposition restée sans réponse.

## Phase 7 — Heures
- [ ] ~~Check-in / check-out~~ — **abandonné** le 2026-08-03. Le prestataire
      déclare ses heures après la prestation, il ne badge pas : un pointage
      contredisait le principe 1 de product.md. La géolocalisation tombe avec.
- [x] Écart heures estimées / réelles
- [x] Validation par l'admin, avec correction motivée ou contestation
- [x] Saisie par l'agence quand le prestataire a oublié de déclarer
- [x] **Conséquence traitée en phase 10** : la ponctualité est remplacée par un
      indicateur de fiabilité, calculé sur les désistements et les non-réponses.

## Phase 8 — Facturation
- [x] Sélection des missions validées du mois
- [x] Génération du PDF avec toutes les mentions obligatoires
- [x] Numérotation continue par prestataire, sans trou
      — préfixe et rang de départ paramétrables, figés dès la première facture
- [x] Statuts : déposée / validée / payée, annulation sans suppression
- [x] Export comptable côté admin (CSV point-virgule, UTF-8 avec BOM)
- [ ] **Hors périmètre assumé** : seule la franchise en base de TVA est gérée.
      Un prestataire assujetti se voit refuser l'émission avec un message
      explicite plutôt que de recevoir une facture non conforme.
- [ ] **Reste ouvert** : pas d'avoir. Une facture payée ne peut donc pas être
      corrigée dans l'application.

## Phase 9 — Notifications
- [x] Mails transactionnels : proposition, confirmation, annulation, rappel
      J-1, document refusé, document expirant, heures contestées, facture à
      déposer, facture payée
- [x] Service de fond hébergé dans l'API : dépile la file toutes les 5 min,
      balaie les rappels une fois par jour
- [x] Journal des envois côté agence, avec relance d'un envoi abandonné
- [ ] ~~Push web~~ — **abandonné** avec la phase 11, qui devait apporter le
      service worker dont il dépend. Le mail reste le canal unique, ce qui est
      de toute façon la règle : rien de critique ne reposait sur le push.
- [ ] **Reste ouvert** : le service de fond suppose une instance unique. En
      cas de montée en charge, il faudra un verrou partagé — la clé d'unicité
      des notifications limite la casse mais ne le remplace pas.

## Phase 10 — Scoring
- [ ] ~~Ponctualité~~ — **sans source de données** depuis l'abandon du
      pointage. Remplacée par un indicateur de **fiabilité** calculé sur
      l'existant : désistements après engagement et propositions laissées
      sans réponse jusqu'à échéance.
- [x] Taux d'acceptation
- [x] Retours terrain : appréciation de 1 à 5 avec commentaire, **facultative**,
      saisie par l'agence sur une prestation terminée
- [x] Tri des meilleurs profils côté admin, avec le détail qui justifie le score
- [x] Score calculé à la lecture, jamais stocké — la colonne `contractors.score`,
      que rien n'avait jamais écrite depuis la phase 1, est supprimée
- [ ] **Limite connue** : l'annulation d'une mission confirmée ne distingue pas
      un désistement du prestataire d'une annulation par l'agence. Le calcul
      utilise l'état de l'événement comme garde-fou — une mission portée par un
      événement annulé ne pénalise personne — mais l'annulation d'un seul poste
      sur un événement actif pénalise encore à tort.

## Phase 11 — PWA — **abandonnée** le 2026-08-03
- [ ] ~~Manifest + installation sur écran d'accueil~~
- [ ] ~~Fiche mission consultable hors ligne~~

Décision prise en connaissance de cause : une application web *fonctionne*
hors ligne dès qu'elle a un service worker, et le push web *fonctionne*, y
compris sur iOS 16.4+ pour une application ajoutée à l'écran d'accueil. Rien
ne l'empêchait techniquement.

Conséquences à connaître :

- L'application reste inutilisable sans réseau. Or `product.md` décrit un
  prestataire qui l'ouvre « sur un parking, avec un réseau moyen » : c'est
  précisément ce cas qui n'est pas couvert.
- Pas d'installation sur écran d'accueil : `app/` s'ouvre dans un onglet de
  navigateur, avec sa barre d'adresse.
- Le push web est abandonné avec elle. Le mail reste le canal unique — ce qui
  est cohérent avec `CLAUDE.md`, où il est de toute façon le seul canal
  obligatoire.

À rouvrir si le terrain remonte des difficultés de connexion.