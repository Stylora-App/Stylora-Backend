# Stylora Backend

ASP.NET Core 9 Web API powering **Stylora**, an AI-powered personal styling and
wardrobe management app. It handles authentication, wardrobe management, AI-driven
colour/season analysis, virtual try-on, an AI outfit chat assistant, and
shopping discovery via ASOS.

---

## Table of Contents

- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [External Integrations](#external-integrations)
- [Functionality](#functionality)
- [Data Model](#data-model)
- [API Documentation](#api-documentation)
- [Configuration](#configuration)
- [Getting Started (Local Development)](#getting-started-local-development)
- [Running with Docker](#running-with-docker)
- [Testing](#testing)
- [CI/CD & Deployment](#cicd--deployment)
- [Project Structure](#project-structure)

---

## Architecture

The solution follows **Clean Architecture** (a.k.a. Onion Architecture), with
dependencies pointing inward toward the Domain layer:

```
Stylora.API            (presentation: controllers, Program.cs, Swagger, auth)
   │
   ├── Stylora.Application   (use cases: services, DTOs, interfaces)
   │      └── Stylora.Domain (entities, enums — no dependencies)
   │
   └── Stylora.Infrastructure (EF Core, repositories, external APIs, AI clients)
          ├── Stylora.Application
          └── Stylora.Domain

Stylora.Application.Tests    (xUnit tests for the Application layer)
```

| Project | Responsibility |
|---|---|
| **Stylora.Domain** | Core entities (`User`, `WardrobeItem`, `SeasonAnalysisResult`, `Color`, `RecommendedColor`, `ClothingReferenceEmbedding`, `TryOnSession`) and enums (`ClothingCategory`, `ClothingReferenceLabel`, `ClothingValidationStatus`, `OutfitRole`, `StylePreference`). No external dependencies. |
| **Stylora.Application** | Business logic services (`AnalysisService`, `WardrobeService`, `TryOnService`, `ExploreService`, `OutfitChatService`, `UserService`, `ClothingValidationService`), DTOs, service interfaces, settings models, and static reference data (colour/category/season taxonomies). |
| **Stylora.Infrastructure** | EF Core `DbContext` (PostgreSQL + pgvector), repositories, external service implementations (Gemini, ASOS, OpenWeatherMap), JWT issuing/validation, and NSwag-generated typed HTTP clients for the AI worker microservices. Includes hosted services that warm up AI workers and seed the clothing-reference embedding dataset on startup. |
| **Stylora.API** | ASP.NET Core Web API: REST controllers, JWT bearer authentication, CORS, Swagger/OpenAPI, composition root (`Program.cs`). |
| **Stylora.Application.Tests** | xUnit tests for application services. |

### Related repositories

This backend is part of a multi-repo system (expected as sibling directories):

```
Projects/
├── stylora-backend/   ← this repo
├── stylora-ai/        ← Python FastAPI AI workers (CLIP + Gemma/Qwen)
└── stylora-frontend/  ← Angular frontend
```

- **stylora-ai**: hosts two FastAPI microservices used by this backend:
  - **CLIP image-embedding worker** (default port `8001`) — generates embeddings for clothing photos, used for wardrobe-item validation via vector similarity search.
  - **Gemma/Qwen intent worker** (default port `8002`) — parses natural-language chat messages into structured outfit intent (occasion, style, gender, weather cues) for the AI Outfit Chat feature.
  - The backend talks to these via NSwag-generated typed clients (`ClipWorkerClient`, `GemmaWorkerClient`), regenerated at build time from OpenAPI specs published in the `Stylora-AI` GitHub repo.
- **stylora-frontend**: Angular SPA consuming this API (CORS-enabled for `http://localhost:4200` by default).

---

## Tech Stack

- **.NET 9 / ASP.NET Core** — Web API
- **Entity Framework Core 9** — ORM, code-first migrations (auto-applied on startup)
- **PostgreSQL 16 + pgvector** — primary datastore, including vector similarity search for clothing-reference embeddings (`Pgvector.EntityFrameworkCore`)
- **JWT Bearer authentication** — access + refresh tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`)
- **Google OAuth** — social sign-in (`Google.Apis.Auth`)
- **Swagger / OpenAPI** — via Swashbuckle, served at `/swagger` in Development
- **NSwag** — generates typed C# HTTP clients for the AI worker APIs at build time
- **SixLabors.ImageSharp** — server-side image processing
- **Mscc.GenerativeAI** — Google Gemini API client

---

## External Integrations

| Service | Used for |
|---|---|
| **Google Gemini** (`gemini-2.5-flash` + Imagen) | Face-photo colour/season analysis, clothing description, virtual try-on image generation |
| **Google OAuth** | "Sign in with Google" |
| **ASOS (via RapidAPI)** | Product search for the Explore/shopping tab, filtered by the user's seasonal colour palette |
| **OpenWeatherMap** | Current weather context for outfit chat recommendations |
| **CLIP worker** (`stylora-ai`) | Embedding-based validation of wardrobe item photos |
| **Gemma/Qwen worker** (`stylora-ai`) | Intent extraction for the AI outfit chat |

---

## Functionality

All endpoints are under `/api`. Endpoints marked 🔒 require a JWT
(`Authorization: Bearer <token>`).

### Auth — `/api/auth`
- `POST /register` — register with email/password (enforces a password policy)
- `POST /login` — email/password login
- `POST /google` — sign in or register via a Google ID token
- `POST /refresh` — exchange a refresh token for a new access/refresh token pair
- `POST /logout`
- `GET /me` 🔒 — current authenticated user
- `POST /change-password` 🔒

### Users — `/api/users` 🔒
- `GET /profile` — get profile (name, picture, style, season, palette, undertone, contrast, etc.)
- `PUT /profile` — update profile

### Analysis — `/api/analysis` 🔒
- `POST /season` — analyse a face photo via Gemini Vision → season, sub-season, palette, recommended metals, undertone, contrast
- `GET /latest` — most recent season analysis result (or `null`)
- `POST /save-profile` — persist a season analysis result to the user's profile

### Wardrobe — `/api/wardrobe` 🔒
- `GET /items` — list the user's wardrobe items
- `POST /items/analyze` — validate a clothing photo against the CLIP reference dataset *without saving*; returns confidence, nearest labels, and suggested category/colour/style/outfit role
- `POST /items` — add a wardrobe item (auto-validated; validation can be overridden)
- `DELETE /items/{id}` — delete a single item
- `POST /items/delete-batch` — delete multiple items

### Chat — `/api/chat` 🔒
- `POST /outfit` — sends the conversation history to the Gemma intent worker, extracts occasion/style/weather/gender, then assembles an outfit board from the user's wardrobe (with follow-up questions if information is missing)

### Explore — `/api/explore` 🔒
- `GET /` — search ASOS products, filterable by `category`, `gender`, `season`, `subSeason`, and `palette` (comma-separated hex codes), paginated via `page`/`pageSize`

### Try-On — `/api/tryon` 🔒
- `POST /generate` — generates a composite "wearing this item" image from a person photo + clothing photo/URL via Gemini
- `GET /last-photo` — retrieves the last person photo used by the authenticated user

---

## Data Model

- **User** — auth (email + password hash, or Google ID), profile fields, style preference; has one `SeasonAnalysisResult`, many `WardrobeItem`s and `TryOnSession`s
- **SeasonAnalysisResult** — season/sub-season, description, undertone, contrast, hair/eye/skin details, recommended metals, linked `RecommendedColor`s
- **Color** / **RecommendedColor** — named colours with hex codes, linking palette colours to analysis results and wardrobe items
- **WardrobeItem** — image path, category, article type, colour family, style, outfit role, worn count, validation status/confidence/message
- **ClothingReferenceEmbedding** — CLIP embedding vectors + dataset metadata (label, category, colour, season, usage tags) used as the validation reference set (pgvector)
- **TryOnSession** — person/clothing/generated image paths, success flag, error message, optional link to a `WardrobeItem`

EF Core migrations live in `Stylora.Infrastructure/Migrations` and are applied
automatically at application startup.

---

## API Documentation

- **Swagger UI** (Development only): `http://localhost:5214/swagger`
- **OpenAPI spec source**: `docs/openapi.entry.yaml` (+ `docs/*.yaml` per-resource endpoint files)
- A bundled spec can be generated with `./build_openapi_specs.sh`, which also
  regenerates the AI worker clients in `Stylora.Infrastructure/OpenAPIGenerated`
  from the `Stylora-AI` repo's published OpenAPI specs.

---

## Configuration

### `appsettings.json` sections

| Section | Purpose |
|---|---|
| `Database` | `Host`, `Database`, `Username` (password supplied via env var) |
| `ClothingValidation` | CLIP worker URL/model, validation thresholds (`MinimumClothingShare`, `MinimumMargin`, `TopK`), seed dataset paths |
| `OutfitChatModel` | Gemma/Qwen worker URL/model, `MaxNewTokens`, `Temperature` |
| `WeatherApi` | OpenWeatherMap base URL and units |

### Required environment variables

| Variable | Purpose |
|---|---|
| `JWT_SECRET` | Signing key for access/refresh tokens |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID for "Sign in with Google" |
| `GEMINI_API_KEY` | Google Gemini API key |
| `RAPIDAPI_KEY` | RapidAPI key for the ASOS product search |
| `OPENWEATHER_API_KEY` | OpenWeatherMap API key |
| `DATABASE_PASSWORD` | PostgreSQL password |

### Optional environment variables

| Variable | Default | Purpose |
|---|---|---|
| `CORS_ORIGINS` | `http://localhost:4200,http://localhost:3000` | Comma-separated allowed origins |
| `AI_CLIP_PORT` | — | Overrides the CLIP worker URL to `http://localhost:<port>/` (local dev) |
| `AI_GEMMA_PORT` | — | Overrides the Gemma worker URL to `http://localhost:<port>/` (local dev) |
| `GEMMA_MODEL_ID` | `google/gemma-4-26B-A4B-it` | Overrides the outfit-chat model ID |

### `.env` file support

On startup, `Program.cs` looks for a `.env` file two directories above the
working directory (i.e. the parent of the repo root when running `dotnet run`
from `Stylora.API/`) and loads any `KEY=VALUE` lines as environment variables.
This is intended for local multi-repo setups; in Docker Compose, variables are
instead supplied via the compose file's `.env`.

---

## Getting Started (Local Development)

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL 16+ with the [`pgvector`](https://github.com/pgvector/pgvector) extension (or run via Docker, see below)
- [NSwag CLI](https://github.com/RicoSuter/NSwag) (`dotnet tool install --global NSwag.ConsoleCore --version 14.2.0`) — only needed once; the build runs it automatically as a pre-build step (skipped when `CI=true`)
- *(Optional, for wardrobe validation & AI chat)* the `stylora-ai` sibling repo, with Python 3 to run its workers

### 1. Database

Run Postgres with pgvector via Docker:

```bash
docker run -d --name stylora-db -p 5432:5432 \
  -e POSTGRES_DB=stylora -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=<your-password> \
  pgvector/pgvector:pg16
```

Migrations are applied automatically the first time the API starts.

### 2. Configure secrets

Provide the [required environment variables](#required-environment-variables).
For local development, `Stylora.API/Properties/launchSettings.json` (gitignored)
already wires these into the `http` launch profile — replace the placeholder
values with your own keys/passwords.

### 3. Run the API

```bash
cd Stylora.API
dotnet run
```

- API base URL: `http://localhost:5214/api`
- Swagger UI: `http://localhost:5214/swagger`

The first build will generate `ClipWorkerClient`/`GemmaWorkerClient` from the
`Stylora-AI` repo's published OpenAPI specs (requires network access).

### 4. (Optional) Run the AI workers

From the `stylora-ai` sibling repo:

```bash
./run.sh        # CLIP worker on :8001, Gemma/Qwen worker on :8002
```

Without these running, AI Chat and wardrobe-item validation will fail to reach
their workers; all other endpoints (auth, profile, explore, try-on, season
analysis) work independently.

---

## Running with Docker

### Build the image

```bash
docker build -t stylora-backend .
```

The `Dockerfile` is a multi-stage build:
`build` (restore + generate NSwag clients + `dotnet build`) → `test` (runs
`Stylora.Application.Tests`) → `publish` → `final` (runtime image based on
`mcr.microsoft.com/dotnet/aspnet:9.0`, listens on port `8080`).

### docker-compose (production-style)

`deploy/docker-compose.yml` defines three services on **pre-existing external**
networks/volumes:

| Service | Image | Notes |
|---|---|---|
| `db` | `pgvector/pgvector:pg16` | Persists to `stylora-pg-data` volume |
| `backend` | `ghcr.io/stylora-app/stylora-backend:latest` | Port `${BACKEND_PORT:-8080}`, bound to `127.0.0.1` |
| `frontend` | `ghcr.io/stylora-app/stylora-frontend:latest` | Port `${FRONTEND_PORT:-3000}`, bound to `127.0.0.1` |

Setup (one-time):

```bash
docker network create stylora-net
docker network create stylora-ai-net   # shared with the stylora-ai workers
docker volume create stylora-pg-data
docker volume create stylora-clothing-seed
```

Create a `.env` next to the compose file with:
`DATABASE_PASSWORD`, `GEMINI_API_KEY`, `RAPIDAPI_KEY`, `OPENWEATHER_API_KEY`,
`CORS_ORIGINS`, `JWT_SECRET`, `GOOGLE_CLIENT_ID`, and optionally
`BACKEND_PORT`/`FRONTEND_PORT`.

```bash
docker compose -f deploy/docker-compose.yml --env-file .env up -d
```

---

## Testing

```bash
dotnet test Stylora.Application.Tests/Stylora.Application.Tests.csproj
```

Covers `ClothingValidationService`, `ExploreService`, `OutfitChatService`, and
`WardrobeService`.

---

## CI/CD & Deployment

`.github/workflows/deploy.yml` ("CI / Deploy Backend"):

1. **test** (every push & PR to `main`): restore → generate NSwag clients →
   `dotnet build -c Release` → `dotnet test` (uploads TRX results as an artifact)
2. **build-and-push** (push to `main` only, after tests pass): builds the
   `final` Docker stage and pushes it to GHCR as
   `ghcr.io/<owner>/stylora-backend:latest` and `:sha-<commit>`
3. **deploy** (push to `main` only): copies `deploy/docker-compose.yml` to the
   VPS over SCP, then SSHes in and runs
   `docker compose --env-file .env pull backend && docker compose --env-file .env up -d --no-deps backend`

Required GitHub secrets: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_SSH_PORT`,
`VPS_DEPLOY_PATH` (the VPS-side `.env` must also define `GHCR_TOKEN` and
`GITHUB_ORG` for the registry login).

---

## Project Structure

```
stylora-backend/
├── Stylora.API/
│   ├── Controllers/          # Auth, User, Analysis, Wardrobe, Chat, Explore, TryOn
│   ├── Program.cs             # composition root, DI, middleware pipeline
│   └── appsettings*.json
├── Stylora.Application/
│   ├── DTOs/
│   ├── Interfaces/
│   ├── Services/
│   ├── Models/                # settings + reference data (colours, categories, seasons)
│   └── Security/PasswordPolicy.cs
├── Stylora.Domain/
│   ├── Entities/
│   └── Enums/
├── Stylora.Infrastructure/
│   ├── Data/                  # StyloraDbContext
│   ├── Migrations/
│   ├── Repositories/
│   ├── Services/              # Gemini, ASOS, OpenWeather, JWT, AI worker wrappers
│   ├── nswag/                 # NSwag configs for AI worker clients
│   └── OpenAPIGenerated/       # generated AI worker clients (build artifact)
├── Stylora.Application.Tests/
├── docs/                       # OpenAPI spec (entry + per-resource endpoint files)
├── deploy/docker-compose.yml
├── Dockerfile
└── build_openapi_specs.sh
```
