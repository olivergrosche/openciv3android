# Copies Godot's official Android export template and replaces its arm64 engine library with a rebuilt one.
#   python make-template.py <official android_release.apk> <libgodot_android.so> <output .apk>
# Godot signs the APK again when it exports, so the template itself needs no signature.
import sys
import zipfile

official, library, output = sys.argv[1:4]
name = "lib/arm64-v8a/libgodot_android.so"
with zipfile.ZipFile(official) as src, zipfile.ZipFile(output, "w") as dst:
    for entry in src.infolist():
        if entry.filename == name:
            dst.write(library, name, compress_type=zipfile.ZIP_DEFLATED)
        else:
            dst.writestr(entry, src.read(entry.filename), compress_type=entry.compress_type)
print(f"{output}: {name} replaced")
