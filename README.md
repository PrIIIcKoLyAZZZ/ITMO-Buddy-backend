# ITMO Buddy Backend

ITMO Buddy Backend предоставляет REST API и SignalR для приложения ITMO Buddy. REST использует JWT Bearer для защищённых операций. SignalR доставляет события в реальном времени. PostgreSQL хранит данные приложения, MinIO хранит фотографии, а Caddy предоставляет локальный HTTPS. Docker Compose запускает весь стек и выполняет начальную настройку базы и хранилища.

## Что установить

Установите и запустите Docker Desktop либо Docker Engine с Compose V2. Для запуска Backend на компьютере достаточно Docker. .NET SDK, dotnet-ef, Go, PostgreSQL, MinIO и Caddy запускаются или используются при сборке внутри Docker.

## Быстрый старт на Windows

Клонируйте репозиторий и из его корня выполните в Windows Command Prompt:

```bat
scripts\dev-up.bat
```

## Быстрый старт на macOS и Linux

Клонируйте репозиторий и из его корня выполните:

```sh
./scripts/dev-up.sh
```

Скрипты принимают запуск и по пути из другой директории. Первая сборка скачивает базовые образы, зависимости .NET и исходники MinIO, поэтому ей нужен интернет и она займёт больше времени. Скрипт создаёт `.env.docker`, собирает стек, применяет миграции, создаёт bucket и дожидается ответа HTTPS. Существующий `.env.docker` используется при повторном запуске.

После проверки готовности скрипт экспортирует публичный сертификат CA и в интерактивном терминале предлагает установить доверие. Доверие к корневому CA позволяет компьютеру доверять сертификатам, которые он подписал. Если вы пропустили импорт, Backend продолжит работать, а браузеру может потребоваться [установить доверие позже](#локальный-https-и-ca).

## Адреса подключения

**Frontend должен использовать `https://localhost:8443` как Backend Base URL.**

| Назначение | Адрес |
| --- | --- |
| Backend Base URL | `https://localhost:8443` |
| REST API | `https://localhost:8443/api/...` |
| SignalR | `https://localhost:8443/hubs/chat` |
| OpenAPI | `https://localhost:8443/openapi/v1.json` |
| Liveness | `https://localhost:8443/health/live` |
| Readiness | `https://localhost:8443/health/ready` |
| MinIO signed URLs | `https://localhost:9443/...` |
| PostgreSQL | `127.0.0.1:2438` |

`/api/...` обозначает префикс маршрутов. Используйте конкретные маршруты из OpenAPI. Внутренние адреса Docker, включая `http://api:8080`, предназначены для связи контейнеров.

## Проверка Backend

После установки CA откройте в браузере `https://localhost:8443/health/ready` и `https://localhost:8443/openapi/v1.json`. Если у вас есть curl, можно проверить HTTPS с экспортированным CA без изменения системного хранилища:

```sh
curl --fail --cacert .docker-local/root.crt https://localhost:8443/health/live
curl --fail --cacert .docker-local/root.crt https://localhost:8443/health/ready
curl --fail --cacert .docker-local/root.crt https://localhost:8443/openapi/v1.json
```

В Windows CMD укажите `curl.exe`. Liveness проверяет, что процесс API отвечает. Readiness проверяет подключение к PostgreSQL. Для функциональной проверки MinIO загрузите и скачайте фото.

## JWT Authentication

Передавайте токен доступа в защищённых REST-запросах:

```http
Authorization: Bearer <access-token>
```

| Метод и маршрут | Назначение |
| --- | --- |
| `POST /api/auth/register` | Создаёт аккаунт и сессию. Тело содержит `email`, `password` и `consentToPersonalDataProcessing` |
| `POST /api/auth/login` | Создаёт сессию по `email` и `password` |
| `POST /api/auth/refresh` | Использует refresh-cookie, выдаёт новый access token и обновляет cookie |
| `POST /api/auth/logout` | Завершает сессию и очищает refresh-cookie |

Register, login и refresh возвращают `userId`, `accessToken` и `expiresAtUtc`. Все четыре auth-маршрута требуют корректный `Origin`. Браузер формирует его автоматически. Для ручного запроса используйте origin API либо разрешённый origin фронтенда.

## Refresh-cookie

Cookie имеет атрибуты `HttpOnly`, `Secure`, `SameSite=Strict` и `Path=/api/auth`. Браузер отправляет его для HTTPS-запросов к auth-маршрутам при соблюдении same-site. При запросах с другого origin используйте `credentials: 'include'` в том числе для register/login, чтобы браузер принял `Set-Cookie`.

Origin включает схему, hostname и порт. Например, `https://localhost:5173` и `https://localhost:8443` — разные origins, поэтому требуется CORS. Site учитывает схему и сайт, но не порт, поэтому эти два HTTPS-адреса на localhost остаются same-site. Для надёжной локальной разработки используйте HTTPS и один hostname `localhost` с обеих сторон. Подстановка `127.0.0.1` или смена схемы меняет браузерный контекст.

## SignalR

Hub доступен по `https://localhost:8443/hubs/chat`. Соединение требует Bearer access token. В клиенте SignalR токен передаётся через `accessTokenFactory`, а подписку на `MessageReceived` регистрируют до запуска соединения. Когда токен истекает, получите новый access token и подключитесь заново. Hub предназначен для протокола SignalR, поэтому обычное открытие его URL в браузере не проверяет WebSocket или авторизацию.

## Загрузка фотографий

Вызовите `POST /api/profiles/me/photo` с Bearer токеном и `multipart/form-data`. Имя поля — `file`, формат — PNG или JPEG, предел — 5 МиБ. Ответ содержит `photoUrl`, подписанный HTTPS URL MinIO. Используйте URL целиком, включая query. Срок действия по умолчанию составляет 5 минут. Для новой ссылки можно запросить `GET /api/profiles/me`. Реквизиты MinIO хранятся на стороне Backend.

## Скрипты

| macOS / Linux | Windows CMD | Назначение |
| --- | --- | --- |
| `./scripts/dev-up.sh` | `scripts\dev-up.bat` | Создаёт локальную конфигурацию, собирает сервисы и проверяет готовность |
| `./scripts/dev-down.sh` | `scripts\dev-down.bat` | Останавливает проект и удаляет его контейнеры и сети. Volumes и локальные файлы сохраняются |
| `./scripts/dev-logs.sh [service]` | `scripts\dev-logs.bat [service]` | Показывает хвост логов и продолжает выводить новые строки. Ctrl+C закрывает просмотр |

Доступные имена сервисов для логов — `api`, `caddy`, `postgres`, `minio`, `migrator` и `bucket-init`. Например, `./scripts/dev-logs.sh migrator` или `scripts\dev-logs.bat migrator`.

## Где хранятся данные

| Ресурс | Содержимое |
| --- | --- |
| `itmo-buddy-local_postgres_data` | PostgreSQL и история миграций |
| `itmo-buddy-local_minio_data` | Bucket и объекты MinIO |
| `itmo-buddy-local_caddy_data` | CA Caddy, его закрытый ключ и сертификаты |
| `.env.docker` | Реквизиты сервисов, JWT signing key и настройки запуска. Остаётся локальным |
| `.docker-local/root.crt` | Экспортированный публичный CA. Если доверие установил скрипт, он сохраняет копию в `trusted-root.crt` |

`.env.docker.example` документирует переменные. Файл `.env` относится к ручному запуску IB-006. При наличии прежнего PostgreSQL скрипт может перенести `POSTGRES_*` из `.env` или конфигурации контейнера и проверяет подключение к существующей базе до запуска остальных сервисов. Если реквизиты нельзя перенести или они устарели, укажите действующие значения вручную в `.env.docker`. Docker использует `POSTGRES_USER`, `POSTGRES_DB` и `POSTGRES_PASSWORD` при первой инициализации volume. Последующая правка файла сама по себе пароль в PostgreSQL не меняет.

## Повторный запуск

Повторите команду `dev-up` после остановки или обновления кода. Скрипт сохраняет секреты из `.env.docker`. Мигратор применяет только недостающие миграции, задача bucket использует существующий bucket, а Caddy читает прежний CA из volume. Обычный цикл работы — `dev-up`, разработка, затем `dev-down`.

## Локальный HTTPS и CA

Чтобы установить доверие позже, повторно запустите `dev-up` в интерактивном терминале и подтвердите импорт либо используйте публичный `.docker-local/root.crt` вручную.

- **macOS** — откройте сертификат в «Связке ключей», импортируйте в связку входа и настройте доверие для SSL. Изменение доверия может потребовать подтверждения macOS.
- **Linux** — на Debian/Ubuntu поместите публичный сертификат в `/usr/local/share/ca-certificates/itmo-buddy-local.crt` и выполните `sudo update-ca-certificates`. На Fedora/RHEL используйте `/etc/pki/ca-trust/source/anchors/itmo-buddy-local.crt` и `sudo update-ca-trust extract`. На остальных системах используйте процедуру своего дистрибутива. Если браузер использует отдельное хранилище, импортируйте сертификат как доверенный центр в настройках браузера.
- **Windows CMD** — выполните `certutil -user -addstore Root .docker-local\root.crt` после того, как приняли решение доверять CA. Команда использует хранилище текущего пользователя.

Для удаления доверия найдите прежний сертификат по отпечатку сохранённого `trusted-root.crt` или резервной копии публичного CA. В macOS удалите запись в «Связке ключей». В Linux удалите соответствующий файл CA и повторите обновление хранилища. В Windows откройте `certmgr.msc` и удалите запись из доверенных корневых центров текущего пользователя. После удаления доверия уберите `.docker-local/trusted-root.crt`, если он остался. При необходимости повторите удаление в отдельном хранилище браузера.

## Полный сброс

**Удаление Docker volumes необратимо уничтожает данные PostgreSQL и объекты MinIO.** Перед сбросом сделайте резервную копию нужных данных. Для полного сброса остановите стек, снимите доверие со старого CA по инструкции выше, затем вручную выполните:

```sh
docker volume rm itmo-buddy-local_postgres_data itmo-buddy-local_minio_data itmo-buddy-local_caddy_data
```

Если нужно сохранить CA и доверие, исключите `itmo-buddy-local_caddy_data` из этой команды. Следующий запуск восстановит пустую БД и bucket. При удалении CA volume следующий запуск создаст новый CA. `.env.docker` сохраняет текущие секреты. Удаляйте или архивируйте его отдельно только для намеренной смены реквизитов после сброса данных. Если сохранён ручной `.env`, скрипт снова использует его `POSTGRES_*`.

## Дополнительные Frontend origins

Укажите нужные адреса в `CORS_ORIGIN_0` и `CORS_ORIGIN_1` файла `.env.docker` и повторите `dev-up`. По умолчанию разрешены `https://localhost:5173` и `https://localhost:3000`. Значение должно содержать точную схему, hostname и порт без пути или завершающего слеша. Для credentialed CORS используйте явные origins.

## Troubleshooting

| Ситуация | Что проверить |
| --- | --- |
| Docker daemon недоступен | Запустите Docker Desktop или Docker Engine, проверьте `docker info` и активный Docker context |
| Docker Compose отсутствует | Проверьте `docker compose version`. Установите или обновите Compose V2 |
| Порт занят | Проверьте контейнеры и локальные процессы на `2438`, `8443`, `9443`. Освободите порт, если можете остановить его владельца |
| Конфликт Docker subnet | Выберите свободную подсеть и задайте согласованные `CADDY_SUBNET`, `CADDY_DYNAMIC_RANGE` и `CADDY_IP`. Статический IP должен быть внутри подсети и вне динамического диапазона. Затем выполните `dev-down` и `dev-up` |
| PostgreSQL credentials не совпадают с volume | Восстановите действующие `POSTGRES_*` из прежней конфигурации. Проверьте логи `postgres`. Сохраните volume |
| Migrator завершился с ошибкой | Посмотрите логи `migrator` и `postgres`, проверьте подключение и права на миграции и `pg_trgm`, затем повторите запуск |
| MinIO недоступен | Посмотрите логи `minio`, статус healthcheck и доступность volume |
| Bucket initialization failed | Посмотрите логи `bucket-init` и `minio`, проверьте MinIO credentials и имя bucket в `.env.docker` |
| HTTPS-сертификат не доверен | Сверьте CA в `.docker-local/root.crt`, проверьте через `--cacert`, затем установите доверие для нужного браузера |
| `/health/ready` возвращает 503 | Проверьте логи `api`, `postgres` и `migrator`, статус PostgreSQL и настройки подключения |
| CORS или Origin error | Сверьте точный HTTPS origin с `CORS_ORIGIN_0` и `CORS_ORIGIN_1`, затем повторите `dev-up` |
| Refresh-cookie не передаётся | Проверьте HTTPS, общий hostname `localhost`, `credentials: 'include'` и атрибуты cookie в инструментах браузера |
| SignalR не подключается | Проверьте `/hubs/chat`, срок JWT, доверие CA, origin и статус Upgrade в инструментах браузера |
| Подписанная ссылка MinIO возвращает 403 | Получите новую ссылку через API, используйте её целиком и проверьте системное время, hostname `localhost` и порт `9443` |
| После перезапуска изменился сертификат | Сертификат сервера может обновляться. Если изменился корневой CA, проверьте volume Caddy, снимите доверие со старого CA и подтвердите новый |
| Docker build не проходит | Прочитайте ошибку конкретного build stage. Проверьте доступ Docker к MCR, Docker Hub, NuGet, GitHub и Go module proxy, а также свободное место |

## Архитектура Backend

Solution содержит семь проектов .NET 10.

- **ITMO.Buddy.Api** — REST, SignalR, JWT, OpenAPI и composition root.
- **ITMO.Buddy.Application** — прикладные сценарии и валидация.
- **ITMO.Buddy.Application.Contracts** — контракты запросов, ответов и событий.
- **ITMO.Buddy.Application.Abstractions** — интерфейсы прикладных зависимостей.
- **ITMO.Buddy.Domain** — доменные модели и правила.
- **ITMO.Buddy.Infrastructure** — реализации JWT, хеширования паролей и файлового хранилища.
- **ITMO.Buddy.Infrastructure.Persistence** — EF Core, репозитории, PostgreSQL и миграции.

Подробности локального стека описаны в [архитектуре IB-007](docs/IB-007-docker-architecture.md). Для диагностики вне Docker доступна [ручная инструкция IB-006](docs/IB-006-local-run.md) со своими адресами и настройками.

## Область ответственности

Этот репозиторий содержит Backend и инфраструктуру локальной разработки. Frontend реализуется отдельно.
