-- Creates one database per service on the single local Postgres server,
-- per DatabaseDesign.md §1 ("database-per-service") and Deployment.md §1
-- ("one Postgres server, multiple databases, one per service" for local dev).
-- Each service's own EF Core migrations own the schema *inside* its database
-- from here on — this script only ever creates the empty databases themselves.

CREATE DATABASE identity;
CREATE DATABASE tenant;
CREATE DATABASE trip;
CREATE DATABASE ai;
CREATE DATABASE notification;
CREATE DATABASE reporting;
