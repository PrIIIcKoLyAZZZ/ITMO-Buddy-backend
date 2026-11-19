#!/bin/sh
set -eu
CDPATH='' cd -- "$(dirname -- "$0")/.."
. ./scripts/dev-common.sh
check_docker
require_env
compose down
