# Agent Memory Service

> One memory. Every agent. Your team.

A shared agent memory platform for teams — cross-agent, namespace-scoped, with vector search.

## Stack

- **.NET 8** — ASP.NET Core Minimal APIs
- **PostgreSQL + pgvector** — persistent storage + semantic search
- **Redis** — distributed caching
- **JWT** — authentication
- **EF Core 8** — ORM with code-first migrations
- **MediatR + FluentValidation** — CQRS pipeline
- **Modular monolith** — Users, Namespaces, Memories, Agents modules

## Quick Start

```bash
# Start Postgres + Redis
docker-compose up postgres redis -d

# Run migrations & start API
cd src/Api
dotnet run
```

Swagger UI: http://localhost:5000/swagger

## API Overview

| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/auth/register | Register user |
| POST | /api/auth/login | Get JWT token |
| GET | /api/namespaces | List namespaces |
| POST | /api/namespaces | Create namespace |
| GET | /api/namespaces/{id}/memories | List memories |
| POST | /api/namespaces/{id}/memories | Add memory |
| GET | /api/namespaces/{id}/memories/search?q= | Semantic search |
| POST | /api/namespaces/{id}/agents | Create agent |

## Modules

- **Users** — registration, login, JWT refresh
- **Namespaces** — isolated memory spaces with member roles
- **Memories** — store/retrieve/search memories with pgvector embeddings
- **Agents** — API key-based agent identities scoped to namespaces


# Update the database with any new migrations
dotnet ef database update  --project ./Modules/Shared/Shared.Infrastructure/AgentMemoryService.Shared.Infrastructure.csproj --startup-project ./Api/AgentMemoryService.Api.csproj
