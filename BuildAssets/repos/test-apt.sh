#!/bin/bash

if [[ -z "$PUBLISH_LINUX_EXTERNAL" || -z "$DEB_DIST" || -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  PUBLISH_LINUX_EXTERNAL, DEB_DIST, OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV'
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  ORIGIN="https://apt.octopus.com"
else
  ORIGIN="http://prerelease.apt.octopus.com"
fi

# Configure apt
export DEBIAN_FRONTEND=noninteractive
apt-get update --quiet 2 || exit
apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings
apt-get install --no-install-recommends --yes gnupg curl software-properties-common >/dev/null || exit
apt-key adv --fetch-keys "$ORIGIN/public.key" 2>&1 || exit
add-apt-repository "deb $ORIGIN/ $DEB_DIST main" || exit
apt-get update --quiet 2 || exit

# Install
apt-get install --no-install-recommends --yes tentacle octopuscli >/dev/null || exit

# Test
echo "== Testing Tentacle =="
/opt/octopus/tentacle/Tentacle version || exit
echo
echo "== Testing Octopus CLI =="
octo version || exit
apt-get --no-install-recommends --yes install ca-certificates >/dev/null || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
