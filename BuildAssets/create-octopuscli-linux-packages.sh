#!/bin/bash
# Package files from OCTOPUSCLI_BINARIES, with executable permission and a /usr/bin symlink, into .deb and .rpm packages in OUT_PATH.

if [[ -z "$VERSION" ]]; then
  echo 'This script requires the environment variable VERSION - the version being packaged.' >&2
  exit 1
fi
if [[ -z "$OCTOPUSCLI_BINARIES" ]]; then
  echo 'This script requires the environment variable OCTOPUSCLI_BINARIES - the path containing octo and related files.' >&2
  exit 1
fi
if [[ -z "$OUT_PATH" ]]; then
  echo 'This script requires the environment variable OUT_PATH - the path where packages should be written.' >&2
  exit 1
fi

source "$(dirname "${BASH_SOURCE[0]}")/../linux-package-feeds/create-linux-packages.sh"
