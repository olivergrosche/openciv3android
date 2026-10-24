#!/usr/bin/env bash
# Installs android-port/out/OpenCiv3.apk on a device. Installing ends a running game: save first.
#   android-port/scripts/deploy.sh tablet|pixel|<adb serial> [--data]
# --data also replaces Assets, Lua and Text in Documents/OpenCiv3 (needed after upstream updates; the old folders are
# moved to Documents/OpenCiv3-previous). Civ3 itself (Documents/Civ3) and C7.ini are never touched.
set -euo pipefail
source "$(dirname "$0")/env.sh"

case "${1:-}" in
	tablet) DEV=$TABLET ;;
	pixel) DEV=$PIXEL ;;
	"") echo "usage: deploy.sh tablet|pixel|<adb serial> [--data]" >&2; exit 1 ;;
	*) DEV=$1 ;;
esac
PKG=io.github.c7game.openciv3
adb() { "$ADB" -s "$DEV" "$@"; }

adb shell am force-stop $PKG || true

if [ "${2:-}" = "--data" ]; then
	adb shell 'rm -rf /sdcard/Documents/OpenCiv3-new /sdcard/Documents/OpenCiv3-previous; mkdir -p /sdcard/Documents/OpenCiv3-new /sdcard/Documents/OpenCiv3-previous'
	for d in Assets Lua Text; do
		adb push "$(cygpath -w "$REPO/C7/$d")" /sdcard/Documents/OpenCiv3-new/ | tail -1
	done
	adb shell 'cd /sdcard/Documents && rm -f OpenCiv3-new/Assets/.git OpenCiv3-new/Assets/.gdignore && mkdir -p OpenCiv3 &&
		for d in Assets Lua Text; do [ -e OpenCiv3/$d ] && mv OpenCiv3/$d OpenCiv3-previous/; mv OpenCiv3-new/$d OpenCiv3/; done && rmdir OpenCiv3-new'
fi

# Some devices check apps installed over adb and block or delay them; switch that off for the install only.
verify=$(adb shell settings get global verifier_verify_adb_installs | tr -d '\r')
adb shell settings put global verifier_verify_adb_installs 0
adb install -r "$(cygpath -w "$OUT/OpenCiv3.apk")"
if [ "$verify" = "null" ]; then
	adb shell settings delete global verifier_verify_adb_installs
else
	adb shell settings put global verifier_verify_adb_installs "$verify"
fi

# The game reads its data from shared storage.
adb shell appops set $PKG MANAGE_EXTERNAL_STORAGE allow
adb shell am start -n $PKG/com.godot.game.GodotApp > /dev/null
echo "Installed on $DEV"
