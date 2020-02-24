#!/bin/bash

# This script requires environment variables to run:
#   S3_PUBLISH_ENDPOINT, OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV

# Configure apt
export DEBIAN_FRONTEND=noninteractive
apt-get update --quiet 2 || exit
apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings
apt-get install --no-install-recommends --yes gnupg curl software-properties-common >/dev/null || exit
apt-key adv --fetch-keys "http://$S3_PUBLISH_ENDPOINT/public.key" || exit
add-apt-repository "deb http://$S3_PUBLISH_ENDPOINT/ stretch main" || exit
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
