#!/bin/bash

# This script requires environment variables to run:
#   S3_PUBLISH_ENDPOINT, OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV

# Configure yum
curl -s "http://$S3_PUBLISH_ENDPOINT/tentacle.repo" -o /etc/yum.repos.d/tentacle.repo || exit
curl -s "http://$S3_PUBLISH_ENDPOINT/octopuscli.repo" -o /etc/yum.repos.d/octopuscli.repo || exit

# Install
yum --quiet --assumeyes install tentacle 2>&1 || exit
yum --quiet --assumeyes install octopuscli 2>&1 || exit

# Test
echo "== Testing Tentacle =="
/opt/octopus/tentacle/Tentacle version || exit
echo
echo "== Testing Octopus CLI =="
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
