// Prints the buildings of a BIQ with their required tech, and the civs' free techs: dotnet run -- <file.biq> [name filter]
using System;
using System.Linq;
using QueryCiv3;
using QueryCiv3.Biq;

class Program {
	static string TechName(BiqData biq, int i) => i >= 0 && i < biq.Tech.Length ? biq.Tech[i].Name : i.ToString();

	static void Main(string[] args) {
		BiqData biq = BiqData.LoadFile(args[0]);
		string filter = args.Length > 1 ? args[1] : "";
		Console.WriteLine($"BLDG {biq.Bldg?.Length}, TECH {biq.Tech?.Length}, ERAS {biq.Eras?.Length}, GOVT {biq.Govt?.Length}");
		for (int i = 0; i < biq.Bldg.Length; i++) {
			BLDG b = biq.Bldg[i];
			if (!b.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
			Console.WriteLine($"{i,3} {b.Name,-28} tech={TechName(biq, b.RequiredAdvance)} gov={b.RequiredGovernment} cost={b.Cost} key={b.CivilopediaEntry}");
		}
		for (int i = 0; i < biq.Tech.Length; i++) {
			TECH t = biq.Tech[i];
			if (t.Name.Contains("Trash", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("Farming") || t.Name.Contains("Crops") || t.Era < 0)
				Console.WriteLine($"TECH {i} {t.Name} era={t.Era} cost={t.Cost} prereq={TechName(biq, t.Prerequisite1)},{TechName(biq, t.Prerequisite2)},{TechName(biq, t.Prerequisite3)},{TechName(biq, t.Prerequisite4)} key={t.CivilopediaEntry}");
		}
		for (int i = 0; i < biq.Race.Length; i++) {
			RACE r = biq.Race[i];
			Console.WriteLine($"RACE {i} {r.Name}: free {TechName(biq, r.FreeTech1)}, {TechName(biq, r.FreeTech2)}, {TechName(biq, r.FreeTech3)}, {TechName(biq, r.FreeTech4)}");
		}
	}
}
