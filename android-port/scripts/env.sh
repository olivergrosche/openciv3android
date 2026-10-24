# Shared settings for the android-port scripts (sourced, not run). Run the scripts from Git Bash on Windows.
# Everything outside the repository lives in the local "toolbox" folder. Its path, the adb serials of the devices and
# the signing key are set in env.local.sh next to this file, which git ignores: copy env.local.sh.example to start one.

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ ! -f "$SCRIPTS/env.local.sh" ]; then
	echo "Missing $SCRIPTS/env.local.sh: copy env.local.sh.example and fill it in." >&2
	exit 1
fi
source "$SCRIPTS/env.local.sh"

DOTNET_DIR="$TOOLBOX/dotnet"                       # .NET SDK 8 (not on PATH)
GODOT="$TOOLBOX/godot/Godot_v4.4.1-stable_mono_win64/Godot_v4.4.1-stable_mono_win64_console.exe"
ADB="$ANDROID_SDK/platform-tools/adb.exe"
TEMPLATE="$TOOLBOX/godot-templates/android_release_4.4.1_tpafix.apk"

REPO="$(cd "$SCRIPTS/../.." && pwd)"
OUT="$REPO/android-port/out"
mkdir -p "$OUT"

# Godot's export builds the C# project itself and only finds this SDK through PATH and DOTNET_ROOT; without them it
# looks for a system .NET 9 and silently exports an APK without C# code (about 33 MB instead of 61 MB).
export PATH="$(cygpath -u "$DOTNET_DIR"):$PATH"
export DOTNET_ROOT="$(cygpath -w "$DOTNET_DIR")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export MSYS_NO_PATHCONV=1
