-- Initialisation de la base de développement.
--
-- Remplace ce que faisait le conteneur Docker : création du rôle et de la
-- base applicatifs sur le PostgreSQL 16 installé localement.
--
-- À exécuter une fois, en superutilisateur, sur la base « postgres » :
--   psql -h localhost -p 5432 -U postgres -d postgres -f scripts/init-db.sql
--
-- Idempotent : relancer le script sur une installation déjà initialisée ne
-- casse rien et ne modifie pas le mot de passe existant.
--
-- Les identifiants sont en clair et volontairement triviaux : ils ne servent
-- qu'en développement local et sont identiques pour toute l'équipe. Ils
-- correspondent à la chaîne de connexion de
-- api/LaGestion.Api/appsettings.Development.json.

-- Rôle applicatif. CREATE ROLE n'accepte pas IF NOT EXISTS, d'où le bloc.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'lagestion') THEN
        CREATE ROLE lagestion LOGIN PASSWORD 'lagestion';
        RAISE NOTICE 'Rôle « lagestion » créé.';
    ELSE
        RAISE NOTICE 'Rôle « lagestion » déjà présent, inchangé.';
    END IF;
END
$$;

-- Base applicative. CREATE DATABASE ne peut pas tourner dans un bloc DO
-- (pas de transaction), on passe donc par \gexec : la requête ne produit une
-- commande à exécuter que si la base est absente.
SELECT format('CREATE DATABASE %I OWNER %I', 'lagestion', 'lagestion')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'lagestion')
\gexec

-- Droits sur la base, quel que soit son état initial.
GRANT ALL PRIVILEGES ON DATABASE lagestion TO lagestion;
