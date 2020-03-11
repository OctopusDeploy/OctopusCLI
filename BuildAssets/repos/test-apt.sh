#!/bin/bash

if [[ -z "$PUBLISH_LINUX_EXTERNAL" ]]; then
  echo 'This script requires the environment variable PUBLISH_LINUX_EXTERNAL - specify "true" to test the external public feed.' >&2
  exit 1
fi
if [[ -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the environment variables OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, and'\
    '\nOCTOPUS_EXPECT_ENV - specifying an Octopus server for testing "list-environments", an API key to access it, the'\
    '\nSpace to search, and an environment name expected to be found there.' >&2
  exit 1
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  ORIGIN="https://apt.octopus.com"
else
  ORIGIN="http://prerelease.apt.octopus.com"
fi

# Configure docker environment
export DEBIAN_FRONTEND=noninteractive
apt-get update --quiet 2 || exit
apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings

# The following commands are intended to test our instructions at: https://octopus.com/downloads/octopuscli
# Keep the process similar to how they might be applied (in a more automatic and quiet way)

echo "## Configuring apt"
DIST=$({ grep --perl --no-filename --only-matching --no-messages \
  '^deb\s+(\[[^\]#]*?\]\s+)?[^\s#]+(debian|ubuntu)[^\s#]*\s+\K\w+' /etc/apt/sources.list /etc/apt/sources.list.d/* \
  | sort | uniq --count | sort --reverse --numeric; echo 0 stable; } | awk '{ print $2; exit; }')
apt-get install --no-install-recommends --yes gnupg curl ca-certificates apt-transport-https 2>&1 >/dev/null || exit
set -o pipefail
curl --silent --show-error --fail --location "$ORIGIN/public.key" | APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=1 apt-key add - 2>&1 || exit
set +o pipefail
echo "deb $ORIGIN/ $DIST main" > /etc/apt/sources.list.d/octopus.com.list || exit
apt-get update --quiet 2 || exit

echo "## Installing tentacle, octopuscli"
apt-get install --no-install-recommends --yes tentacle octopuscli >/dev/null || exit

echo "## Testing Tentacle"
/opt/octopus/tentacle/Tentacle version || exit
echo
echo "## Testing Octopus CLI"
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
