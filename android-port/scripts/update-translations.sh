#!/usr/bin/env bash
# Regenerates the German translation tables and lists the interface texts that have no German yet.
#   android-port/scripts/update-translations.sh
# Add the listed texts to android-port/l10n/ui_de.tsv (English<TAB>German, \n for line breaks) and run it again.
# Texts that are only placeholders in scenes (overwritten in code) can stay untranslated.
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$REPO"

# Names of units, buildings, techs, civilizations etc.: from the German patch's rules, matched to OpenCiv3's rules.
dotnet run --project android-port/tools/Civ3Names -c Release -- "$GERMAN_BIQ" C7/Lua/civ3/ruleset.json C7/Translations/names.csv \
	| head -3

# Interface texts.
perl android-port/l10n/l10n-keys.pl > "$OUT/ui_keys.txt"
perl android-port/l10n/make-ui-csv.pl android-port/l10n/ui_de.tsv C7/Translations/ui.csv "$OUT/ui_keys.txt" \
	| tee "$OUT/untranslated.txt"
