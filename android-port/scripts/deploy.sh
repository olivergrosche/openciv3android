#!/usr/bin/env bash
# Installs android-port/out/openciv3android.apk on a device. Installing ends a running game: save first.
#   android-port/scripts/deploy.sh tablet|pixel|<adb serial>
# The APK carries Assets, Lua and Text and unpacks them into Documents/OpenCiv3 at its first start; Civ3, C7.ini and
# saved games are never touched.
set -euo pipefail
source "$(dirname "$0")/env.sh"

case "${1:-}" in
	tablet) DEV=$TABLET ;;
	pixel) DEV=$PIXEL ;;
	"") echo "usage: deploy.sh tablet|pixel|<adb serial>" >&2; exit 1 ;;
	*) DEV=$1 ;;
esac
PKG=io.github.olivergrosche.openciv3android
adb() { "$ADB" -s "$DEV" "$@"; }

adb shell am force-stop $PKG || true

# Some devices check apps installed over adb and block or delay them; switch that off for the install only.
verify=$(adb shell settings get global verifier_verify_adb_installs | tr -d '\r')
adb shell settings put global verifier_verify_adb_installs 0
adb install -r "$(cygpath -w "$OUT/openciv3android.apk")"
if [ "$verify" = "null" ]; then
	adb shell settings delete global verifier_verify_adb_installs
else
	adb shell settings put global verifier_verify_adb_installs "$verify"
fi

# The game reads its data from shared storage.
adb shell appops set $PKG MANAGE_EXTERNAL_STORAGE allow
adb shell am start -n $PKG/com.godot.game.GodotApp > /dev/null
echo "Installed on $DEV"

# Until October 2026 the app was io.github.c7game.openciv3; it shares the data folder but is a separate app.
if adb shell pm list packages io.github.c7game.openciv3 | grep -q .; then
	echo "The old app io.github.c7game.openciv3 is still installed: adb -s $DEV uninstall io.github.c7game.openciv3"
fi
