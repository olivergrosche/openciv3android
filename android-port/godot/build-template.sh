#!/usr/bin/env bash
# Rebuilds the patched Godot export template that android-port/scripts/build-apk.sh exports with ($TEMPLATE in
# env.sh). Needed only if that file is lost or OpenCiv3 moves to another Godot version (see README "Godot fix").
#   android-port/godot/build-template.sh
# Needs Python 3, the JDK 17 and the Android SDK; downloads the Godot source, SCons and NDK 23.2 (about 1.5 GB).
set -euo pipefail
source "$(dirname "$0")/../scripts/env.sh"
HERE="$(cd "$(dirname "$0")" && pwd)"

GODOT_VERSION=4.4.1-stable
SRC_ROOT="${GODOT_SRC_ROOT:-C:/Users/admin/dev/godot-src}"
SRC="$SRC_ROOT/godot-$GODOT_VERSION"
OFFICIAL="$APPDATA/Godot/export_templates/${GODOT_VERSION/-/.}.mono/android_release.apk"
JAVA_HOME="${JAVA_HOME:-C:/Users/admin/dev/jdk17}"
export ANDROID_HOME="$ANDROID_SDK" JAVA_HOME

mkdir -p "$SRC_ROOT"
if [ ! -d "$SRC" ]; then
	curl -sSL -o "$SRC_ROOT/godot-$GODOT_VERSION.tar.gz" "https://github.com/godotengine/godot/archive/refs/tags/$GODOT_VERSION.tar.gz"
	tar xzf "$SRC_ROOT/godot-$GODOT_VERSION.tar.gz" -C "$SRC_ROOT"
fi
if ! grep -q tpa_list_utf8 "$SRC/modules/mono/mono_gd/gd_mono.cpp"; then
	(cd "$SRC" && patch -p1 < "$HERE/godot-4.4.1-tpa-fix.patch")
fi
if [ ! -x "$SRC_ROOT/venv/Scripts/scons" ]; then
	python -m venv "$SRC_ROOT/venv"
	"$SRC_ROOT/venv/Scripts/python" -m pip install -q scons==4.8.1
fi
if [ ! -d "$ANDROID_SDK/ndk/23.2.8568313" ]; then
	yes | "$ANDROID_SDK/cmdline-tools/latest/bin/sdkmanager.bat" --sdk_root="$ANDROID_SDK" "ndk;23.2.8568313" > /dev/null
fi

# Swappy only paces frames for Vulkan; OpenCiv3 renders with the OpenGL compatibility renderer.
(cd "$SRC" && "$SRC_ROOT/venv/Scripts/scons" platform=android target=template_release arch=arm64 \
	module_mono_enabled=yes production=yes lto=none debug_symbols=no swappy=no -j"$(nproc)")

mkdir -p "$(dirname "$TEMPLATE")"
"$SRC_ROOT/venv/Scripts/python" "$HERE/make-template.py" "$OFFICIAL" \
	"$SRC/platform/android/java/lib/libs/release/arm64-v8a/libgodot_android.so" "$TEMPLATE"
echo "Template: $TEMPLATE"
