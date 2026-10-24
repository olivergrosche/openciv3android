#!/usr/bin/env bash
# Regenerates the German translation tables and lists the names and interface texts that have no German yet.
#   android-port/scripts/update-translations.sh
# Add listed names to android-port/l10n/names_de.tsv and listed texts to android-port/l10n/ui_de.tsv
# (English<TAB>German, \n for line breaks), then run it again. Texts that are only placeholders in scenes (overwritten in
# code) can stay untranslated.
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$REPO"

# Names of units, buildings, techs, civilizations, cities etc. from OpenCiv3's rules and from the scenarios exported
# to android-port/out/scenarios (see android-port/README.md, "Giant Earth").
shopt -s nullglob
perl android-port/l10n/name-keys.pl C7/Lua/civ3/ruleset.json "$OUT"/scenarios/*.json > "$OUT/name_keys.txt"
perl android-port/l10n/make-ui-csv.pl android-port/l10n/names_de.tsv C7/Translations/names.csv "$OUT/name_keys.txt" \
	| tee "$OUT/untranslated-names.txt"

# Interface texts.
perl android-port/l10n/l10n-keys.pl > "$OUT/ui_keys.txt"
perl android-port/l10n/make-ui-csv.pl android-port/l10n/ui_de.tsv C7/Translations/ui.csv "$OUT/ui_keys.txt" \
	| tee "$OUT/untranslated.txt"
