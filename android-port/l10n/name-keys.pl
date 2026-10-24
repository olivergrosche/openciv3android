# Lists the English names in OpenCiv3's rules, and in scenarios saved as OpenCiv3 JSON, that need a German entry in
# names_de.tsv: techs, resources, governments, terraforms, units, buildings, terrain, civilizations (name, noun,
# adjective, leader, cities), difficulties, experience levels and citizens. Prints one name per line, sorted.
#   perl android-port/l10n/name-keys.pl C7/Lua/civ3/ruleset.json [scenario.json ...]
use strict; use warnings; use JSON::PP;
die "usage: name-keys.pl <ruleset.json> [scenario.json ...]\n" unless @ARGV;
my %names;
my %fields = (
	techs => ['name'], resources => ['name'], governments => ['name'], terraForms => ['name'], unitPrototypes => ['name'],
	buildings => ['name'], terrainTypes => ['displayName'], difficulties => ['name'], experienceLevels => ['displayName'],
	citizenTypes => ['singularName', 'pluralName'], civilizations => ['name', 'noun', 'adjective', 'leader'],
);
for my $file (@ARGV) {
	open my $f, '<:raw', $file or die "$file: $!";
	my $rules = decode_json(do { local $/; <$f> });
	close $f;
	for my $list (keys %fields) {
		for my $entry (@{ $rules->{$list} || [] }) {
			for my $field (@{ $fields{$list} }) {
				$names{$entry->{$field}} = 1 if defined $entry->{$field} && !ref $entry->{$field};
			}
			$names{$_} = 1 for @{ $entry->{cityNames} || [] };
		}
	}
}
binmode STDOUT, ':encoding(UTF-8)';
print "$_\n" for sort grep { /[A-Za-z]/ } keys %names;
