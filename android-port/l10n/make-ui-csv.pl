# Converts ui_de.tsv (English<TAB>German, "\n" = line break) into the Godot translation table ui.csv
# (columns keys,de; fields quoted, real line breaks inside quotes) and lists interface texts without a translation.
#   perl android-port/l10n/make-ui-csv.pl android-port/l10n/ui_de.tsv C7/Translations/ui.csv [keys.txt]   (from the repo root;
#   android-port/scripts/update-translations.sh does this)
# keys.txt (optional) is the output of l10n-keys.pl; keys missing from the table are printed as "untranslated".
use strict; use warnings; use utf8;
my ($tsv, $csv, $keys) = @ARGV;
open my $in, '<:encoding(UTF-8)', $tsv or die "$tsv: $!";
my (%de, @order);
while (my $line = <$in>) {
	chomp $line; $line =~ s/\r$//;
	next if $line =~ /^#/ || $line !~ /\t/;
	my ($en, $tr) = split /\t/, $line, 2;
	s/\\n/\n/g for $en, $tr;
	die "duplicate key: $en\n" if exists $de{$en};
	$de{$en} = $tr; push @order, $en;
}
close $in;
sub csv_field { my $s = shift; $s =~ s/"/""/g; return "\"$s\""; }
open my $out, '>:encoding(UTF-8)', $csv or die "$csv: $!";
print $out "keys,de\n";
print $out csv_field($_), ',', csv_field($de{$_}), "\n" for @order;
close $out;
printf "%d texts written to %s\n", scalar @order, $csv;
if ($keys) {
	open my $k, '<:encoding(UTF-8)', $keys or die "$keys: $!";
	while (my $key = <$k>) {
		chomp $key; $key =~ s/\r$//; $key =~ s/\\n/\n/g; $key =~ s/\\"/"/g;
		next if exists $de{$key};
		(my $show = $key) =~ s/\n/\\n/g;
		print "untranslated: $show\n";
	}
}
