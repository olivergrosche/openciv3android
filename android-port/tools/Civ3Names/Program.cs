using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using QueryCiv3;
using QueryCiv3.Biq;

// Builds an English -> German name table for OpenCiv3 from a German Civilization III rules file (the German patch's
// Conquests.biq) and OpenCiv3's own rules (C7/Lua/civ3/ruleset.json, English names; c7-static-map-save.json in 0.3).
//
//   dotnet run -- <German Conquests.biq> <C7/Lua/civ3/ruleset.json> <C7/Translations/names.csv>
//
// Entries are matched by civilopedia key where the JSON has one (techs, resources, governments, terraforms), by a key
// derived from the English name (units PRTO_, buildings BLDG_, terrain TERR_) and by position (civilizations with
// their leaders and city names, difficulties). Output: Godot translation CSV with the columns keys,de.
static class Program {
	static readonly SortedDictionary<string, string> table = new(StringComparer.Ordinal);
	static readonly List<string> problems = new();

	static int Main(string[] args) {
		if (args.Length != 3) {
			Console.Error.WriteLine("usage: Civ3Names <German Conquests.biq> <ruleset.json> <out.csv>");
			return 1;
		}
		BiqData biq = BiqData.LoadFile(args[0]);
		using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(args[1]));
		JsonElement root = doc.RootElement;

		var prto = ByKey(biq.Prto.Select(p => (p.CivilopediaEntry, p.Name)));
		var tech = ByKey(biq.Tech.Select(p => (p.CivilopediaEntry, p.Name)));
		var bldg = ByKey(biq.Bldg.Select(p => (p.CivilopediaEntry, p.Name)));
		var good = ByKey(biq.Good.Select(p => (p.CivilopediaEntry, p.Name)));
		var govt = ByKey(biq.Govt.Select(p => (p.CivilopediaEntry, p.Name)));
		var terr = ByKey(biq.Terr.Select(p => (p.CivilopediaEntry, p.Name)));
		var tfrm = ByKey(biq.Tfrm.Select(p => (p.CivilopediaEntry, p.Name)));

		Section(root, "techs", "name", tech, "TECH_", biq.Tech.Select(p => p.Name).ToArray());
		Section(root, "resources", "name", good, "GOOD_", biq.Good.Select(p => p.Name).ToArray());
		Section(root, "governments", "name", govt, "GOVT_", biq.Govt.Select(p => p.Name).ToArray());
		Section(root, "terraForms", "name", tfrm, "TFRM_", biq.Tfrm.Select(p => p.Name).ToArray());
		Section(root, "unitPrototypes", "name", prto, "PRTO_", biq.Prto.Select(p => p.Name).ToArray());
		Section(root, "buildings", "name", bldg, "BLDG_", biq.Bldg.Select(p => p.Name).ToArray());
		Section(root, "terrainTypes", "displayName", terr, "TERR_", biq.Terr.Select(p => p.Name).ToArray());

		JsonElement[] civs = List(root, "civilizations").ToArray();
		for (int i = 0; i < civs.Length && i < biq.Race.Length; i++) {
			JsonElement c = civs[i];
			RACE race = biq.Race[i];
			Add(Str(c, "name"), race.Name, $"civ {i}");
			Add(Str(c, "noun"), race.Noun, $"civ {i} noun");
			Add(Str(c, "adjective"), race.Adjective, $"civ {i} adjective");
			Add(Str(c, "leader"), race.LeaderName, $"civ {i} leader");
			if (c.TryGetProperty("cityNames", out JsonElement cities)) {
				RACE_City[] german = biq.RaceCityName[i];
				int j = 0;
				foreach (JsonElement city in cities.EnumerateArray()) {
					if (j < german.Length) Add(city.GetString(), german[j].Name, $"civ {i} city {j}");
					j++;
				}
				if (j != german.Length) problems.Add($"civ {i} ({Str(c, "name")}): {j} city names in JSON, {german.Length} in BIQ");
			}
		}
		if (civs.Length != biq.Race.Length) problems.Add($"{civs.Length} civilizations in JSON, {biq.Race.Length} in BIQ");

		JsonElement[] diffs = List(root, "difficulties").ToArray();
		for (int i = 0; i < diffs.Length && i < biq.Diff.Length; i++) Add(Str(diffs[i], "name"), biq.Diff[i].Name, $"difficulty {i}");

		JsonElement[] levels = List(root, "experienceLevels").ToArray();
		for (int i = 0; i < levels.Length && i < biq.Expr.Length; i++) Add(Str(levels[i], "displayName"), biq.Expr[i].Name, $"experience {i}");

		var ctzn = new Dictionary<string, CTZN>(StringComparer.OrdinalIgnoreCase);
		foreach (CTZN c in biq.Ctzn) ctzn.TryAdd(c.CivilopediaEntry, c);
		foreach (JsonElement c in List(root, "citizenTypes")) {
			string key = Str(c, "civilopediaEntry");
			if (key != null && ctzn.TryGetValue(key, out CTZN german)) {
				Add(Str(c, "singularName"), german.SingularName, key);
				Add(Str(c, "pluralName"), german.PluralName, key);
			}
		}

		var csv = new StringBuilder("keys,de\n");
		foreach (var (en, de) in table) csv.Append(Csv(en)).Append(',').Append(Csv(de)).Append('\n');
		File.WriteAllText(args[2], csv.ToString(), new UTF8Encoding(false));

		Console.WriteLine($"{table.Count} translations written to {args[2]}");
		foreach (string p in problems) Console.WriteLine("  " + p);
		return 0;
	}

	// English names whose civilopedia key cannot be derived from the name (checked against the German Conquests.biq).
	static readonly Dictionary<string, string> KeyAliases = new() {
		["Research Lab"] = "BLDG_Lab", ["The Pyramids"] = "BLDG_Pyramids", ["The Hanging Gardens"] = "BLDG_Hanging_Gardens",
		["The Colossus"] = "BLDG_Colossus", ["The Great Lighthouse"] = "BLDG_Lighthouse", ["The Great Library"] = "BLDG_Great_Library",
		["The Oracle"] = "BLDG_Oracle", ["The Great Wall"] = "BLDG_Great_Wall", ["Sun Tzu's Art of War"] = "BLDG_Art_of_War",
		["Magellan's Voyage"] = "BLDG_Circumnavigation", ["Copernicus' Observatory"] = "BLDG_Solar_System",
		["Shakespeare's Theater"] = "BLDG_Great_Playhouse", ["Leonardo's Workshop"] = "BLDG_Inventor's_Workshop",
		["JS Bach's Cathedral"] = "BLDG_Grand_Cathedral", ["Newton's University"] = "BLDG_Great_University",
		["Smith's Trading Company"] = "BLDG_Trading_Company", ["The United Nations"] = "BLDG_United_Nations",
		["The Manhattan Project"] = "BLDG_Manhattan_Project", ["Heroic Epic"] = "BLDG_Epic", ["Iron Works"] = "BLDG_Great_Ironworks",
		["The Pentagon"] = "BLDG_Pentagon", ["Apollo Program"] = "BLDG_Apollo_Project", ["Strategic Missile Defense"] = "BLDG_SDI",
		["Intelligence Agency"] = "BLDG_Intelligence_Center", ["SS Docking Bay"] = "BLDG_SS_Landing_Docking_Bay",
		["SS Storage/Supply"] = "BLDG_SS_Storage_Supply", ["The Internet"] = "BLDG_Internet",
		["The Temple of Artemis"] = "BLDG_Artemis", ["The Statue of Zeus"] = "BLDG_Zeus", ["The Mausoleum of Mausollos"] = "BLDG_Mausoleum",
		["Modern Paratrooper"] = "PRTO_Paratrooper", ["Tactical Nuke"] = "PRTO_NUKE", ["Numidian Mercenary"] = "PRTO_Libyan_Mercenary",
		["Hwach'a"] = "PRTO_Hwacha", ["Joan d'Arc"] = "PRTO_Joan", ["Wang Kon"] = "PRTO_Wang",
		["Three-Man Chariot"] = "PRTO_Three_Man_Chariot", ["Chasqui Scout"] = "PRTO_Chasquis_Scout",
	};

	static Dictionary<string, string> ByKey(IEnumerable<(string key, string name)> entries) {
		var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, name) in entries) d.TryAdd(key, name);
		return d;
	}

	static IEnumerable<JsonElement> List(JsonElement root, string name) =>
		root.TryGetProperty(name, out JsonElement a) ? a.EnumerateArray() : Enumerable.Empty<JsonElement>();

	static string Str(JsonElement e, string name) =>
		e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

	// Uses each entry's civilopedia key, or one derived from the English name ("Bronze Working" -> TECH_Bronze_Working).
	// If that fails and the JSON list is as long as the BIQ section, the BIQ entry at the same position is used and the
	// pair is printed for checking.
	static void Section(JsonElement root, string list, string nameField, Dictionary<string, string> german, string prefix, string[] germanByIndex) {
		JsonElement[] entries = List(root, list).ToArray();
		bool sameLength = entries.Length == germanByIndex.Length;
		if (!sameLength) problems.Add($"{list}: {entries.Length} in JSON, {germanByIndex.Length} in BIQ");
		for (int i = 0; i < entries.Length; i++) {
			string english = Str(entries[i], nameField);
			if (english == null) continue;
			string key = Str(entries[i], "civilopediaEntry")
				?? (KeyAliases.TryGetValue(english, out string alias) ? alias : prefix + english.Replace(' ', '_'));
			if (german.TryGetValue(key, out string de)) {
				Add(english, de, key);
			} else if (sameLength) {
				Console.WriteLine($"  by position {list}[{i}]: {english} -> {germanByIndex[i]}");
				Add(english, germanByIndex[i], $"{list}[{i}]");
			} else {
				problems.Add($"no German entry for {key} ({english})");
			}
		}
	}

	static void Add(string english, string german, string where) {
		if (string.IsNullOrEmpty(english) || string.IsNullOrEmpty(german) || english == german) return;
		if (table.TryGetValue(english, out string existing) && existing != german) {
			problems.Add($"conflict for \"{english}\": \"{existing}\" vs \"{german}\" ({where}), keeping the first");
			return;
		}
		table[english] = german;
	}

	static string Csv(string s) => s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
