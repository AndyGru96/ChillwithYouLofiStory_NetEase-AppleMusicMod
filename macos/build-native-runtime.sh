#!/bin/bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
BUILD_ROOT="$PWD/.downloads"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
mkdir -p "$BUILD_ROOT"
fetch() {
    local path="$1" url="$2"
    if [[ ! -f "$path" ]]; then
        curl -fLsS --connect-timeout 15 --max-time 120 --retry 2 "$url" -o "$path.part"
        mv "$path.part" "$path"
    fi
}
fetch "$BUILD_ROOT/bepinex-native-source.tar.gz" 'https://codeload.github.com/bbauti/BepInEx/tar.gz/105b4f06d16b23d221cde22062e48d3c0fb9a9dd'
fetch "$BUILD_ROOT/harmony-native-source.tar.gz" 'https://codeload.github.com/bbauti/BepInEx.Harmony/tar.gz/f16083f4e78f50320afad47a5d0e1c5474a582f5'
verify() { [[ "$(shasum -a 256 "$1" | awk '{print $1}')" == "$2" ]] || { echo '源码归档校验失败，停止构建。' >&2; exit 1; }; }
verify "$BUILD_ROOT/bepinex-native-source.tar.gz" 0121fd146256e7a681b937e288e13a13b5c9ece9f867df12a38e65ea97133c95
verify "$BUILD_ROOT/harmony-native-source.tar.gz" b8fd8d9d646eff844e08962ecd7c545cc0f47151bc73a714760ec8dc0ed10be0
# Pinned source commits are recorded in NATIVE-RUNTIME.md. Only build reviewed source.
mkdir -p "$BUILD_ROOT/bepinex-native-src/submodules/BepInEx.Harmony"
tar -xzf "$BUILD_ROOT/bepinex-native-source.tar.gz" --strip-components=1 -C "$BUILD_ROOT/bepinex-native-src"
tar -xzf "$BUILD_ROOT/harmony-native-source.tar.gz" --strip-components=1 -C "$BUILD_ROOT/bepinex-native-src/submodules/BepInEx.Harmony"
# Remove the fork's unrelated Valheim probes and identify this local build.
patch --batch -p1 -d "$BUILD_ROOT/bepinex-native-src" < "$PWD/patches/native-runtime.patch"
"$DOTNET_BIN" publish "$BUILD_ROOT/bepinex-native-src/BepInEx.Preloader/BepInEx.Preloader.csproj" -c Release -o "$BUILD_ROOT/native-core"
