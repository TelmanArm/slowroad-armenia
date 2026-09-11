# The Slow Road Armenia

**Live:** [slowroadarmenia.com](https://slowroadarmenia.com/)

A travel guide site for Armenia — built with ASP.NET Core MVC and shipped
to AWS EC2 through a fully automated Docker + GitHub Actions pipeline.

**Stack:** .NET 9 · ASP.NET Core MVC · Docker · GitHub Actions · GHCR · AWS EC2 · nginx

---

## Pipeline

Two workflows:

- **CI** (`ci.yml`) — runs on every pull request to `main`: `dotnet build + test`. No deploy.
- **CD** (`cd.yml`) — runs on every push to `main`: test → build → deploy.

CD runs on push to `main`, and each stage gates the next:

```
open PR (→ main)                 git push (main)
      │                                │
      ▼                                ▼
CI: dotnet build + test          test    • dotnet build + test  (deploy only if these pass)
    (no deploy)                        │
                                       ▼
                                 build   • docker build
                                         • push → ghcr.io/telmanarm/slowroad-armenia:latest + :<sha>
                                       │
                                       ▼
                                 deploy  • SSH into EC2, run deploy/deploy.sh
                                         • docker pull the exact :<sha> image
                                         • replace container (-d --restart unless-stopped -p 127.0.0.1:8080:8080)
                                         • docker image prune
                                       │
                                       ▼
                                 AWS EC2 → nginx reverse proxy → container :8080
```

Deploys use the exact commit SHA image, so any deploy maps to one commit and can
be rolled back. The container binds to `127.0.0.1` only — nothing is exposed
except through nginx.

## Docker

Multi-stage build: the SDK image restores and publishes, the slim ASP.NET
runtime image carries only the published output — smaller image, no build
tools in production.

```bash
docker build -t slowroad .
docker run -p 8080:8080 slowroad
# → http://localhost:8080
```

## Run locally

```bash
dotnet restore
dotnet run --project SlowRoad
```

## Configuration

Deployment secrets live in GitHub repo secrets, never in the repo:
`EC2_HOST`, `EC2_USER`, `EC2_SSH_KEY`.

## Branches

- `main` — production; a push here builds and deploys automatically
- `develop` — integration branch
