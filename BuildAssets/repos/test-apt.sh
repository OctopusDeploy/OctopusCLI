#!/bin/bash

if [[ -z "$PUBLISH_LINUX_EXTERNAL" || -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" \
   || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  PUBLISH_LINUX_EXTERNAL, OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV' >&2
  exit 1
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  ORIGIN="https://apt.octopus.com"
else
  ORIGIN="http://prerelease.apt.octopus.com"
fi

# Configure apt
echo "## Configuring apt"
export DEBIAN_FRONTEND=noninteractive
apt-get update --quiet 2 || exit
apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings

DIST=$({ grep --perl --no-filename --only-matching --no-messages \
  '^deb\s+(\[[^\]#]*?\]\s+)?[^\s#]+(debian|ubuntu)[^\s#]*\s+\K\w+' /etc/apt/sources.list /etc/apt/sources.list.d/* \
  | sort | uniq --count | sort --reverse --numeric; echo 0 stable; } | awk '{ print $2; exit; }')
apt-get install --no-install-recommends --yes gnupg curl ca-certificates apt-transport-https 2>&1 >/dev/null || exit
curl --silent --show-error --fail --location "$ORIGIN/public.key" | APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=1 apt-key add - 2>&1 || exit
echo "deb $ORIGIN/ $DIST main" > /etc/apt/sources.list.d/octopus.com.list || exit
apt-get update --quiet 2 || exit

# Install
apt-get install --no-install-recommends --yes tentacle octopuscli >/dev/null || exit

# Test
echo "## Testing Tentacle"
/opt/octopus/tentacle/Tentacle version || exit
echo
echo "## Testing Octopus CLI"
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
