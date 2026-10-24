#!/usr/bin/env bash
# Builds android-port/out/openciv3android.apk from the working tree.
#   android-port/scripts/build-apk.sh
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$REPO/C7"

if [ ! -f "$TEMPLATE" ]; then
	echo "Missing $TEMPLATE: build it with android-port/godot/build-template.sh (see android-port/README.md)." >&2
	exit 1
fi
if [ -z "${GODOT_ANDROID_KEYSTORE_RELEASE_PATH:-}" ] || [ -z "${GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD:-}" ]; then
	echo "No signing key: set GODOT_ANDROID_KEYSTORE_RELEASE_* in android-port/scripts/env.local.sh." >&2
	exit 1
fi
if [ ! -f Assets/README.md ] || [ ! -f addons/markdownlabel/README.md ]; then
	echo "Submodules missing: run android-port/scripts/init-submodules.sh" >&2
	exit 1
fi

# export_presets.cfg refers to the template by this path relative to C7, so the preset holds no local paths.
cp -u "$TEMPLATE" "$OUT/android-template.apk"

# The game reads Assets from the device; keep the editor from importing 100 MB it does not need.
touch Assets/.gdignore

# The editor's C# assemblies must exist before the export, or the exported scenes lose the connections of C#
# [Signal]s (no unit buttons, dead end-turn button). The stale export cache would keep that broken state.
dotnet build C7.csproj -c Debug -nologo -v q
"$GODOT" --headless --path . --import > "$OUT/import.log" 2>&1 || true
rm -rf .godot/exported
"$GODOT" --headless --path . --export-release "Android" "$(cygpath -w "$OUT/openciv3android.apk")" > "$OUT/export.log" 2>&1

size=$(stat -c %s "$OUT/openciv3android.apk")
if [ "$size" -lt 50000000 ]; then
	echo "APK is only $size bytes: the C# build is missing (see $OUT/export.log)." >&2
	exit 1
fi
echo "Built $OUT/openciv3android.apk ($size bytes)"
