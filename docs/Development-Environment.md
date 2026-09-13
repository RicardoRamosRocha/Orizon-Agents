# Orizon Agents — Ambiente de Desenvolvimento

## Infraestrutura oficial

```text
PostgreSQL: postgres:16 — 127.0.0.1:55432 — database orizon_agents
Redis: redis:7-alpine — 127.0.0.1:6379
```

O PostgreSQL Windows em `127.0.0.1:5432` não pertence ao Orizon Agents.

## Inicialização

```powershell
docker compose up -d
docker compose ps
```

Configure credenciais em `.env` não versionado e em User Secrets/variáveis de ambiente. Os `appsettings*.json` não armazenam a senha do PostgreSQL.

## Migrations

Migrations não são executadas automaticamente no startup. Para aplicar explicitamente:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=127.0.0.1;Port=55432;Database=orizon_agents;Username=orizon;Password=<local-secret>'
dotnet ef database update --project .\src\OrizonAgents.Infrastructure --startup-project .\src\OrizonAgents.Web --context OrizonAgentsDbContext --configuration Debug
```

O Database Guard falha em Development se o endpoint estiver incorreto ou houver migrations pendentes.

## Health

```powershell
Invoke-WebRequest http://127.0.0.1:5017/health
```

## Backup e restore

```powershell
$env:PGPASSWORD = '<local-secret>'
.\scripts\db\backup.ps1
.\scripts\db\restore.ps1 -BackupFile .\backups\postgres\<timestamp>\orizon_agents.backup
```

O backup usa `127.0.0.1:55432`, cria arquivos em `backups/postgres/` e valida o catálogo com `pg_restore --list`. O restore padrão usa uma base temporária; o banco oficial exige confirmação explícita.

## Regras

Não use o banco de desenvolvimento como banco destrutivo de testes. Nunca execute, sem procedimento de recuperação aprovado:

```text
docker compose down -v
EnsureDeleted
DROP DATABASE
TRUNCATE
```

O volume `orizon-agents_postgres_data` deve ser preservado. Não pare ou altere a infraestrutura de outros projetos.
