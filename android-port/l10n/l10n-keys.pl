# Lists the English texts that need a German entry in Translations/ui.csv: format strings of L.F/L.T, literal
# arguments of popup/menu helpers, literal Text/TooltipText assignments, tooltips in C7Engine/Actions.cs and the
# text/tooltip/title properties of the scenes. Run from the repo root. Prints one key per line, with \n kept as
# in the source.
use strict; use warnings; use File::Find;
my %keys;
my $str = qr/"((?:[^"\x5c]|\x5c.)*)"/;
find(sub {
  return unless /\.cs$/; return if $File::Find::dir =~ m{/obj|/addons/|/Assets};
  local $/; open my $f, '<', $_ or return; my $s = <$f>; close $f;
  $s =~ s/"\s*\+\s*\$?"//g;   # "a" + "b" -> "ab", so that concatenated format strings come out whole
  $keys{$1} = 1 while $s =~ /L\.[FT]\(\s*$str/g;
  $keys{$1} = 1 while $s =~ /\b(?:AddButton|AddHeader|AddItem|SetTextAndCenterLabel|InformationalPopup|TemporaryPopup|TextDialog|ConfirmationPopup|MsgShowTemporaryPopup)\(\s*\$?$str/g;
  $keys{$1} = 1 while $s =~ /\b(?:Text|TooltipText|Title)\s*=\s*$str/g;
  $keys{$1} = 1 while $s =~ /SetText\(\s*\d+\s*,\s*$str/g;
  $keys{$1} = 1 while $s =~ /\b(?:yesText|noText|message|header|label)\s*[:=]\s*\$?$str/g;
  $keys{$1} = 1 while $s =~ /\(\s*$str\s*,\s*C7Action\.\w+/g;   # Game.AdvisorMenu
  $keys{$1} = 1 while $s =~ /\b(?:CreateAdvisorTitle|AddTitleRow)\([^;]*?$str/g;
  if ($_ eq 'Game_EmitMessages.cs') { $keys{$1} = 1 while $s =~ /^\s*\$?$str,?\s*$/mg; }   # popup header/text/button arguments
  if ($File::Find::name =~ /Actions\.cs$/) { $keys{$1} = 1 while $s =~ /\]\s*=\s*$str/g; }
}, 'C7', 'C7Engine');
find(sub {
  return unless /\.tscn$/; return if $File::Find::dir =~ m{/addons/|/Assets};
  open my $f, '<', $_ or return;
  while (<$f>) { $keys{$1} = 1 if /^(?:text|tooltip_text|title|placeholder_text) = $str/; }
  close $f;
}, 'C7');
print "$_\n" for sort grep { /[A-Za-z]{2}/ && !/\{[a-z]/ } keys %keys;
