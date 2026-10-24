#!/usr/bin/env bash
# Checks out the submodules C7/Assets (OpenCiv3 art and sound) and C7/addons/markdownlabel (used by the credits).
# .gitmodules records ssh URLs; this uses https so that no GitHub key is needed.
#   android-port/scripts/init-submodules.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
git -c submodule.C7/Assets.url=https://github.com/C7-Game/Assets.git \
	-c submodule.C7/addons/markdownlabel.url=https://github.com/daenvil/MarkdownLabel.git \
	submodule update --init C7/Assets C7/addons/markdownlabel
