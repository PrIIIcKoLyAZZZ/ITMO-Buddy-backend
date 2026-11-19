#!/bin/sh
set -eu
CDPATH='' cd -- "$(dirname -- "$0")/.."
. ./scripts/dev-common.sh
check_docker
require_env
case $#:${1-} in
    0:) ;;
    1:api|1:caddy|1:postgres|1:minio|1:migrator|1:bucket-init) ;;
    *) fail 'Usage: dev-logs.sh [api|caddy|postgres|minio|migrator|bucket-init]' ;;
esac
exec docker compose --env-file .env.docker -p "$project" -f compose.yml -f compose.frontend.yml logs --tail=100 --follow "$@"
