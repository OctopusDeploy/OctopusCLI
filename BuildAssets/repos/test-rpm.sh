#!/bin/bash

if [[ -z "$PUBLISH_LINUX_EXTERNAL" || -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  PUBLISH_LINUX_EXTERNAL, OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV'
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  ORIGIN="https://rpm.octopus.com/"
else
  ORIGIN="http://prerelease.rpm.octopus.com/"
fi

# Configure yum
curl --silent "$ORIGIN/tentacle.repo" --output /etc/yum.repos.d/tentacle.repo || exit
curl --silent "$ORIGIN/octopuscli.repo" --output /etc/yum.repos.d/octopuscli.repo || exit

# Install
yum --quiet --assumeyes install tentacle octopuscli 2>&1 || exit

# Test
echo "== Testing Tentacle =="
/opt/octopus/tentacle/Tentacle version || exit
echo
echo "== Testing Octopus CLI =="
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
