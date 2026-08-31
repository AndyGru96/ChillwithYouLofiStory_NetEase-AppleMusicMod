#!/bin/bash
set -euo pipefail
PROJECT_DIR="$(cd -- "$(dirname -- "$0")" && pwd)"
exec "$PROJECT_DIR/macos/dist/ChillMusicMac/Start-Music-Mod.command" "$@"
