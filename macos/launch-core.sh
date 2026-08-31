#!/bin/bash
# Steam invokes this silently before the unmodified game; there is no separate launcher window.
set -euo pipefail
MOD_ROOT="$(cd -- "$(dirname -- "$0")" && pwd)"
GAME_APP="${CHILL_GAME_APP:-$HOME/Library/Application Support/Steam/steamapps/common/Chill with You Lo-Fi Story/Chill With You.app}"
MODE="${1:-}"
if [[ "$MODE" == --steam ]]; then
    shift
    [[ $# -gt 0 ]] || { echo 'Steam 未提供游戏路径。' >&2; exit 1; }
    GAME_INPUT="$1"
    shift
    if [[ "$GAME_INPUT" == *.app ]]; then
        GAME_APP="$GAME_INPUT"
        GAME_NAME=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$GAME_APP/Contents/Info.plist")
        GAME_BIN="$GAME_APP/Contents/MacOS/$GAME_NAME"
    else
        GAME_BIN="$GAME_INPUT"
        GAME_NAME="$(basename -- "$GAME_BIN")"
    fi
else
    if [[ "$MODE" == --check ]]; then shift; elif [[ -n "$MODE" ]]; then echo '未知启动选项。' >&2; exit 1; fi
    GAME_NAME=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$GAME_APP/Contents/Info.plist")
    GAME_BIN="$GAME_APP/Contents/MacOS/$GAME_NAME"
fi
[[ -x "$GAME_BIN" ]] || { echo '未找到游戏可执行文件。' >&2; exit 1; }
if [[ "$(/usr/sbin/sysctl -n hw.optional.arm64 2>/dev/null || echo 0)" == 1 ]]; then NATIVE_ARCH=arm64; else NATIVE_ARCH=x86_64; fi
RUN_ARCH="${MUSICBRIDGE_ARCH:-$NATIVE_ARCH}"
[[ "$RUN_ARCH" == arm64 || "$RUN_ARCH" == x86_64 ]] || { echo '不支持的架构。' >&2; exit 1; }
/usr/bin/lipo "$GAME_BIN" -verify_arch "$RUN_ARCH" >/dev/null 2>&1 || { echo '游戏缺少所需 CPU 架构。' >&2; exit 1; }
[[ -f "$MOD_ROOT/BepInEx/core/MonoMod.Core.dll" && -f "$MOD_ROOT/libdoorstop.dylib" && -f "$MOD_ROOT/BepInEx/plugins/ChillWithYouMusicBridge/MusicBridge.Plugin.dll" ]] || { echo '原生 Mod 运行库不完整，请重新构建。' >&2; exit 1; }
[[ -z "${DOORSTOP_DISABLE:-}" ]] || { echo '当前环境设置了 DOORSTOP_DISABLE。' >&2; exit 1; }
if [[ "$MODE" == --check ]]; then
    echo "检查通过：$RUN_ARCH / 原生兼容 BepInEx / 不修改游戏程序"
    echo "Mod：$MOD_ROOT"
    exit 0
fi
if /usr/bin/pgrep -x "$GAME_NAME" >/dev/null; then echo '游戏已运行，请先正常退出。' >&2; exit 1; fi
mkdir -p "$MOD_ROOT/logs"
export DOORSTOP_ENABLED=1
export DOORSTOP_TARGET_ASSEMBLY="$MOD_ROOT/BepInEx/core/BepInEx.Preloader.dll"
export DOORSTOP_MONO_DEBUG_ENABLED=0 DOORSTOP_MONO_DEBUG_SUSPEND=0
export SteamAppId=3548580
printf '[MusicBridge] launch architecture=%s mode=%s\n' "$RUN_ARCH" "${MODE:-manual}" >> "$MOD_ROOT/logs/launcher.log"
exec /usr/bin/arch "-$RUN_ARCH" -e "DYLD_INSERT_LIBRARIES=$MOD_ROOT/libdoorstop.dylib" "$GAME_BIN" "$@" -logFile "$MOD_ROOT/logs/Unity.log" >> "$MOD_ROOT/logs/launcher.log" 2>&1
