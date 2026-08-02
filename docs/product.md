# Spec fonctionnelle — lagestion

## Le métier
Une agence de staffing événementiel place des prestataires indépendants sur
des événements (montage, régie, manutention, accueil). Aujourd'hui tout se
gère par mails, SMS et tableurs. L'app remplace ça.

## Utilisateurs
- **Prestataire** : indépendant (micro-entreprise, EURL, SASU...). Usage
  mobile, sessions courtes, souvent sur le terrain avec un réseau moyen.
- **Admin** : staffe, valide, facture. Usage desktop, sessions longues.
- **Owner** : admin + réglages de l'agence.

## Principes
1. Le prestataire n'est pas un salarié. Aucune UI ne doit ressembler à de la
   gestion de personnel : il propose ses disponibilités, il accepte ou refuse
   une mission, il facture. Il ne "pointe pas au travail".
2. Un prestataire ouvre l'app 3 minutes sur un parking. Chaque écran répond à
   une seule question.
3. Rien de critique ne passe uniquement par une notification push.

---

## App prestataire

### Accueil
Calendrier du mois, 3 états visuels : disponibilité déclarée, mission
confirmée, indisponible. En-tête : heures du mois et montant facturé.
Bloc "prochaine mission" cliquable. Bandeau d'alerte si un document est
expiré ou si une facture est à déposer.

### Disponibilités
Déclaration par jour ou par créneau, avec récurrence ("tous les week-ends").
Modifiable tant qu'aucune mission n'est confirmée sur le créneau.

### Missions
Liste : propositions reçues / confirmées / passées.
Fiche mission : intitulé, contexte et objectif, ce qu'il y a à faire, dates
et horaires, lieu et modalités d'accès (quai, badge, code), tarif horaire,
estimation du nombre d'heures, tenue exigée, référent terrain.
Le nom du client peut être masqué (événements sous confidentialité).
Boutons accepter / refuser avec une deadline de réponse.

### Profil et documents
Identité, statut juridique, SIRET, coordonnées, RIB, tarif par défaut.
Coffre à documents : pièce d'identité, attestation de vigilance URSSAF,
RC pro, permis, habilitations. Chaque document a une date de validité et un
statut de validation. Indicateur de complétude du dossier.

### Heures
Check-in / check-out sur la mission. Écart entre heures estimées et réelles,
soumis à validation de l'agence.

### Facturation (dépôt assisté)
Le prestataire voit ses missions validées du mois, coche celles à facturer,
l'app pré-remplit les lignes (mission, heures, tarif, total) et génère le PDF.
Il vérifie et dépose.
Le prestataire reste l'émetteur : la numérotation suit SA propre séquence,
continue et sans trou. L'app ne renumérote jamais.
Mentions générées automatiquement : identité et adresse de l'émetteur, SIRET,
identité du client, date, numéro, désignation des prestations, quantité,
prix unitaire, total, "TVA non applicable, art. 293 B du CGI" si franchise,
délai de paiement, pénalités de retard, indemnité forfaitaire de recouvrement.
Liste des factures avec statut : déposée / validée / payée.

### Notifications
Mail systématiquement, push en complément : proposition de mission,
confirmation, rappel J-1, changement d'horaire, document à renouveler,
facture à déposer, paiement effectué.

---

## Back-office admin

### Dashboard
Missions à venir, postes non pourvus, documents expirés dans le réseau,
factures en attente, heures à valider.

### Événements
Création d'un événement (client, lieu, dates), découpage en postes (intitulé,
nombre de personnes, horaires, tarif horaire, tenue, brief).
Duplication pour les événements récurrents.

### Staffing
Recherche de prestataires disponibles à une date, filtrable par compétence,
zone géographique et score. Envoi de propositions groupées, suivi des
réponses en temps réel, remplacement rapide d'un désistement.

### Réseau
Fiches prestataires, compétences, historique des missions, score, blacklist.
Suivi de conformité documentaire avec relances automatiques.

### Heures et facturation
Validation des heures réelles, écart prévu/réel, réception des factures,
contrôle, passage en payée, export comptable.

### Réglages
Utilisateurs et rôles, tarifs par défaut, modèles de mails, infos de l'agence.

---

## Hors périmètre v1
Auto-facturation (mandat de facturation), facturation électronique
Factur-X/PDP, paiements intégrés, application mobile native, gestion de
stock matériel, multi-agences visible dans l'UI.