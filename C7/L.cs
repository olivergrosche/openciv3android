using C7GameData;
using Godot;

/****
	Translation helpers for text that is built in code. Static text assigned to a Control (Label, Button, tooltip) is
	translated by Godot itself; the tables are in res://Translations (ui.csv for the interface, names.csv for units,
	techs, buildings, civilizations etc., generated from the German Civ3 rules by tools/Civ3Names).
****/
public static class L {
	// The translation of a single text or name, or the text itself if there is none.
	public static string T(string text) => string.IsNullOrEmpty(text) ? text : TranslationServer.Translate(text).ToString();

	// Translates the format string first, then fills in the arguments: L.F("Treasury: {0}", gold).
	public static string F(string format, params object[] args) => string.Format(T(format), args);

	// A date from TimeOptions.GetDisplayTime ("4000 BC", "Jan, 1940 AD") with the era label translated.
	public static string Time(string text) {
		int space = text?.LastIndexOf(' ') ?? -1;
		return space < 0 ? text : text[..space] + " " + T(text[(space + 1)..]);
	}

	// MapUnit.Describe() with translated names: "Veteran (4/4) Spearman (1.2.1/1)".
	public static string Describe(MapUnit unit) {
		UnitPrototype type = unit.unitType;
		string hitPoints = type.attack > 0 || type.defense > 0 ? $" ({unit.hitPointsRemaining}/{unit.maxHitPoints})" : "";
		string attack = type.bombard > 0 ? $"{type.attack}({type.bombard})" : type.attack.ToString();
		return $"{T(unit.experienceLevel.displayName)}{hitPoints} {T(type.name)} ({attack}.{type.defense}.{unit.movementPoints.getMixedNumber()}/{type.movement})";
	}
}
