# The Slow Road Armenia

A travel guide to Armenia — built with ASP.NET Core MVC and shipped to AWS EC2
through a fully automated Docker + GitHub Actions pipeline.

### 🌐 Live: [slowroadarmenia.com](https://slowroadarmenia.com/)

---

## Stack

| Layer | Tech |
|---|---|
| App | .NET 9 · ASP.NET Core MVC · EF Core · ASP.NET Identity |
| Data | PostgreSQL 16 · S3 (backups) |
| Ship | Docker Compose · GitHub Actions · GHCR |
| Run | AWS EC2 · nginx |

---

## Pipeline

Two workflows, each stage gating the next:

- **CI** (`ci.yml`) — every pull request to `main`: `dotnet build + test`. No deploy.
- **CD** (`cd.yml`) — every push to `main`: test → build → deploy.

Both can also be run manually from the Actions tab (`workflow_dispatch`).

```mermaid
flowchart TD
    subgraph CI["CI · pull request"]
        A["dotnet build + test<br/>(no deploy)"]
    end

    subgraph CD["CD · push to main"]
        B["test<br/>• dotnet build + test"]
        C["build<br/>• docker build<br/>• push → ghcr.io/telmanarm/slowroad-armenia:latest + :sha"]
        D["deploy<br/>• generate idempotent migrate.sql (dotnet ef)<br/>• scp compose file · migrate.sql · backup.sh → /opt/slowroad<br/>• SSH → deploy/deploy.sh"]
        B --> C --> D
    end

    subgraph SH["deploy/deploy.sh (on EC2)"]
        S1["1 · backup DB → S3"]
        S2["2 · apply migrations"]
        S3["3 · docker compose pull app (:sha)"]
        S4["4 · docker compose up -d"]
        S5["5 · docker image prune"]
        S1 --> S2 --> S3 --> S4 --> S5
    end

    subgraph PROD["Production"]
        P["AWS EC2 → nginx → app :8080 → Postgres (pgdata volume)"]
    end

    D --> SH --> PROD
```

Deploys use the exact commit SHA image, so any deploy maps to one commit and can
be rolled back. The container binds to `127.0.0.1` only — nothing is exposed
except through nginx.

---

## Docker

Multi-stage build: the SDK image restores and publishes, the slim ASP.NET
runtime image carries only the published output — smaller image, no build tools
in production. The app needs Postgres to run.

---

## Configuration

Secrets are never committed.

**GitHub repo secrets**
`EC2_HOST` · `EC2_USER` · `EC2_SSH_KEY`

**Server `/opt/slowroad/.env`**
`POSTGRES_USER` · `POSTGRES_PASSWORD` · `POSTGRES_DB` ·
`ConnectionStrings__Default` · `Admin__Email` · `Admin__Password` · `BACKUP_BUCKET`

Locally, the connection string lives in `appsettings.Development.json`, and the
admin login (`Admin:Email` / `Admin:Password`) is set via `dotnet user-secrets`.

---

## Database & migrations

On deploy, CD generates an idempotent SQL script and applies it **after** a
backup and **before** the new app starts.

---

## Backups

- **Automatic** before every deploy
- **Manual** runs available on EC2
- Stored gzipped in `s3://$BACKUP_BUCKET/postgres/`; last 2 kept on the server

---

## Rollback

Deploys are pinned to the exact commit SHA image, so an older SHA can be brought
back. Migrations are **not** rolled back — if the schema changed, restore from a
backup.

---

## Branches

| Branch | Role |
|---|---|
| `develop` | Daily work; feature branches merge here |
| `main` | Production; PR from `develop` runs CI, merge deploys automatically |