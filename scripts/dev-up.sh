#!/bin/sh
set -eu
CDPATH='' cd -- "$(dirname -- "$0")/.."
. ./scripts/dev-common.sh

umask 077
check_docker
probe_image=curlimages/curl:8.17.0
state_dir=.docker-local
ca_file=$state_dir/root.crt
mkdir -p "$state_dir"
temp_file=
trap '[ -z "$temp_file" ] || rm -f -- "$temp_file"' EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

value() {
    # Input comes from Compose's dotenv parser or Docker inspect, never from shell evaluation.
    awk -v key="$1=" 'index($0, key) == 1 { result = substr($0, length(key) + 1); count++ }
        END { if (count == 1) print result; else exit 1 }'
}

random_hex() {
    od -An -v -tx1 -N "$1" /dev/urandom | tr -d ' \n'
}

volume_exists=false
if docker volume inspect "${project}_postgres_data" >/dev/null 2>&1; then
    volume_exists=true
fi
postgres_name=$(awk '$1 == "container_name:" { print $2; exit }' compose.yml)
owner=$(docker inspect --type=container --format '{{index .Config.Labels "com.docker.compose.project"}}/{{index .Config.Labels "com.docker.compose.service"}}' \
    "$postgres_name" 2>/dev/null) || owner=
[ -z "$owner" ] || [ "$owner" = "$project/postgres" ] ||
    fail "Container $postgres_name belongs to a different Compose project. Check its ownership before continuing."

if [ ! -e .env.docker ]; then
    if [ -f .env ]; then
        postgres_settings=$(docker compose --env-file .env -p "$project" -f compose.yml config --environment 2>/dev/null) ||
            fail 'Cannot read PostgreSQL settings from .env. Set the working POSTGRES_* values in .env.docker.'
    elif [ "$volume_exists" = true ]; then
        postgres_id=$(container postgres)
        [ -n "$postgres_id" ] || fail 'Existing PostgreSQL volume has no container or .env. Set the working POSTGRES_* values in .env.docker.'
        postgres_settings=$(docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$postgres_id")
    else
        postgres_settings=$(printf 'POSTGRES_USER=itmo_buddy\nPOSTGRES_DB=itmo_buddy\nPOSTGRES_PASSWORD=%s\n' "$(random_hex 24)")
    fi
    pg_user=$(printf '%s\n' "$postgres_settings" | value POSTGRES_USER) || fail 'Missing or ambiguous POSTGRES_USER.'
    pg_db=$(printf '%s\n' "$postgres_settings" | value POSTGRES_DB) || fail 'Missing or ambiguous POSTGRES_DB.'
    pg_password=$(printf '%s\n' "$postgres_settings" | value POSTGRES_PASSWORD) || fail 'Missing or ambiguous POSTGRES_PASSWORD.'
    unset postgres_settings
    for identifier in "$pg_user" "$pg_db"; do
        case $identifier in
            ''|*[!a-zA-Z0-9_]*) fail 'Set POSTGRES_USER and POSTGRES_DB as simple PostgreSQL identifiers in .env.docker.' ;;
        esac
    done
    case $pg_password in
        ''|REPLACE_*|*"'"*|*'\'*) fail 'Set the working POSTGRES_PASSWORD in .env.docker using Compose dotenv quoting.' ;;
    esac
    jwt=$(head -c 48 /dev/urandom | base64 | tr -d '\r\n')
    [ "${#jwt}" -eq 64 ] || fail 'Could not generate a 48-byte JWT signing key.'
    minio_user=$(random_hex 12)
    minio_password=$(random_hex 24)
    [ "${#minio_user}" -eq 24 ] && [ "${#minio_password}" -eq 48 ] || fail 'Could not generate MinIO credentials.'
    temp_file=$(mktemp ./.env.docker.XXXXXXXX)
    {
        printf "POSTGRES_USER='%s'\nPOSTGRES_DB='%s'\nPOSTGRES_PASSWORD='%s'\n" "$pg_user" "$pg_db" "$pg_password"
        printf 'JWT_SIGNING_KEY=%s\nMINIO_ROOT_USER=itmo_%s\nMINIO_ROOT_PASSWORD=%s\n' "$jwt" "$minio_user" "$minio_password"
        printf '%s\n' 'MINIO_BUCKET=itmo-buddy-local' 'CORS_ORIGIN_0=https://localhost:5173' 'CORS_ORIGIN_1=https://localhost:3000'
    } > "$temp_file"
    ln "$temp_file" .env.docker || fail 'Could not create .env.docker without overwriting an existing file.'
    rm -f -- "$temp_file"
    temp_file=
    unset jwt minio_user minio_password
    printf 'Created .env.docker with local credentials.\n'
fi

require_env
settings=$(compose config --environment 2>/dev/null) || fail 'Could not read .env.docker.'
for key in POSTGRES_USER POSTGRES_DB POSTGRES_PASSWORD JWT_SIGNING_KEY MINIO_ROOT_USER MINIO_ROOT_PASSWORD MINIO_BUCKET; do
    setting=$(printf '%s\n' "$settings" | value "$key") ||
        fail "Missing or ambiguous $key in .env.docker."
    case $setting in ''|REPLACE_*) fail "Set $key in .env.docker." ;; esac
    case $key in
        POSTGRES_USER|POSTGRES_DB)
            case $setting in *[!a-zA-Z0-9_]* ) fail "$key must be a simple PostgreSQL identifier." ;; esac
            ;;
        MINIO_ROOT_USER) [ "${#setting}" -ge 3 ] || fail 'MINIO_ROOT_USER needs at least 3 characters.' ;;
        MINIO_ROOT_PASSWORD) [ "${#setting}" -ge 8 ] || fail 'MINIO_ROOT_PASSWORD needs at least 8 characters.' ;;
        MINIO_BUCKET)
            printf '%s\n' "$setting" | LC_ALL=C grep -Eq '^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$' ||
                fail 'MINIO_BUCKET must be a valid lowercase S3 bucket name.'
            case $setting in *..*) fail 'MINIO_BUCKET must not contain adjacent dots.' ;; esac
            if printf '%s\n' "$setting" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$'; then
                fail 'MINIO_BUCKET must not be an IP address.'
            fi
            ;;
        JWT_SIGNING_KEY)
            if [ "$(uname -s)" = Darwin ]; then decode=-D; else decode=-d; fi
            key_bytes=$(printf '%s' "$setting" | base64 "$decode" 2>/dev/null | wc -c)
            printf '%s' "$setting" | base64 "$decode" >/dev/null 2>&1 &&
                [ "$key_bytes" -ge 32 ] || fail 'JWT_SIGNING_KEY must decode from Base64 to at least 32 bytes.'
            ;;
    esac
done
pg_user=$(printf '%s\n' "$settings" | value POSTGRES_USER)
pg_db=$(printf '%s\n' "$settings" | value POSTGRES_DB)
pg_password=$(printf '%s\n' "$settings" | value POSTGRES_PASSWORD)
unset settings setting

# A short-lived Docker port reservation also catches non-Docker listeners.
set --
for mapping in postgres:2438:5432 caddy:8443:8443 caddy:9443:9443; do
    service=${mapping%%:*}
    ports=${mapping#*:}
    host_port=${ports%%:*}
    service_port=${ports#*:}
    id=$(container "$service")
    if [ -n "$id" ] && docker port "$id" "$service_port/tcp" 2>/dev/null | grep -Fxq "127.0.0.1:$host_port"; then
        continue
    fi
    set -- "$@" -p "127.0.0.1:$host_port:$service_port"
done
if [ "$#" -gt 0 ]; then
    docker run --rm "$@" --entrypoint /bin/sh "$probe_image" -c true ||
        fail 'Could not reserve an expected loopback port (2438, 8443, 9443). Check the Docker error and stop only the conflicting service.'
fi

if [ "$volume_exists" = true ]; then
    compose up -d --no-recreate postgres || fail 'Could not start existing PostgreSQL.'
    postgres_id=$(container postgres)
    mounted=$(docker inspect --format '{{range .Mounts}}{{if eq .Destination "/var/lib/postgresql/data"}}{{.Name}}{{end}}{{end}}' "$postgres_id")
    [ "$mounted" = "${project}_postgres_data" ] || fail 'Existing PostgreSQL container uses an unexpected volume.'
    deadline=$(( $(date +%s) + 120 ))
    while :; do
        # PostgreSQL may trust loopback connections inside its own container.
        if PGPASSWORD=$pg_password docker exec -e PGPASSWORD -e PGCONNECT_TIMEOUT=3 "$postgres_id" \
            psql -X -w -h postgres -U "$pg_user" -d "$pg_db" -Atqc 'SELECT 1' >/dev/null 2>&1; then
            break
        fi
        state=$(docker inspect --format '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{end}}' "$postgres_id")
        case $state in
            'running starting') [ "$(date +%s)" -lt "$deadline" ] && sleep 2 && continue ;;
        esac
        fail 'Cannot authenticate to existing PostgreSQL. Use its working POSTGRES_* values in .env.docker and inspect postgres logs. The volume was preserved.'
    done
fi
unset pg_password

if ! compose up -d --build --wait --wait-timeout 120; then
    compose ps -a >&2 || :
    fail 'Compose startup failed. See scripts/dev-logs.sh [service]. For an overlapping Docker subnet, adjust CADDY_SUBNET, CADDY_DYNAMIC_RANGE and CADDY_IP together.'
fi

temp_file=$(mktemp "$state_dir/root.XXXXXXXX")
deadline=$(( $(date +%s) + 120 ))
while :; do
    for job in migrator bucket-init; do
        id=$(container "$job")
        status=
        [ -z "$id" ] || status=$(docker inspect --format '{{.State.Status}} {{.State.ExitCode}}' "$id")
        case $status in
            'exited 0') ;;
            exited*) fail "$job failed. See scripts/dev-logs.sh $job." ;;
            *) break ;;
        esac
    done
    if [ "$job" = bucket-init ] && [ "$status" = 'exited 0' ]; then
        postgres_id=$(container postgres)
        minio_id=$(container minio)
        caddy_id=$(container caddy)
        if [ -n "$postgres_id" ] && [ -n "$minio_id" ] && [ -n "$caddy_id" ] &&
            [ "$(docker inspect --format '{{.State.Health.Status}}' "$postgres_id")" = healthy ] &&
            [ "$(docker inspect --format '{{.State.Health.Status}}' "$minio_id")" = healthy ] &&
            docker exec "$caddy_id" cat /data/caddy/pki/authorities/local/root.crt > "$temp_file" 2>/dev/null &&
            grep -q '^-----BEGIN CERTIFICATE-----$' "$temp_file"; then
            if command -v curl >/dev/null 2>&1; then
                code=$(curl --noproxy '*' --cacert "$temp_file" -s --max-time 5 -o /dev/null -w '%{http_code}' \
                    https://localhost:8443/health/ready) || code=
            else
                code=$(docker run --rm --user 0:0 --network "container:$caddy_id" \
                    --mount "type=bind,source=$PWD/$temp_file,target=/root-ca.crt,readonly" "$probe_image" \
                    --noproxy '*' --cacert /root-ca.crt -s --max-time 5 -o /dev/null -w '%{http_code}' \
                    https://localhost:8443/health/ready) || code=
            fi
            [ "$code" != 200 ] || break
        fi
    fi
    if [ "$(date +%s)" -ge "$deadline" ]; then
        compose ps -a >&2 || :
        fail 'Timed out waiting for HTTPS readiness. See scripts/dev-logs.sh api or caddy.'
    fi
    sleep 2
done

if [ -f "$ca_file" ] && ! cmp -s "$temp_file" "$ca_file"; then
    printf 'The Caddy CA changed. Remove trust for the old CA before trusting the new one (see README).\n' >&2
fi
mv -f -- "$temp_file" "$ca_file"
temp_file=
printf 'Public Caddy CA: %s/%s\n' "$PWD" "$ca_file"

if [ -t 0 ] && ! cmp -s "$ca_file" "$state_dir/trusted-root.crt"; then
    printf '%s\n' 'Trusting this local root CA lets certificates it signs be trusted on this computer.' \
        'Installation may ask for administrator rights to update the system trust store.'
    printf 'Install trust for the ITMO Buddy local CA now? [y/N] '
    read -r answer || answer=
    case $answer in
        y|Y|yes|YES)
            installed=false
            if [ "$(uname -s)" = Darwin ]; then
                security add-trusted-cert -r trustRoot -p ssl -k "$HOME/Library/Keychains/login.keychain-db" "$ca_file" &&
                    installed=true
            elif [ -d /usr/local/share/ca-certificates ] && command -v update-ca-certificates >/dev/null 2>&1; then
                sudo install -m 0644 "$ca_file" /usr/local/share/ca-certificates/itmo-buddy-local.crt &&
                    sudo update-ca-certificates && installed=true
            elif [ -d /etc/pki/ca-trust/source/anchors ] && command -v update-ca-trust >/dev/null 2>&1; then
                sudo install -m 0644 "$ca_file" /etc/pki/ca-trust/source/anchors/itmo-buddy-local.crt &&
                    sudo update-ca-trust extract && installed=true
            fi
            if [ "$installed" = true ]; then
                cp "$ca_file" "$state_dir/trusted-root.crt"
            else
                printf 'Automatic trust installation failed or is unavailable. See the manual CA instructions in README.\n'
            fi
            ;;
    esac
fi
if ! cmp -s "$ca_file" "$state_dir/trusted-root.crt"; then
    printf 'The backend works. Your browser may still require trust for the local CA (see README).\n'
fi

cat <<'READY'
ITMO Buddy Backend is ready.

Backend:
https://localhost:8443

Swagger UI:
https://localhost:8443/swagger

OpenAPI:
https://localhost:8443/openapi/v1.json

Health:
https://localhost:8443/health/ready

SignalR:
https://localhost:8443/hubs/chat

MinIO signed URLs:
https://localhost:9443

PostgreSQL:
127.0.0.1:2438
READY
