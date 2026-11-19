# IB-007 — архитектура локального Docker-стека

## Сборка API

Solution содержит семь проектов .NET 10. API ссылается на Application, Contracts, Abstractions, Infrastructure и Persistence. Application использует Domain, Contracts и Abstractions. Infrastructure и Persistence используют Domain и Abstractions, Abstractions использует Domain и Contracts. Domain и Contracts не имеют проектных зависимостей. Версии NuGet централизованы в `Directory.Packages.props`, общие настройки сборки находятся в `Directory.Build.props`.

Точка входа — `src/ITMO.Buddy/Api/ITMO.Buddy.Api/ITMO.Buddy.Api.csproj`, сборка — `ITMO.Buddy.Api.dll`. Образы Microsoft `sdk:10.0` и `aspnet:10.0` оставлены на поддерживаемой линии .NET 10 с обновлением патчей через тег. Версии базовых слоёв могут меняться между сборками. Dockerfile восстанавливает зависимости до копирования исходников, публикует API в Release и переносит публикацию в runtime с пользователем `app` и HTTP на 8080.

## Compose и порядок запуска

На этапе 2 добавить `compose.frontend.yml` для совместного запуска с `compose.yml`. Сохранить проект Compose `itmo-buddy-local`, сервис `postgres` с образом `postgres:17`, контейнер `itmo-buddy-postgres`, volume `postgres_data` и публикацию `127.0.0.1:2438:5432`. На проверенной машине этот порт занимает контейнер проекта. На портах 8443 и 9443 слушателей не обнаружено при проверке.

| Узел | Сеть и хранение | Условие запуска |
| --- | --- | --- |
| PostgreSQL | Сеть проекта, `postgres:5432`, существующий volume | Проверка `pg_isready` |
| Мигратор | Сеть проекта, доступ к PostgreSQL | PostgreSQL healthy |
| MinIO | Сеть проекта, `minio:9000`, отдельный named volume данных | S3 API отвечает, консоль остаётся внутри контейнера |
| Инициализация bucket | Сеть проекта, доступ к MinIO | Дождаться S3 и выполнить `mc mb --ignore-existing` с ограниченным ожиданием и кодом ошибки при сбое |
| API | Сеть проекта для PostgreSQL и MinIO, отдельная bridge-сеть до Caddy, HTTP 8080 | Мигратор и инициализация bucket завершились успешно |
| Caddy | Доступ к MinIO по сети проекта и к API по выделенной сети, named volume для `/data` | Проксирует к запущенным сервисам |

Публиковать только IPv4 loopback порты Caddy `127.0.0.1:8443` и `127.0.0.1:9443` (на хосте с IPv6 при необходимости явно проверить или добавить привязки к `::1` для обоих портов). Проверить доступность портов и отсутствие пересечения подсети выделенной bridge-сети с локальными сетями. Для Caddy использовать адрес из её IPAM и передать этот же адрес API как единственный доверенный proxy. Job-сервисы должны использовать условия Compose `service_healthy` для PostgreSQL и `service_completed_successfully` для допуска API. При обновлении образа мигратора Compose должен запускать job новой версии даже при наличии ранее завершившегося контейнера.

## Конфигурация API

| Настройки | Требования |
| --- | --- |
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | Обязательны для базового Compose. Изменение значений в `.env` само по себе не меняет пользователей или пароли уже инициализированной базы |
| `ConnectionStrings__Postgres` | Обязательна для API и design-time factory. В контейнерах использовать `Host=postgres;Port=5432` и согласованные реквизиты базы. Хостовая строка подключения из примера использует loopback и 2438 |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` | Обязательны. Ключ в Base64, минимум 32 байта после декодирования. `Jwt__AccessTokenLifetime` по умолчанию 30 минут, допустимо от 1 секунды до 30 минут |
| `Minio__InternalEndpoint`, `Minio__ExternalEndpoint`, `Minio__AccessKey`, `Minio__SecretKey`, `Minio__BucketName` | Обязательны. Внутренний origin `http://minio:9000`. Согласовать credentials и регион с bucket-init и MinIO. Регион по умолчанию `us-east-1`, срок подписанной ссылки 5 минут |
| `ASPNETCORE_ENVIRONMENT`, `AllowedHosts`, `Https__Port` | Для локального стека Development, явный `localhost` без порта и HTTPS-порт 8443. Development уже задаёт допустимые loopback hosts и порт 7110. Для Production значение `AllowedHosts` требуется явно |
| `ReverseProxy__KnownProxies__0`, `ReverseProxy__ForwardLimit` | Точный адрес Caddy и один hop. Пустой список сетей |
| `Cors__AllowedOrigins__0` и последующие элементы | Явные доверенные HTTPS origins фронтенда по мере необходимости. По умолчанию список пуст. CORS поддерживает credentials |
| `FileCleanup__Enabled` | Оставить выключенным для первого запуска. Перед включением проверить bucket, доступы и PostgreSQL |

Базовый Compose использует `.env` для подстановки. На проверенной машине игнорируемый Git файл `.env` содержит только настройки PostgreSQL и строку подключения. Пример и инструкция IB-006 рассчитаны на запуск API на хосте. Для Compose передавать приложению только необходимые переменные во время запуска, учётные данные не помещать в контекст и образ.

## Миграции

В Persistence есть `20261117192121_InitialPersistence`, `20261118142755_AccountFileCleanup` и snapshot. Первый релиз требует `pg_trgm`, поэтому пользователю миграций нужны права на создание схемных объектов и расширения в целевой БД. Использовать отдельный одноразовый контейнер с EF Core migration bundle, сформированным из проекта Persistence отдельно от publish API. `.config/dotnet-tools.json` фиксирует `dotnet-ef` 10.0.14. Для сборки ориентироваться на контейнерный target Linux выбранной архитектуры (amd64 или arm64) и framework-dependent bundle с .NET 10 runtime. Design-time factory требует `ConnectionStrings__Postgres` при сборке bundle, поэтому дать ей синтаксически валидную заглушку без реальных реквизитов и без доступа к базе. В job передавать рабочую строку окружением. Проверить выбор design-time factory и работу bundle на этапе 2.

По сравнению с запуском EF CLI в SDK-контейнере bundle не требует SDK во время выполнения job. Вариант с SQL потребовал бы отдельно поддерживать генерацию и исполнение идемпотентного скрипта. Bundle применяет отсутствующие миграции по `__EFMigrationsHistory`; повторный запуск с актуальной схемой сохраняет данные. Ошибка останавливает допуск API, а повторная попытка после диагностики использует прежний volume. Существующий readiness отвечает за связность PostgreSQL и не проверяет схему или MinIO, поэтому обязательны ограничения запуска Compose. [Документация EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#bundles).

## MinIO

Анонимные pull официальных репозиториев проверены на Docker Desktop. Для сервера проверены `RELEASE.2025-09-07T16-13-09Z` и `RELEASE.2025-10-15T17-29-55Z`, для клиента — `RELEASE.2025-08-13T08-35-41Z`. Quay вернул `401 UNAUTHORIZED`, Docker Hub — `pull access denied`. Доступный анонимный официальный образ для этих версий не подтверждён.

Для этапа 2 выбран Go build stage с исходниками MinIO `RELEASE.2025-10-15T17-29-55Z` (commit `9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a`) и mc `RELEASE.2025-08-13T08-35-41Z` (commit `7394ce0dd2a80935aded936b09fa12cbb3cb8096`). Коммиты подтверждены через upstream Git. В Dockerfile для MinIO требуется зафиксировать версию Go по исходникам и проверить сборку для amd64 и arm64. Upstream MinIO Community архивирован, поэтому дальнейшая поддержка выбранного релиза остаётся риском. [Релиз MinIO](https://github.com/minio/minio/releases/tag/RELEASE.2025-10-15T17-29-55Z) · [Исходники mc](https://github.com/minio/mc/tree/7394ce0dd2a80935aded936b09fa12cbb3cb8096).

Код использует Minio SDK 7.0.0 для PutObject, presigned GET, RemoveObject, BucketExists и ListObjects. Эти S3 операции определяют проверку совместимости. Сборка MinIO, запуск bucket-init и выполнение операций с выбранным исходным релизом относятся к этапу 2.

## HTTPS и браузерный контракт

Caddy принимает `https://localhost:8443` и проксирует HTTP на `api:8080` с сохранением публичного Host, заменой входящих `X-Forwarded-For` и `X-Forwarded-Proto` и поддержкой WebSocket. API обрабатывает оба заголовка до HTTPS redirect только от явно доверенного адреса. Использовать `ReverseProxy__KnownProxies__0` с адресом Caddy на выделенной сети. Для `https://localhost:9443` проксировать S3 на `minio:9000` с сохранением Host с портом, URI и query для подписи SigV4. Исключить из логов токены, cookie и подписанные query.

Caddy `tls internal` создаёт сертификаты и CA. Сохранять состояние CA в named volume Caddy `/data`. На этапе 3 предусмотреть экспорт только публичного root certificate и явное согласие пользователя на добавление CA в доверенные. Приватный ключ остаётся в volume. Проверки HTTPS выполнять с доверенным CA.

В `MinioOptions` сейчас требуется HTTPS и DNS-имя с точкой вне loopback. На этапе 2 передать в Infrastructure фактический признак Development от хоста и разрешить для него HTTPS origin с точным hostname `localhost`. Использовать одну и ту же проверку в startup validation и при создании `MinioFileStorage`. Сохранить проверку origin и условия Production. Проверить, что signing client с явно заданным регионом формирует URL с `localhost:9443`, и что прокси сохраняет подпись.

| Назначение | Целевой адрес |
| --- | --- |
| API base | `https://localhost:8443` |
| REST | `https://localhost:8443/api/…` — префикс маршрутов контроллеров |
| SignalR | `https://localhost:8443/hubs/chat` |
| OpenAPI | `https://localhost:8443/openapi/v1.json` — только Development |
| Liveness / readiness | `https://localhost:8443/health/live` и `https://localhost:8443/health/ready` |
| Подписанные S3 ссылки | `https://localhost:9443/…` |
| PostgreSQL с хоста | `127.0.0.1:2438` |

Для SignalR предусмотрены JWT Bearer и проверка Origin. Refresh-cookie использует `HttpOnly`, `Secure`, `SameSite=Strict` и путь `/api/auth`; браузерный сценарий требует same-site HTTPS frontend и credentialed requests для операций с cookie. Убедиться, что явный Origin допускается также проверками auth и Hub.

## Реализация этапа 2

`compose.frontend.yml` подключается вместе с базовым `compose.yml` через `--env-file .env.docker`. Используйте `.env.docker.example` как список параметров. Для существующего `postgres_data` нужны действующие `POSTGRES_*`. Строка подключения API и мигратора формируется из тех же имени БД и пользователя, пароль передаётся Npgsql через `PGPASSWORD`. В качестве имён БД и пользователя используйте простые идентификаторы, как в примере. Для обновления исходников запускайте `docker compose --env-file .env.docker -f compose.yml -f compose.frontend.yml up -d --build`. Compose пересоздаёт job при изменении образа, а API зависит от её успешного завершения.

`edge` использует `172.31.243.0/28`, выделенный Caddy адрес `172.31.243.2` и отдельный динамический диапазон `172.31.243.8/29`. Такой диапазон предотвращает выдачу адреса Caddy другому контейнеру до его запуска. При конфликте с локальной сетью задайте `CADDY_SUBNET`, `CADDY_DYNAMIC_RANGE` и `CADDY_IP` согласованно. Caddy подключён к `edge`, API и MinIO — к обеим сетям. Консоль MinIO выключена.

Для проверки TLS используйте публичный сертификат `/data/caddy/pki/authorities/local/root.crt` из контейнера Caddy с `curl --cacert`. Caddy пропускает установку CA в системное хранилище. При остановке стека сохраняйте volumes PostgreSQL, MinIO и Caddy.

На порту 9443 Caddy пропускает только GET к `/<MINIO_BUCKET>/profiles/*` с параметром `X-Amz-Signature`. Caddy удаляет заголовки `Authorization` и `X-Amz-*`, сохраняя публичный Host, путь и параметры ссылки. Подпись и срок действия проверяет MinIO. API и bucket-init используют внутренний S3 endpoint для записи, удаления и обслуживания bucket.
