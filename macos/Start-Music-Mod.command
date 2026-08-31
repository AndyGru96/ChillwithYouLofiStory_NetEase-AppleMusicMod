#!/bin/bash
set -euo pipefail
MOD_ROOT="$(cd -- "$(dirname -- "$0")" && pwd)"
exec "$MOD_ROOT/launch-core.sh" "$@"
