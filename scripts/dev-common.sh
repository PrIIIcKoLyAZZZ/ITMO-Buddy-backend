# Shared helpers for the POSIX dev scripts.
set +x

fail() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

compose() {
    docker compose --env-file .env.docker -p "$project" -f compose.yml -f compose.frontend.yml "$@"
}

container() {
    docker ps -aq --filter "label=com.docker.compose.project=$project" \
        --filter "label=com.docker.compose.service=$1" --filter label=com.docker.compose.oneoff=False
}

check_docker() {
    command -v docker >/dev/null 2>&1 || fail 'Install Docker Desktop or Docker Engine with Compose V2.'
    docker compose version >/dev/null 2>&1 || fail 'Docker Compose V2 is unavailable.'
    docker info >/dev/null 2>&1 || fail 'Docker daemon is unavailable. Start Docker, then check docker info.'
    [ -f compose.yml ] && [ -f compose.frontend.yml ] && [ -f docker/Caddyfile ] ||
        fail 'Compose files or docker/Caddyfile are missing.'
    project=$(awk '$1 == "name:" { print $2; exit }' compose.yml)
    [ -n "$project" ] || fail 'Missing Compose project name in compose.yml.'
    unset POSTGRES_USER POSTGRES_PASSWORD POSTGRES_DB JWT_SIGNING_KEY MINIO_ROOT_USER MINIO_ROOT_PASSWORD \
        MINIO_BUCKET CORS_ORIGIN_0 CORS_ORIGIN_1 CADDY_SUBNET CADDY_DYNAMIC_RANGE CADDY_IP
}

require_env() {
    [ -f .env.docker ] || fail 'Missing .env.docker. Run scripts/dev-up.sh first.'
    compose config -q >/dev/null 2>&1 ||
        fail 'Invalid .env.docker or Compose YAML. Check required values against .env.docker.example.'
}
