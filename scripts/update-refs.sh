#!/usr/bin/env bash
# Regenerates lib/refs: reference-only copies (public API signatures, no method bodies) of the
# game and BepInEx assemblies, so CI can build without the game installed.
# Needs the refasmer tool: dotnet tool install -g JetBrains.Refasmer.CliTool
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
game="${GAME_PATH:-/var/mnt/data/SteamLibrary/steamapps/common/Stonewards}/Stonewards_Data/Managed"
bepinex="${PROFILE_PATH:-$HOME/.config/r2modmanPlus-local/Stonewards/profiles/Default}/BepInEx/core"
out="$root/lib/refs"

# Keep in sync with the <Reference> items in src/ModSettings/ModSettings.csproj.
game_dlls=(Assembly-CSharp UnityEngine UnityEngine.CoreModule UnityEngine.InputLegacyModule
    UnityEngine.TextRenderingModule UnityEngine.UIElementsModule)
bepinex_dlls=(BepInEx 0Harmony)

rm -rf "$out"
mkdir -p "$out"
for name in "${game_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$game/$name.dll"; done
for name in "${bepinex_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$bepinex/$name.dll"; done
ls -l "$out"
