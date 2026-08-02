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
