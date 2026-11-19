# IB-007 · Stage 4/4 — финальный аудит Backend MVP

**Рекомендация — ACCEPT для локальной Frontend-интеграции.** Стек с обычным `dev-up` доступен по HTTPS, основные сценарии подтверждены. Блокирующих дефектов в проверенной конфигурации локальной разработки не осталось. Готовность публичного Production-развёртывания не подтверждена.

## 1. Git и состав IB-007

- Ветка — `feature/IB-007`. Исходный локальный и удалённый HEAD — `6c8997b74ae2394797eb0fc1493ad2f22bd5d554`. Рабочее дерево на входе чистое. Все пять принятых коммитов находятся в истории.
- SHA исправления этапа 4 — `1d5e2c0fd31167f3a76d929f0bd865db075e48c0`. Коммит отчёта имеет название `docs: record IB-007 final backend audit`. Его точный SHA определяется через `git log -1 --format=%H -- docs/IB-007-final-report.md`.
- Push — опубликован в `origin/feature/IB-007`. [Отчёт в удалённой ветке](https://github.com/PrIIIcKoLyAZZZ/ITMO-Buddy-backend/blob/feature/IB-007/docs/IB-007-final-report.md). Финальный удалённый SHA публикуется в сообщении о завершении. Целевой ref — `refs/heads/feature/IB-007`.

| Коммит | Изменения |
| --- | --- |
| `70c5d22` | Dockerfile API, подготовка контекста сборки и архитектура локального стека |
| `f40445b` | Compose для HTTPS, Caddy, MinIO из фиксированных исходников, bucket-init и мигратор |
| `056ab38` | Манифест dotnet-ef в Git для чистых сборок |
| `32cdfe4` | Developer scripts, шаблон окружения и инструкция передачи Frontend |
| `6c8997b` | Swagger UI в Development и инструкции JWT Authorize |
| Этап 4 | Ограничение доступа через Caddy к MinIO, описание контрактов SignalR, итоговый отчёт |

## 2. Docker и версии

Проверка проведена из отдельного чистого клона в Linux arm64 daemon Docker-in-Docker на macOS Docker Desktop. В окружении скрипта отсутствовал `dotnet`. Сборка из исходников, SDK, EF CLI и Go работали внутри Docker. Стек пользователя в основном daemon и его тома не затрагивались.

| Компонент | Проверенная версия и роль |
| --- | --- |
| Docker / Compose тестового daemon | Engine 29.10.0, Compose 5.7.1 |
| PostgreSQL | 17.12 из `postgres:17`, `pg_isready`, том `postgres_data` |
| API | ASP.NET Core и .NET 10.0.15, Release, пользователь `app`, HTTP 8080 внутри Docker |
| Мигратор | Одноразовый EF bundle 10.0.14 на .NET Runtime 10.0.15 |
| MinIO | `RELEASE.2025-10-15T17-29-55Z`, `9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a`, Go 1.24.8, том `minio_data` |
| Bucket-init | mc `RELEASE.2025-08-13T08-35-41Z`, `7394ce0dd2a80935aded936b09fa12cbb3cb8096`, Go 1.24.8 |
| Caddy | 2.11.2 Alpine, `tls internal`, том `caddy_data` |

PostgreSQL и MinIO прошли healthchecks. Мигратор ждёт PostgreSQL, bucket-init ждёт MinIO, API запускается после успешного завершения обеих задач. API и MinIO подключены к `default` и `edge`, Caddy подключён к `edge` с закреплённым IP. Публикация на хост ограничена IPv4 loopback для PostgreSQL 2438 и Caddy 8443/9443. У API и MinIO отсутствуют host port bindings.

## 3. Сборка и Clean Architecture

`dotnet restore ITMO-Buddy-backend.slnx` и `dotnet build ITMO-Buddy-backend.slnx --no-restore` прошли с SDK 10.0.201. Результат — 0 warnings, 0 errors. Docker Compose config, сборка всех четырёх build targets и Caddy config проверены.

| Проект .NET 10 | Сборка | Прямые проектные зависимости |
| --- | --- | --- |
| Domain | PASS | — |
| Application.Contracts | PASS | — |
| Application.Abstractions | PASS | Domain, Contracts |
| Application | PASS | Domain, Contracts, Abstractions |
| Infrastructure | PASS | Domain, Abstractions |
| Infrastructure.Persistence | PASS | Domain, Abstractions |
| Api | PASS | Application, Contracts, Abstractions, Infrastructure, Persistence |

Циклов в `ProjectReference` нет. Domain и Application независимы от Infrastructure. `Program.cs` собирает зависимости и включает `ValidateOnBuild` и `ValidateScopes`. Все 11 Application Services, валидаторы, DbContext, репозитории и current-user зарегистрированы scoped. JWT, BCrypt, MinIO и SignalR notifier — singleton. Фоновый FileCleanup создаёт собственные scopes. Очистка login rate limit обслуживается отдельным hosted service. Startup options validation проверяет JWT, MinIO, FileCleanup и прикладные параметры. Контроллеры адаптируют HTTP и делегируют предметные операции Application.

## 4. Безопасность

- JWT выпускается и проверяется с HS256, общим Base64-ключом, issuer и audience. Обязательны подпись, срок действия и однозначная идентичность, clock skew равен нулю. Защищённый маршрут вернул 401 без JWT и 200 с JWT. Срок по умолчанию — 30 минут.
- Регистрация и вход выдали `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`. Refresh заменил cookie, повтор старого токена получил 401. Logout удалил cookie, последующий refresh отозванным токеном получил 401. В PostgreSQL хранится хеш refresh-токена. Выданный JWT действует до истечения срока.
- CORS разрешил настроенный HTTPS origin с credentials. Для постороннего origin заголовок разрешения отсутствовал. AuthOriginGuard отклонил запрос без Origin, ChatHubOriginGuard отклонил чужой Origin. SignalR negotiate без JWT вернул 401.
- Защита сообщений проверена на трёх пользователях. Посторонний пользователь не прочитал чат. Решение по чужому объявлению вернуло 403. Приватные события отправляются через `Clients.User` с идентификатором из подписанного `sub`.
- Caddy передаёт схему и адрес, API обрабатывает forwarding только от настроенного proxy с лимитом в один hop. В коде подтверждены отдельные rate limits auth, тегов, фото и сообщений и ограничение неудачных входов. Полные окна и отрицательные proxy-сценарии покрыты [принятым аудитом IB-006](IB-006-final-audit.md#10-security).
- В отдельном Production-запуске `https://localhost:9443` отклонён при валидации MinIO Options. С допустимым DNS origin подтвердились readiness и login. `/swagger`, `/swagger/index.html` и `/openapi/v1.json` вернули 404 с действующим JWT. HSTS регистрируется вне Development.
- Gitleaks не обнаружил утечек в чистом checkout и в истории ветки. `.env.docker` создаётся локально и игнорируется Git. В API/Caddy logs отсутствовали проверенные JWT, пароли, refresh-cookie, подписанные URL и ключ query `access_token=`. NuGet audit не сообщил advisory по прямым и транзитивным пакетам семи проектов на момент проверки.

## 5. Application, REST и SignalR

| Сценарий | Результат этапа 4 |
| --- | --- |
| Auth | Register, login, refresh, logout, повтор отозванных токенов и guards проверены |
| Profiles и словари | PUT/GET собственного профиля, faculties, создание и поиск interests/skills |
| Поиск и рекомендации | GET поиска и рекомендаций, skip. Оценка качества ранжирования вне smoke |
| Likes и matches | Взаимные лайки создали match, доступен идентификатор беседы |
| Объявления и отклики | Создание, два отклика, отказ постороннему, параллельное принятие на одно место |
| Chats, messages, notifications | Список бесед, отправка и чтение сообщения, список уведомлений, изоляция третьего пользователя |
| SignalR | Negotiation и WSS handshake, доставлены `MatchCreated`, `MessageReceived` и `NotificationUnreadCountChanged` с ожидаемым payload |
| Photo, account deletion, cleanup | Загрузка, замена и удаление фото, удаление собственного аккаунта, сохранность соседнего аккаунта |

Проверены DTO/валидаторы и прикладные границы транзакций. Создание мэтча, соединение участников, уведомления и решение по отклику сохраняются транзакционно. Доставка realtime выполняется после commit. Фото и удаление аккаунта используют блокировку аккаунта, очередь удаления и проверку ссылок. Повтор штатного refresh защищён конкурентным обновлением записи.

## 6. PostgreSQL и миграции

Просмотрены EF mappings, уникальные и составные индексы, внешние ключи, каскады и optimistic concurrency. Начальный прогон применил `20261117192121_InitialPersistence` и `20261118142755_AccountFileCleanup`. В DB подтверждены обе строки `__EFMigrationsHistory` с EF 10.0.14, расширение `pg_trgm` и 69 индексов схемы public. Повторное исполнение bundle сообщило, что схема актуальна. Оба запуска завершились с кодом 0. При двух параллельных решениях на одно место ответы были 200 и 409. После `dev-down` и `dev-up` сохранились PostgreSQL-аккаунт, профиль и фото в MinIO.

## 7. MinIO, Caddy и TLS

- Bucket-init завершился с кодом 0 на первом и повторных запусках. Bucket имеет private policy. Внутреннее чтение списка выполнено с mc. Через API подтверждены PutObject, подписанный GET с побайтовым совпадением и RemoveObject при замене фото и удалении аккаунта.
- Caddy на 9443 передаёт только подписанные GET-запросы на путь фотографий. Штатный PUT, запрос к корню, GET без подписи и GET с изменённым составом query получили 403. Ранее действительные ссылки на удалённые объекты получили 404. Во временной конфигурации worker через SDK перечислил объекты, поставил один объект без ссылки в очередь и удалил его. Фото со ссылкой в БД сохранилось. При TTL 3 секунды ссылка после истечения срока вернула 403.
- Caddy выпустил цепочку сертификатов для `localhost`. `curl --cacert` и TLS-клиент доверяли экспортированному CA. CA сохранился после пересоздания контейнеров. `.docker-local` содержал только публичный `root.crt`. Системное доверие при проверке не менялось.

## 8. Swagger UI

HTTPS `/swagger` и ресурсы UI доступны при стандартном запуске Development. `/openapi/v1.json` содержит 27 путей и 35 операций. Проверены схема HTTP Bearer, требование JWT для защищённого профиля и ответы 401/200 с тем же REST-маршрутом, который использует Try it out. Интерактивные Authorize и Try it out покрыты принятой браузерной проверкой `6c8997b`. Реализация Swagger и OpenAPI на этапе 4 не менялась. Полный браузерный сценарий не повторялся.

## 9. Developer scripts и Frontend handoff

- `sh -n` прошёл для `dev-common.sh`, `dev-up.sh`, `dev-down.sh` и `dev-logs.sh`. Первый `dev-up` сгенерировал `.env.docker` с правами `0600`. Повторные запуски сохранили его байты, рабочие PostgreSQL-реквизиты, сертификат и именованные тома. `dev-logs.sh migrator` вывел журнал мигратора. `dev-down.sh` убрал контейнеры и сети, тома сохранились.
- Windows BAT: Implemented, Not Tested. PowerShell и Windows-окружение не запускались.
- Все необходимые для интеграции Backend-контракты документированы и доступны Frontend-разработчику в [README](../README.md), OpenAPI и [справочнике REST](IB-006-final-audit.md#3-implemented-rest-api). Проверены инструкции запуска, Swagger Authorize, Bearer, refresh-cookie и `credentials: 'include'`, CORS, пять SignalR payload, восстановление истории через REST, фото, CA, логи, остановка, сохранение и явный сброс данных. Отсутствующих контрактов по итогам сверки не выявлено. Два события объявлений сверены по коду и принятому IB-006, их доставка отдельно на этапе 4 не повторялась.

## 10. Исправленные дефекты

| Серьёзность | Причина и файлы | Повторная проверка |
| --- | --- | --- |
| Critical · устранён для локального входа | Прежний Caddy открывал весь S3 API на 9443. Зафиксированная версия MinIO затронута advisory об обходе проверки подписи при записи. `docker/Caddyfile` ограничивает доступ GET на путь фото с query-подписью и удаляет `Authorization` и `X-Amz-*` из заголовков. `compose.frontend.yml` передаёт имя bucket. README и Docker architecture описывают границу доступа. [MinIO GHSA-hv4r-mvr4-25vw](https://github.com/minio/minio/security/advisories/GHSA-hv4r-mvr4-25vw) · [MinIO GHSA-9c4q-hq6p-c237](https://github.com/minio/minio/security/advisories/GHSA-9c4q-hq6p-c237) | Конфигурация Caddy прошла валидацию. Загрузка API, чтение подписанного фото, отказ внешнему PUT, приватность bucket, замена и удаление подтверждены через обычные операции |
| Major · исправлен | README описывал одно событие SignalR. Добавлены пять событий, получатели, формы аргументов и восстановление истории | Сверка с `SignalRRealtimeNotifier`, DTO, OpenAPI и принятым IB-006. Три события подтверждены через WSS |

## 11. Остаточные риски и границы проверки

- Архивный Community MinIO остаётся уязвимой зависимостью внутри доверенных Docker-сетей. Caddy закрывает доступ к операциям записи через опубликованный порт. Прямой доступ к MinIO, недоверенные контейнеры в этих сетях и публичный Production находятся вне рекомендации. [Advisories MinIO](https://github.com/minio/minio/security/advisories) · [Официальный репозиторий MinIO](https://github.com/minio/minio).
- Полная Windows-проверка, нативный запуск из shell macOS, повторная сборка Docker на amd64, интерактивная установка доверия CA, нагрузка, многорепличная доставка и распределённые rate limits не проверялись на этапе 4. Production проверялся локально в части старта, login и отключения Dev endpoints. Дополнительные сценарии SignalR, JWT expiry, proxy и rate limits зафиксированы в [IB-006](IB-006-final-audit.md).
- Smoke-проверка покрыла основные сценарии. Полная матрица 35 операций и параметров не проходилась. Unit, Integration и E2E tests не добавлялись. Мигратор вывел предупреждение об отсутствии GSSAPI, но проверенные подключения с паролем и миграции завершились успешно. Readiness проверяет PostgreSQL. Доступность фото подтверждена отдельными запросами.

## 12. Финальные адреса

| Назначение | Адрес |
| --- | --- |
| Backend Base URL | `https://localhost:8443` |
| REST | `https://localhost:8443/api/…` |
| Swagger UI | `https://localhost:8443/swagger` |
| OpenAPI | `https://localhost:8443/openapi/v1.json` |
| SignalR | `https://localhost:8443/hubs/chat` |
| Liveness | `https://localhost:8443/health/live` |
| Readiness | `https://localhost:8443/health/ready` |
| Подписанные фото | `https://localhost:9443/…` |
| PostgreSQL | `127.0.0.1:2438` |

## Frontend Developer Quick Start

1. Запустите Docker Desktop или Docker Engine с Compose. Клонируйте ветку `feature/IB-007` из [ITMO-Buddy-backend](https://github.com/PrIIIcKoLyAZZZ/ITMO-Buddy-backend).
2. Из корня клона выполните `./scripts/dev-up.sh` на macOS/Linux или `scripts\dev-up.bat` в Windows CMD.
3. Используйте Backend Base URL `https://localhost:8443` и Swagger URL `https://localhost:8443/swagger`. CA, JWT Authorize и параметры клиента описаны в [README](../README.md).

**IB-007 — Stage 4/4: WAITING FOR USER APPROVAL**
