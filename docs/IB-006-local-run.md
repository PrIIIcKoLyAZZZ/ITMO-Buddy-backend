# Локальный запуск ITMO Buddy API

## Требования и переменные

Нужны .NET SDK 10, Docker Compose для PostgreSQL 17, OpenSSL, curl и jq. Для локального MinIO из исходников нужны Go 1.24 или новее и Git. Для HTTPS к хранилищу нужны Caddy 2 и mkcert.

Скопируйте `.env.example` в `.env` и замените все `REPLACE_*`. Используйте одну пару учётных данных PostgreSQL в переменных `POSTGRES_*` и `ConnectionStrings__Postgres`. Пример использует порт `2438`, опубликованный Compose на loopback. Для локальных паролей удобно использовать результат `openssl rand -hex 24`. JWT signing key сгенерируйте отдельно.

```sh
cp .env.example .env
chmod 600 .env
openssl rand -base64 48
```

После заполнения загрузите доверенный локальный файл в каждый shell, из которого запускаете API, EF CLI или MinIO. .NET получает значения из окружения процесса. Compose отдельно читает файл для подстановок в YAML.

```sh
set -a
. ./.env
set +a
docker compose up -d postgres
docker compose exec postgres pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"
```

## MinIO с доверенным локальным HTTPS

API запускается при валидной конфигурации MinIO. Для загрузки и чтения фото нужен работающий MinIO с существующим bucket. В примере loopback HTTP используется только для серверного клиента. Браузерный endpoint использует HTTPS и локальное DNS-имя с точкой.

Добавьте запись `127.0.0.1 storage.itmo-buddy.test` в системный hosts-файл. Из корня репозитория после загрузки окружения подготовьте временный каталог и сертификат. mkcert добавляет локальный CA в системное хранилище. При отдельном хранилище доверия браузера импортируйте этот CA туда.

```sh
local_dir="$(mktemp -d)"
mkcert -install
mkcert -cert-file "$local_dir/storage.pem" -key-file "$local_dir/storage-key.pem" storage.itmo-buddy.test
GOBIN="$local_dir" go install github.com/minio/minio@RELEASE.2025-09-07T16-13-09Z
MINIO_ROOT_USER="$Minio__AccessKey" MINIO_ROOT_PASSWORD="$Minio__SecretKey" MINIO_SITE_REGION="$Minio__Region" \
  "$local_dir/minio" server --address 127.0.0.1:9000 --console-address 127.0.0.1:9001 "$local_dir/minio-data" &
```

Caddy сохраняет исходный Host при проксировании на HTTP upstream. Это важно для проверки подписи URL.

```sh
cat > "$local_dir/Caddyfile" <<CADDY
https://storage.itmo-buddy.test:9443 {
    bind 127.0.0.1
    tls "$local_dir/storage.pem" "$local_dir/storage-key.pem"
    reverse_proxy 127.0.0.1:9000
}
CADDY
caddy run --config "$local_dir/Caddyfile" --adapter caddyfile &
curl --fail --aws-sigv4 "aws:amz:$Minio__Region:s3" \
  --user "$Minio__AccessKey:$Minio__SecretKey" -X PUT \
  "$Minio__ExternalEndpoint/$Minio__BucketName"
```

После входа проверьте `POST /api/profiles/me/photo` с JPEG или PNG и полем multipart `file`. Откройте полученный `photoUrl` в браузере до истечения срока действия ссылки. Проверка должна пройти с обычной валидацией TLS.

```sh
curl --fail -H "Authorization: Bearer $access" \
  -F 'file=@/path/to/photo.png;type=image/png' "$api/api/profiles/me/photo" > photo.json
curl --fail "$(jq -er .photoUrl photo.json)" -o downloaded-photo.png
```

`FileCleanup__Enabled=false` оставляет фоновое удаление выключенным. При удалении аккаунта и замене фото Application продолжает ставить файлы в очередь. Перед включением обработчика проверьте выделенный bucket, его доступы, PostgreSQL и чтение фото по подписанной ссылке.

## Миграции и запуск API

Установите `dotnet-ef` версии 10, если его ещё нет. Запускайте миграции вручную после проверки имени базы и адреса сервера. В текущей схеме применяются `20261117192121_InitialPersistence` и `20261118142755_AccountFileCleanup`. При запуске хоста миграции автоматически не применяются.

```sh
dotnet dev-certs https --trust
dotnet restore ITMO-Buddy-backend.slnx
dotnet build ITMO-Buddy-backend.slnx --no-restore
dotnet ef database update --no-build \
  --project src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence
dotnet run --no-build --project src/ITMO.Buddy/Api/ITMO.Buddy.Api --launch-profile https
```

```sh
api=https://localhost:7110
curl --fail "$api/health/live"
curl --fail "$api/health/ready"
curl --fail "$api/openapi/v1.json"
```

Оба health endpoint доступны без JWT. Liveness проверяет процесс. Readiness до трёх секунд проверяет подключение PostgreSQL и возвращает `503` при её недоступности. Readiness проверяет PostgreSQL, но не подтверждает доступность MinIO. Health endpoints и SignalR transport описаны здесь отдельно от OpenAPI REST-документа. Сам документ публикуется только в Development.

## Auth и SignalR smoke

Используйте временный аккаунт и защищайте локальные файлы с ответами и cookie. Укажите допустимый `Origin` во всех четырёх auth-операциях, в том числе при вызове из curl. Браузерный frontend на другом origin добавьте в `Cors:AllowedOrigins`. Для передачи `SameSite=Strict` cookie браузеру нужны frontend и API на одном site и credentialed requests.

```sh
umask 077
smoke_dir="$(mktemp -d)"
read -r -s local_password
jq -n --arg password "$local_password" \
  '{email:"local-smoke@example.invalid",password:$password,consentToPersonalDataProcessing:true}' > "$smoke_dir/register.json"
unset local_password
curl --fail -H "Origin: $api" -H 'Content-Type: application/json' \
  -c "$smoke_dir/cookies" --data-binary @"$smoke_dir/register.json" \
  "$api/api/auth/register" > "$smoke_dir/session.json"
curl --fail -H "Origin: $api" -H 'Content-Type: application/json' \
  -c "$smoke_dir/cookies" --data-binary @"$smoke_dir/register.json" \
  "$api/api/auth/login" > "$smoke_dir/session.json"
curl --fail -H "Origin: $api" -X POST -b "$smoke_dir/cookies" -c "$smoke_dir/cookies" \
  "$api/api/auth/refresh" > "$smoke_dir/session.json"
access="$(jq -er .accessToken "$smoke_dir/session.json")"
curl --fail -H "Authorization: Bearer $access" "$api/api/profiles/me"
```

Пароль для временного аккаунта должен соответствовать правилам регистрации. В браузерных инструментах проверьте `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth` и дату окончания cookie. При refresh cookie меняется. В JSON сессии присутствуют `userId`, `accessToken` и `expiresAtUtc`.

В существующем клиенте с официальным `@microsoft/signalr` создайте подключение к `https://localhost:7110/hubs/chat` через `new HubConnectionBuilder().withUrl(url, { accessTokenFactory: () => access }).build()`. Зарегистрируйте обработчик `MessageReceived` до `start()`. Войдите вторым аккаунтом, создайте диалог через взаимные лайки и отправьте сообщение через REST. Это позволяет наблюдать реальное событие на подключённом клиенте. После истечения JWT получите новый access token и подключитесь заново. Для logout отправьте `POST /api/auth/logout` с `Origin` и файлом cookie, затем удалите одноразовые файлы.

## Reverse proxy и лимиты

Вне Development укажите `AllowedHosts` как список реальных имён через точку с запятой, без порта и wildcard. `Https:Port` задаёт публичный HTTPS-порт для редиректа. HSTS действует вне Development в течение 30 дней. Для локального HTTPS используется отдельный профиль запуска.

По умолчанию список доверенных proxies пуст. Добавляйте конкретный IP непосредственного proxy в `ReverseProxy:KnownProxies` или ограниченный CIDR в `ReverseProxy:KnownNetworks`. Укажите `ForwardLimit` по числу доверенных hops. Внешний proxy должен перезаписывать входные `X-Forwarded-For` и `X-Forwarded-Proto`, передавать публичный Host без изменения и ограничивать доступ к backend listener. Для WebSocket нужен passthrough upgrade, для SSE нужна потоковая передача без буферизации. API использует только согласованные `X-Forwarded-For` и `X-Forwarded-Proto` от явно доверенных адресов. `X-Forwarded-Host` игнорируется. Настройте health probes на HTTPS либо запускайте их через доверенный proxy.

В access/error logs proxy, балансировщика и средств наблюдения записывайте путь без query. Исключите `Authorization`, `Cookie`, `Set-Cookie`, `access_token`, SignalR `ConnectionToken` и тела запросов. Это отдельная настройка внешней инфраструктуры.

| Операция | Общий лимит по умолчанию |
|---|---|
| Login | 60 запросов за минуту по IP |
| Register | 12 запросов за 10 минут по IP |
| Refresh | 60 запросов за минуту по IP |
| Создание интересов и навыков | 30 запросов за минуту на пользователя, общий лимит |
| Загрузка фото | 10 запросов за минуту на пользователя |
| Отправка сообщений | 120 запросов за минуту на пользователя |

Дополнительно login сериализуется для одного IP. После пяти ошибок `auth.invalid_credentials` за скользящие десять минут следующие попытки получают `429 auth.login_rate_limited` без проверки пароля. Успешные входы и ошибки инфраструктуры не увеличивают счётчик. Ошибки привязки HTTP учитывает общий лимит login. `Retry-After` возвращается в секундах, если время известно. Таблица credential failures хранит до 10 000 IP по умолчанию и периодически удаляет истёкшие записи.

In-memory Rate Limiting действует только в рамках одного экземпляра API и сбрасывается после рестарта. Для развёртывания с несколькими репликами общий лимит требует отдельного решения на уровне инфраструктуры. Пропуски рекомендаций также хранятся в памяти экземпляра.
