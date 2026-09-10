# The Slow Road Armenia

**Live:** [slowroadarmenia.com](https://slowroadarmenia.com/)

A travel guide site for Armenia — built with ASP.NET Core MVC and shipped
to AWS EC2 through a fully automated Docker + GitHub Actions pipeline.

**Stack:** .NET 9 · ASP.NET Core MVC · Docker · GitHub Actions · GHCR · AWS EC2 · nginx

---

## Pipeline

```
git push (main)
      │
      ▼
GitHub Actions — build.yml
  • docker build
  • push → ghcr.io/telmanarm/slowroad-armenia:latest + :<sha>
      │
      ▼
GitHub Actions — deploy.yml   (triggered on successful build)
  • SSH into EC2
  • docker pull latest
  • stop / remove old container
  • docker run -d --restart unless-stopped -p 127.0.0.1:8080:8080
  • docker image prune
      │
      ▼
AWS EC2 → nginx reverse proxy → container :8080
```

Every image is tagged with both `latest` and the commit SHA, so any deploy can
be rolled back to an exact commit. The container binds to `127.0.0.1` only —
nothing is exposed to the internet except through nginx.

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
