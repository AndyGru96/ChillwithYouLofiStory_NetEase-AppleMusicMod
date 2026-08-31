#!/bin/bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
MOD_SOURCE="$PWD"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
GAME_APP="${CHILL_GAME_APP:-$HOME/Library/Application Support/Steam/steamapps/common/Chill with You Lo-Fi Story/Chill With You.app}"
GAME_MANAGED="$GAME_APP/Contents/Resources/Data/Managed"
[[ -f "$GAME_MANAGED/Assembly-CSharp.dll" ]] || { echo '未找到 Mono 版游戏，请设置 CHILL_GAME_APP。' >&2; exit 1; }
"$DOTNET_BIN" build "$MOD_SOURCE/src/MusicBridge.Plugin.csproj" -c Release -p:GameManagedDir="$GAME_MANAGED"
mkdir -p "$MOD_SOURCE/.downloads"
ARCHIVE="$MOD_SOURCE/.downloads/BepInEx_macos_universal_5.4.23.5.zip"
EXPECTED_SHA=01c2ae782eb016dfd6c345a18dbd2dcafffb3d9d318449d6486689f426b4a323
if [[ ! -f "$ARCHIVE" ]]; then
    curl --fail --location --silent --show-error --connect-timeout 15 --max-time 90 --retry 2 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip' -o "$ARCHIVE.part"
    mv "$ARCHIVE.part" "$ARCHIVE"
fi
ACTUAL_SHA=$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')
[[ "$ACTUAL_SHA" == "$EXPECTED_SHA" ]] || { echo 'BepInEx SHA-256 校验失败，已停止。' >&2; exit 1; }
# Never overwrite an existing runtime's user config/cache while rebuilding.
STAGE=$(mktemp -d "$MOD_SOURCE/.downloads/stage.XXXXXX")
trap 'rm -rf -- "$STAGE"' EXIT
unzip -q "$ARCHIVE" -d "$STAGE"
DEST="${MUSICBRIDGE_BUILD_DEST:-$MOD_SOURCE/dist/ChillMusicMac}"
mkdir -p "$DEST/BepInEx/core" "$DEST/BepInEx/plugins/ChillWithYouMusicBridge"
"$MOD_SOURCE/build-native-runtime.sh"
cp "$MOD_SOURCE"/.downloads/native-core/* "$DEST/BepInEx/core/"
cp "$STAGE/libdoorstop.dylib" "$DEST/"
cp "$MOD_SOURCE/../Chill with You Lo-Fi Story/BepInEx/LICENSE" "$DEST/BepInEx/LICENSE"
cp "$MOD_SOURCE/.downloads/bepinex-native-src/LICENSE" "$DEST/BepInEx/LICENSE.native-MIT"
cp "$MOD_SOURCE/.downloads/bepinex-native-src/submodules/BepInEx.Harmony/LICENSE" "$DEST/BepInEx/LICENSE.harmony-MIT"
cp "$MOD_SOURCE/../LICENSE" "$DEST/LICENSE"
cp "$MOD_SOURCE/src/bin/Release/netstandard2.1/MusicBridge.Plugin.dll" "$DEST/BepInEx/plugins/ChillWithYouMusicBridge/"
cp "$MOD_SOURCE/scripts/music.js" "$DEST/BepInEx/plugins/ChillWithYouMusicBridge/"
cp "$MOD_SOURCE/Start-Music-Mod.command" "$MOD_SOURCE/launch-core.sh" "$DEST/"
cp "$MOD_SOURCE/README.md" "$DEST/使用说明.md"
cp "$MOD_SOURCE/NATIVE-RUNTIME.md" "$MOD_SOURCE/TEST-REPORT.md" "$MOD_SOURCE/THIRD-PARTY-NOTICES.md" "$DEST/"
chmod +x "$DEST/Start-Music-Mod.command" "$DEST/launch-core.sh"
echo "构建完成：$DEST"
