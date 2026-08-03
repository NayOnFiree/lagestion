# LaGestion

Gestion de staffing événementiel : une agence place des prestataires
indépendants sur des événements.

Deux interfaces distinctes, une seule API.

| Dossier  | Rôle                                              | URL de dev              |
| -------- | ------------------------------------------------- | ----------------------- |
| `api/`   | API REST .NET 10 (controllers) + EF Core / Npgsql | <http://localhost:5080> |
| `app/`   | Front prestataire, mobile-first                   | <http://localhost:5173> |
| `admin/` | Back-office, desktop                              | <http://localhost:5174> |

L'entité s'appelle `Contractor` dans le code et « prestataire » dans l'UI.

Les deux fronts sont **indépendants** : pas de workspace npm, pas de package
partagé, pas d'outillage monorepo. La duplication (client HTTP, types générés)
est assumée à ce stade.

---

## Prérequis

| Outil                                                     | Version   | Épinglée par  |
| --------------------------------------------------------- | --------- | ------------- |
| [.NET SDK](https://dotnet.microsoft.com/download)          | 10.0.302+ | `global.json` |
| [Node.js](https://nodejs.org)                              | 24.18.1   | `.nvmrc`      |
| [PostgreSQL](https://www.postgresql.org/download/windows/) | 16.x      | —             |

PostgreSQL 16 s'installe **localement**, en service. Pas de Docker en
développement (voir plus bas).

`global.json` interdit la sélection d'un SDK .NET plus ancien : si seul le
SDK 8 est présent, `dotnet` échoue avec « A compatible .NET SDK was not
found » plutôt que de compiler silencieusement avec la mauvaise version.

`.nvmrc` fixe la version de Node : `nvm use` (ou `fnm use`) à la racine du
dépôt. Node 24 est la LTS active ; en dessous de 22.22, `react-router` 8
émet un avertissement `EBADENGINE`.

---

## Ordre de démarrage

1. Service PostgreSQL démarré, et `scripts/init-db.sql` passé une fois
2. `cd api/LaGestion.Api && dotnet run --launch-profile http`
3. `cd app && npm run dev` et/ou `cd admin && npm run dev`

L'API ne crée ni le rôle ni la base : sans initialisation, `/health` répond
**503**.

---

## Base de données en développement

La base tourne sur un **PostgreSQL 16 installé localement en service**, sur
le port **5432** par défaut. Pas de conteneur en développement.

### Initialisation, une fois par machine

Le rôle et la base sont créés par un script versionné, `scripts/init-db.sql`,
à exécuter en superutilisateur :

```bash
psql -h localhost -p 5432 -U postgres -d postgres -f scripts/init-db.sql
```

Sous Windows, si `psql` n'est pas dans le `PATH` :

```powershell
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -p 5432 -U postgres -d postgres -f scripts/init-db.sql
```

Le script est **idempotent** : le relancer sur une installation déjà
initialisée ne casse rien et ne réécrit pas le mot de passe existant.

### Identifiants

En clair et volontairement triviaux — ils ne servent qu'en local et sont
identiques pour toute l'équipe : base `lagestion`, utilisateur `lagestion`,
mot de passe `lagestion`. Ils correspondent à la chaîne de connexion de
`api/LaGestion.Api/appsettings.Development.json`.

### Se connecter à la main

```bash
psql -h localhost -p 5432 -U lagestion -d lagestion
```

### Remise à zéro

```bash
psql -h localhost -p 5432 -U postgres -d postgres \
  -c "DROP DATABASE IF EXISTS lagestion;" -c "DROP ROLE IF EXISTS lagestion;"
psql -h localhost -p 5432 -U postgres -d postgres -f scripts/init-db.sql
```

> **Docker sera réintroduit pour le déploiement en production (VPS Ubuntu),
> avec l'image `postgres:16-alpine` pour rester sur la même version majeure
> qu'en dev.** Le `docker-compose.yml` de développement a été retiré ; il
> reste consultable dans l'historique git (`git show 9de3ad7:docker-compose.yml`).

---

## Lancer l'API

```bash
cd api/LaGestion.Api
dotnet run --launch-profile http
```

- API : <http://localhost:5080>
- Health : <http://localhost:5080/health>
- Swagger UI : <http://localhost:5080/swagger>
- Document OpenAPI : <http://localhost:5080/openapi/v1.json>

`GET /health` renvoie **200** si la connexion PostgreSQL est établie,
**503** sinon :

```json
{ "status": "healthy", "database": true, "timestamp": "2026-08-02T13:40:00Z" }
```

En développement l'API est servie en HTTP simple (pas de redirection HTTPS),
pour que les fronts n'aient pas de certificat auto-signé à approuver.

### Erreurs et sérialisation

- Les erreurs sortent au format **ProblemDetails** (RFC 9457) :
  `AddProblemDetails()` côté services, `UseExceptionHandler()` +
  `UseStatusCodePages()` dans le pipeline. Une exception non gérée comme un
  simple 404 renvoient un `application/problem+json`.
- Le JSON est sérialisé en **camelCase** (`PropertyNamingPolicy` et
  `DictionaryKeyPolicy` posés explicitement dans `Program.cs`), alors que le
  C# reste en PascalCase.

### Secrets

`appsettings.Development.json` est **versionné** : il ne contient que les
identifiants de dev créés par `scripts/init-db.sql`, identiques pour toute
l'équipe. Ce ne sont pas des secrets.

Les `.env` des fronts sont ignorés par git ; seuls les `.env.example` sont
versionnés.

**La clé de signature des jetons est le seul vrai secret du projet.** Elle
n'est nulle part dans le dépôt et l'API refuse de démarrer sans elle. À poser
une fois par machine :

```bash
cd api/LaGestion.Api
dotnet user-secrets set "Jwt:SigningKey" "<au moins 32 caractères aléatoires>"
dotnet user-secrets list
```

En production, la configuration vient des variables d'environnement
(`Jwt__SigningKey`, `ConnectionStrings__Postgres`, `Cors__AllowedOrigins__0`, …).

---

## Authentification

Connexion par **code agence + adresse électronique + mot de passe**. Le code
agence est nécessaire parce qu'avant toute identité il n'existe aucun moyen de
savoir sur quelle agence filtrer : une même adresse peut exister dans deux
agences.

| Endpoint        | Rôle                                                     |
| --------------- | -------------------------------------------------------- |
| `POST /auth/login`   | ouvre une session                                   |
| `POST /auth/refresh` | échange le cookie contre un nouvel access token     |
| `POST /auth/logout`  | révoque le refresh token courant                    |
| `GET /auth/me`       | compte associé à l'access token présenté            |

- **Access token** : JWT signé HS256, 15 minutes, renvoyé en JSON et gardé
  **en mémoire** par les fronts. Jamais dans `localStorage`.
- **Refresh token** : 30 jours, en cookie `httpOnly` + `Secure` + `SameSite=Lax`,
  limité au chemin `/auth`. Stocké **haché** en base — une fuite de la table ne
  permet pas de rejouer les jetons.
- **Rotation** : chaque rafraîchissement consomme le jeton et en émet un
  nouveau. Représenter un jeton déjà consommé est traité comme un vol :
  toute la chaîne active du compte est révoquée.
- **Rôles** : `Contractor`, `Admin`, `Owner`, exposés en policies `contractor`,
  `admin` (Admin ou Owner), `owner`.
- L'agence courante vient d'un claim signé, jamais du client. Sur une requête
  anonyme elle vaut `Guid.Empty` et le filtre global ne laisse rien passer.

---

## Pièces justificatives

Les fichiers ne sont **jamais** en base : seule leur clé de stockage l'est.
En développement ils atterrissent dans `storage/` à la racine, ignoré par git ;
en production, derrière une implémentation S3-compatible.

- **Formats acceptés** : PDF, JPEG, PNG, 10 Mo au maximum. Le type est déduit
  des **octets d'en-tête**, pas de l'extension ni du `Content-Type` annoncé :
  un fichier texte renommé en `.pdf` est refusé.
- **Consultation** : le contenu n'est servi que par un lien signé HMAC valable
  deux minutes. Un navigateur qui ouvre un document ou charge une image ne pose
  pas d'en-tête `Authorization` — le lien porte donc lui-même son autorisation.
  La clé de signature est dérivée de `Jwt:SigningKey` avec une étiquette
  distincte : un seul secret à provisionner, deux clés indépendantes.
- **Expiration** : jamais stockée comme statut, toujours calculée depuis
  `expires_at`. Un statut figé demanderait un traitement périodique, et une
  pièce affichée comme valide alors qu'elle est périmée serait pire que pas de
  statut du tout.

---

## Événements et postes

Un événement porte le contexte — client, lieu, dates, modalités d'accès — et
se découpe en **postes** : un intitulé, un effectif recherché, un créneau, un
tarif horaire, une tenue et un brief. Un poste peut déborder du créneau de
l'événement : le montage arrive avant, le démontage repart après.

**Un événement s'annule, il ne se supprime pas.** Statut `Draft`, `Published`
ou `Cancelled`. L'annulation horodate et bascule les propositions en cours en
annulées, sans rien effacer. Un poste, lui, reste supprimable tant qu'aucun
prestataire n'y a été sollicité ; au-delà l'API répond 409.

**La duplication décale, elle ne resollicite pas.** L'événement et ses postes
sont recopiés en appliquant le même écart de dates à tout le monde, ce qui
préserve les durées et l'enchaînement des postes. La copie repart en
brouillon. Les propositions de mission ne sont **jamais** recopiées.

Le tarif et les horaires d'un poste restent modifiables après acceptation.
L'API renvoie alors la liste des prestataires engagés, que le back-office
affiche avant de refermer le formulaire : à l'agence de les prévenir.

---

## Scoring

Trois indicateurs, **tous calculés à la lecture** — aucun score n'est stocké.
Un score stocké serait faux entre deux recalculs et obligerait à se souvenir
de le rafraîchir après chaque événement qui l'affecte.

| Indicateur | Calcul |
| --- | --- |
| Acceptation | propositions acceptées ÷ propositions auxquelles le prestataire a répondu |
| Fiabilité | 1 − (désistements + non-réponses à échéance) ÷ propositions reçues |
| Note moyenne | moyenne des appréciations, de 1 à 5 |

Le **score sur 100** est la moyenne des seuls indicateurs qui ont des données.
Un prestataire sans appréciation n'est pas pénalisé pour cette absence : elle
est ignorée, pas comptée comme un zéro. Un prestataire sans aucun historique
n'a **pas de score** — ce n'est ni bon ni mauvais.

L'API expose le détail de chaque indicateur, numérateur et dénominateur
compris : un score doit pouvoir s'expliquer, pas seulement s'afficher.

**La ponctualité n'est pas calculée.** Elle figurait dans la spec, mais rien
n'enregistre l'heure d'arrivée depuis l'abandon du pointage. La fiabilité la
remplace : c'est le signal que l'agence cherche vraiment — « puis-je compter
dessus ».

### Appréciations

Facultatives. L'agence note une prestation terminée de 1 à 5 avec un
commentaire ; valider des heures n'a jamais exigé de note. Noter à nouveau
corrige la note précédente au lieu d'en empiler une seconde.

### Limite connue

L'annulation d'une mission confirmée ne distingue pas un désistement du
prestataire d'une annulation par l'agence. Le calcul utilise l'état de
l'événement comme garde-fou — une mission portée par un **événement annulé**
ne pénalise personne — mais annuler un seul poste sur un événement toujours
actif pénalise encore le prestataire à tort.

---

## Notifications

**Le mail est le canal fiable et obligatoire.** Le push web est reporté à la
phase 11, où le service worker de la PWA sera mis en place ; rien de critique
n'en dépend.

Rien n'est envoyé dans le fil de la requête : une panne du serveur de mail ne
doit pas faire échouer une action métier qui, elle, a réussi. Les messages
sont mis en file dans la table `notifications`, dans la **même transaction**
que l'action qui les déclenche — un message ne part donc jamais sur une
opération qui n'a pas abouti.

Un service de fond hébergé dans l'API dépile la file toutes les cinq minutes
et, une fois par jour, balaie les rappels : mission du lendemain, pièce
expirée ou expirant sous 30 jours, prestations validées non facturées.

**Le balayage quotidien est idempotent** grâce à une clé d'unicité par
message : repasser sur les mêmes données ne renvoie rien. La clé des pièces
expirantes porte le mois, une pièce qui reste périmée est donc rappelée une
fois par mois, pas tous les jours.

### Configuration

```bash
dotnet user-secrets set "Email:SmtpHost" "smtp-relay.brevo.com"
dotnet user-secrets set "Email:SmtpUser" "..."
dotnet user-secrets set "Email:SmtpPassword" "..."
```

L'envoi passe par **MailKit**, volontairement agnostique du fournisseur :
Brevo, Mailjet, OVH ou Scaleway se configurent par hôte, port et identifiants
sans toucher au code.

**Sans `SmtpHost` configuré — le cas en développement — les mails sont écrits
en fichiers `.eml` dans `storage/mails/`** au lieu d'être envoyés. On relit ce
qui serait parti, sans rien expédier.

Le back-office expose un journal des envois, avec relance manuelle d'un
message abandonné après cinq tentatives.

### Limite connue

Le service de fond suppose **une seule instance de l'API**. En cas de montée
en charge horizontale, il faudra un verrou partagé : la clé d'unicité limite
la casse mais ne remplace pas un verrou.

---

## Facturation

**Le prestataire reste l'émetteur.** L'application pré-remplit, numérote selon
*sa* séquence et génère le PDF ; elle ne renumérote jamais et ne pratique pas
l'auto-facturation.

### Numérotation

Préfixe et rang de départ se règlent dans le profil : quelqu'un qui a déjà
facturé jusqu'à 41 hors application reprend à 42, sinon il émettrait une
seconde facture n° 1. **Ces réglages se figent dès la première facture** — les
changer ensuite rejouerait un numéro déjà émis.

Le numéro et le PDF sont attribués dans la même transaction. Un numéro
consommé sans document laisserait un trou dans la séquence ; l'inverse
produirait un PDF sans numéro valide. Une facture annulée **garde son numéro**,
pour la même raison. Une facture payée ne s'annule pas : elle appelle un avoir,
qui n'existe pas encore.

### Mentions

Identité, adresse et SIRET de l'émetteur comme du client sont **recopiés à
l'émission**, jamais relus sur le profil courant : un prestataire qui déménage
ne doit pas réécrire des factures déjà transmises.

Le PDF porte les mentions obligatoires : désignation, quantité, prix unitaire,
total, « TVA non applicable, art. 293 B du CGI », délai de paiement, pénalités
de retard et indemnité forfaitaire de recouvrement.

### Limites assumées

- **Franchise en base uniquement.** L'émission est refusée à un prestataire
  assujetti à la TVA, avec un message explicite — mieux vaut un refus qu'une
  facture non conforme.
- L'émission est aussi refusée tant que le SIRET ou l'adresse manquent.
- Une prestation déjà portée par une facture non annulée n'est plus
  facturable : c'est ce qui empêche de la facturer deux fois.

Le PDF est généré avec **QuestPDF**, sous licence Community — gratuite tant que
le chiffre d'affaires de l'éditeur reste sous le seuil fixé par QuestPDF. À
revoir si l'outil est vendu.

---

## Heures

**Il n'y a pas de pointage.** Le prestataire est indépendant : une fois la
prestation terminée, il **déclare** les heures qu'il a effectuées. Redéclarer
écrase la déclaration précédente tant que l'agence n'a pas validé — c'est une
correction, pas un doublon.

L'agence valide, corrige ou conteste. Une correction des heures déclarées et
une contestation exigent toutes deux un **motif** : sans lui, le prestataire
n'a aucun moyen de savoir quoi refaire. Un relevé contesté revient dans « à
déclarer » côté prestataire, avec le motif affiché.

Si le prestataire oublie de déclarer, l'agence voit la prestation dans
« prestations sans déclaration » et saisit les heures elle-même ; le relevé
part alors directement validé, en portant la mention de qui l'a saisi.

Le relevé n'est créé qu'à la première déclaration : le créer à la
confirmation produirait des relevés orphelins sur les missions annulées.

---

## Staffing et missions

**Un candidat est un prestataire qui s'est déclaré disponible sur tout le
créneau du poste**, qui n'a pas déjà été sollicité dessus, et qui n'a aucune
mission confirmée en conflit. Le filtre par compétence s'ajoute à cela ; le
filtre par zone n'existe pas — sans géocodage, le rayon de déplacement
déclaré n'est pas exploitable, il est seulement affiché.

Un dossier documentaire incomplet **n'exclut personne** : le candidat apparaît
avec la liste de ce qui manque, et l'agence arbitre. Sur un renfort de
dernière minute, elle préfère parfois relancer la pièce après.

### Le cycle d'une proposition

```
proposée --accepte--> acceptée --confirme--> confirmée
    |                     |                      |
    +--refuse--> refusée  +--annule--> annulée <-+
```

**Accepter ne réserve rien.** C'est une candidature : l'agence confirme
ensuite. Un prestataire peut donc se porter candidat à deux endroits à la
fois — mais il ne peut pas accepter une proposition qui chevauche une mission
déjà **confirmée**, ni être confirmé sur un poste complet.

Les **modalités d'accès** au site (quai, badge, code) ne sont transmises
qu'une fois la mission confirmée.

Une date limite dépassée se calcule à la lecture, comme l'expiration des
documents : sans ordonnanceur, un statut stocké mentirait jusqu'au prochain
passage.

**Le remplacement d'un désistement** consiste à annuler la mission confirmée —
la place se libère aussitôt — puis à resolliciter. Un prestataire qui a refusé
ou s'est désisté redevient candidat sur le même poste : les deux propositions
restent lisibles côte à côte, l'unicité ne portant que sur les propositions en
cours.

---

## Disponibilités et calendrier

Le prestataire **déclare** être disponible ou indisponible, par journée ou par
créneau, plusieurs créneaux par jour étant admis. Une déclaration qui en
recouvre une autre la remplace : redéclarer, c'est changer d'avis.

**« Confirmé » ne se déclare pas.** C'est la conséquence d'une mission
acceptée, déduite des `assignments` au moment de construire le calendrier.
L'énumération `AvailabilityStatus` ne contient donc que `Available` et
`Unavailable`. Corollaire : on ne peut pas se déclarer indisponible sur un
créneau couvert par une mission confirmée — l'API répond 409 et renvoie vers
l'agence.

**La récurrence est un confort de saisie, pas une règle stockée.** « Tous les
samedis » se matérialise immédiatement en autant de lignes que de jours, sur
six mois au maximum. Chaque jour reste modifiable indépendamment, et la
recherche de prestataires disponibles de la phase 6 lira la table directement,
sans rien avoir à déplier.

Les compteurs du mois — heures prévues et rémunération estimée — se calculent
sur les missions confirmées, à partir du créneau et du tarif du poste. Estimés
et non facturés : les heures réelles ne seront connues qu'après pointage.

---

### Complétude du dossier

Un dossier est complet quand le SIRET et l'IBAN sont renseignés et que ces
quatre pièces sont validées et non périmées : pièce d'identité, attestation de
vigilance URSSAF, Kbis ou avis SIRENE, attestation RC pro. Permis et
habilitations restent facultatifs, ils ne concernent que certaines missions.

La règle est écrite une seule fois, dans `DossierRules`, et sert aux deux
côtés : l'application prestataire et le back-office affichent forcément le
même verdict.

---

### Comptes de démonstration

Créés par le seed au premier démarrage en développement. Code agence : `demo`.

| Adresse                          | Rôle        |
| -------------------------------- | ----------- |
| `admin@agence-demo.test`         | Admin       |
| `camille.rousseau@example.test`  | Contractor  |
| `yanis.belkacem@example.test`    | Contractor  |
| `lea.marchand@example.test`      | Contractor  |

Mot de passe commun : `LaGestion!2026`. Ce sont des comptes locaux de
démonstration, pas des identifiants sensibles.

---

## Lancer les fronts

Même procédure pour `app/` et `admin/` :

```bash
nvm use         # lit .nvmrc → Node 24.18.1
cd app          # ou: cd admin
cp .env.example .env
npm install
npm run dev
```

| Commande          | Effet                                       |
| ----------------- | ------------------------------------------- |
| `npm run dev`     | serveur de dev Vite                         |
| `npm run build`   | typecheck + build de production              |
| `npm run lint`    | Oxlint                                       |
| `npm run gen:api` | régénère les types TypeScript depuis l'API   |

La route `/statut` de chaque front appelle `GET /health` : c'est le test de
bout en bout de la chaîne front → API → PostgreSQL.

---

## Régénérer les types API

Les types TypeScript sont générés depuis le document OpenAPI de l'API, avec
[openapi-typescript](https://openapi-ts.dev). **L'API doit tourner.**

```bash
# terminal 1
cd api/LaGestion.Api && dotnet run --launch-profile http

# terminal 2 — à faire dans les deux fronts
cd app   && npm run gen:api    # écrit app/src/types/api.ts
cd admin && npm run gen:api    # écrit admin/src/types/api.ts
```

Le fichier généré est versionné : à regénérer et à commiter à chaque
changement de contrat d'API.

### Pourquoi TypeScript est épinglé en `~5.9`

Le template Vite installe TypeScript 6, mais `openapi-typescript` 7.13
déclare `"peerDependencies": { "typescript": "^5.x" }` : avec TS 6,
`npm install` échoue en `ERESOLVE`. Les deux fronts sont donc épinglés en
`typescript: "~5.9"`.

**Quand lever le verrou :** dès qu'`openapi-typescript` publie une version
dont le peer accepte TypeScript 6. Pour vérifier :

```bash
npm view openapi-typescript peerDependencies
```

Si la sortie n'est plus `{ typescript: '^5.x' }`, passer les deux fronts en
`typescript: "~6.x"`, réinstaller et relancer `npm run build`. L'alternative
`legacy-peer-deps` a été écartée : elle aurait cassé chaque `npm install`
suivant ou imposé un `.npmrc` affaiblissant la résolution de tout le projet.

---

## Convention : `AgencyId` (multi-tenant)

L'application est multi-tenant **dès le départ**. La règle, sans exception :

1. **Toute entité métier porte un `AgencyId`.** C'est une colonne obligatoire,
   non nullable, indexée — au même titre que la clé primaire. Aucune entité
   métier n'existe en dehors d'une agence.
2. **Tout accès aux données est filtré par `AgencyId`.** Lecture, écriture,
   suppression, agrégat : chaque requête est restreinte à l'agence du contexte
   courant. Une requête non filtrée est un bug de sécurité, pas une
   optimisation.
3. **Le filtre ne vient jamais du client.** L'`AgencyId` est déduit du contexte
   d'authentification côté serveur ; il n'est jamais lu depuis le corps de la
   requête, la query string ou un en-tête.
4. **Les clés étrangères restent dans la même agence.** Une entité ne référence
   jamais une entité d'une autre agence.

Concrètement, côté EF Core, cela passera par un filtre de requête global sur
`LaGestionDbContext` plus une valeur d'`AgencyId` posée automatiquement à
l'insertion. Rien n'est encore implémenté : aucune entité n'existe à ce stade.

---

## Organisation de l'API

```
api/LaGestion.Api/
├── Domain/           # entités et règles métier (vide pour l'instant)
├── Features/         # un dossier par fonctionnalité : controller + DTOs
│   └── Health/
└── Infrastructure/   # accès aux données, DbContext, services techniques
```

Le `LaGestionDbContext` est **vide** : aucune entité, aucune migration.
`Microsoft.EntityFrameworkCore.Design` est en place, les commandes
`dotnet ef` sont donc utilisables dès la première entité :

```bash
dotnet tool install --global dotnet-ef       # une fois par machine
cd api/LaGestion.Api
dotnet ef migrations add <Nom>
dotnet ef database update
```
