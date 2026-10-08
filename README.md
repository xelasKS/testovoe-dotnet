# ClientCommissions

Система начисления реферальных комиссий. Событие о прибыли пользователя
добавляет комиссии тем, кто его пригласил вверх по партнёрскому дереву; комиссии
накапливаются и периодически выплачиваются на балансы кошельков.

## Архитектура

Три микросервиса, у каждого своя база (database-per-service), плюс общий
проект контрактов.

| Сервис | Ответственность | База |
|---|---|---|
| **Commissions** | приём событий, расчёт комиссий, периодическая выплата | `clientcommissions` |
| **Users** | партнёрское дерево, выдача цепочки предков | `clientcommissions_users` |
| **Wallet** | балансы и история выплат | `clientcommissions_wallet` |
| **Shared** | wire-контракты между сервисами (`AncestorContract`, `PayoutContract`) | — |

## Ключевые решения

**Разбиение на сервисы  — по зонам ответственности и профилю нагрузки**.
Приём событий + расчёт — один горячий путь, поэтому они в одном сервисе. Иерархия и кошелёк — отдельные сервисы.

**Приём асинхронный.** `POST /events` пишет событие и сразу отдаёт `202`.
Расчёт происходит с помощью фонового воркера. Приём не падает вместе с Users/Kafka, и latency
приёма не зависит от остального конвейера.

**Outbox на стороне Commissions.** Нельзя атомарно изменить БД и опубликовать
в Kafka. Планировщик выплат в одной транзакции переводит комиссии `Unpaid ->
Paying` и пишет строки в outbox. Диспетчер работает по схеме claim-and-release
(см. ниже): короткая транзакция клеймит батч `Unsent -> Sending`, затем publish
в Kafka идёт БЕЗ открытой транзакции, и вторая короткая транзакция проставляет
`Sent` + комиссию `Paid` по фактически опубликованным. Падение между шагами
безопасно: Kafka at-least-once, а consumer в Wallet идемпотентен — непрошедшие
строки остаются `Sending` и переподхватываются по таймауту заявки.

**Inbox на стороне Wallet.** Kafka даёт at-least-once, сообщение может прийти
повторно. Consume и обработка развязаны через `System.Threading.Channels`: один
цикл поллит брокер и кладёт сообщения в bounded-канал, другой обрабатывает и
сдвигает оффсет (`StoreOffset` + периодический auto-commit) только после успешной
записи. Так медленная запись в БД не блокирует поллинг и не вызывает ребаланс.
Уникальный констрейнт на
`CommissionId` в таблице выплат + ловля `23505` делают зачисление идемпотентным —
эффект exactly-once. Ядовитое сообщение не блокирует партицию: после нескольких
неудачных попыток оно уходит в dead-letter-топик, а оффсет сдвигается.

**Сквозная идемпотентность — два ключа в двух местах:**
- `ExternalId` события (Commissions, приём) — нет двойного расчёта;
- `CommissionId` выплаты (Wallet, зачисление) — нет двойной выплаты.


**Партиционирование по `userId`.** Все выплаты одного пользователя попадают в
одну партицию → строгий порядок. Продюсер идемпотентный (`EnableIdempotence`,
`Acks=All`).

**Resilience на синхронном вызове Commissions → Users** — свой Polly-пайплайн
(`AddResilienceHandler`): total timeout → retry (экспонента + джиттер,
только транзиентные) → circuit breaker → per-attempt timeout.

**Активная схема начисления** (`Linear` / `Fibonacci`) хранится строкой в БД
(`SchemaSetting`), резолвер отдаёт реализацию по значению; переключается через
`PUT /admin/schema` и влияет только на новые расчёты (у уже посчитанных комиссий
схема застемплена). Движок расчёта — чистая функция, покрыт unit- и
property-тестами. Индексация Фибоначчи: `F(1) = F(2) = 1` (уровни 1 и 2 дают
одинаковый коэффициент), дальше `F(n) = F(n-1) + F(n-2)`.

## Запуск

Полный стек (БД, Kafka, миграции, три сервиса, Prometheus, Grafana) одной
командой:

```bash
docker compose up -d --build
```

Порядок поднятия выстроен зависимостями: `postgres` → `migrator` (накатывает
миграции и выходит) → сервисы; `kafka` → `kafka-init` (создаёт топик) → сервисы.

- Commissions: http://localhost:5031
- Users: http://localhost:5097
- Wallet: http://localhost:5141
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (дашборд «ClientCommissions Pipeline»)
- `/metrics` на каждом сервисе

**Миграции не накатываются приложением на старте** (в проде это антипаттерн:
гонки при нескольких репликах, лишние права у приложения). Их применяет
отдельный одноразовый сервис `migrator` (`dotnet ef database update`), от
которого зависят сервисы. Дефолтная схема `Linear` засеяна через `HasData` в
миграции.

### Быстрый сквозной сценарий

```bash
# цепочка root -> mid -> leaf
curl -s -XPOST localhost:5097/users -H 'Content-Type: application/json' -d '{}'
# ... подставить полученные id в referrerId следующих, затем событие по leaf:
curl -s -XPOST localhost:5031/events -H 'Content-Type: application/json' \
  -d '{"externalId":"<guid>","userId":"<leaf>","profit":1000}'
# через ~20с балансы в Wallet: mid=10 (уровень 1), root=20 (уровень 2)
curl -s localhost:5141/accounts/<mid>
```

## Тесты

```bash
dotnet test
```

- **Commissions.Tests** — движок расчёта: example-тесты + property-тесты;
  тесты HTTP-клиента к Users с resilience (retry/404, подставной handler).
- **Users.Tests / Wallet.Tests** — интеграционные на реальном Postgres
  (Testcontainers): рекурсивный CTE обхода иерархии, inbox-дедуп выплат.
- **IntegrationTests** — сквозной E2E: поднимает Postgres + Kafka (Testcontainers),
  хостит все три сервиса, гоняет событие через всю систему до балансов;
  плюс round-trip сообщения через Kafka.

Для интеграционных тестов нужен запущенный Docker.

## Observability

- **Serilog** — структурные логи, request logging, correlation-scope
  (`EventId` / `CommissionId`) для сквозной трассировки события.
- **prometheus-net** — HTTP и рантайм-метрики + бизнес-счётчики.
- **Grafana** — дашборд провиженится автоматически.

## API

Swagger UI на каждом сервисе (`/swagger`). Основные ручки:

**Commissions** (`:5031`)
- `POST /events` — принять событие (202).
- `GET /events?userId=` — события пользователя.
- `GET /events/{id}` — детали события: комиссии (кому, уровень, сумма, схема, статус).
- `GET /admin/schema` / `PUT /admin/schema` `{ "schema": "Linear" | "Fibonacci" }` — текущая/переключение схемы.
- `GET /health`.

**Users** (`:5097`)
- `POST /users` `{ referrerId? }` — создать узел дерева.
- `GET /users/{id}/ancestors` — ветка вверх (предки).
- `GET /users/{id}/descendants` — ветка вниз (приглашённые).
- `GET /health`.

**Wallet** (`:5141`)
- `GET /accounts/{userId}` — баланс.
- `GET /accounts/{userId}/payouts` — история выплат.
- `GET /health`.

Выплаты приходят в Wallet только из Kafka.

## Стек

.NET 8, ASP.NET Core (minimal API), EF Core + PostgreSQL, Confluent.Kafka,
Polly (Microsoft.Extensions.Http.Resilience), Serilog, prometheus-net, xUnit,
FsCheck, Testcontainers, Docker Compose.
