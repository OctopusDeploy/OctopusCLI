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
  ORIGIN="https://rpm.octopus.com"
else
  ORIGIN="http://prerelease.rpm.octopus.com"
fi

# The following commands are intended to test our instructions at: https://octopus.com/downloads/octopuscli
# Keep the process similar to how they might be applied (in a more automatic and quiet way)

echo "## Configuring yum"
curl --silent --show-error --fail --location "$ORIGIN/tentacle.repo" --output /etc/yum.repos.d/tentacle.repo || exit
curl --silent --show-error --fail --location "$ORIGIN/octopuscli.repo" --output /etc/yum.repos.d/octopuscli.repo || exit

echo "## Installing tentacle, octopuscli"
if [[ $(. /etc/os-release && echo $ID) != "rhel" ]]; then
  yum --quiet --assumeyes install tentacle octopuscli 2>&1 || exit
else
  if [[ -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ]]; then
    echo -e 'Unable to test in RHEL without environment variables: REDHAT_SUBSCRIPTION_USERNAME, REDHAT_SUBSCRIPTION_PASSWORD.' >&2
    exit 1
  fi
  # Install our packages, but first:
  #   - Register with Red Hat to enable yum
  #   - Install yum-plugin-ovl to reduce chance of "Rpmdb checksum is invalid"
  #   - Suppress output unless there is a failure, because yum is noisy in these RHEL containers
  SUB_OUT="$(
    subscription-manager register --username "$REDHAT_SUBSCRIPTION_USERNAME" --password "$REDHAT_SUBSCRIPTION_PASSWORD" \
      --auto-attach 2>&1
  )" || { echo "Error while registering Red Hat subscription:" >&2; echo "$SUB_OUT" >&2; exit 1; }
  ERR_OUT="$(
    if [[ "$(source /etc/os-release && echo "${VERSION_ID:0:1}")" -lt 8 ]]; then
      yum --quiet --assumeyes install yum-plugin-ovl 2>&1 || exit
    fi
    yum --quiet --assumeyes install tentacle octopuscli 2>&1
  )"
  STATUS=$?
  SUB_OUT="$(
    subscription-manager unsubscribe --all 2>&1
  )" || { echo "Error while removing Red Hat subscription:" >&2; echo "$SUB_OUT" >&2; exit 1; }
  if [[ $STATUS -ne 0 ]]; then
    echo "Error while installing packages:" >&2
    echo "$ERR_OUT" >&2
    exit $STATUS
  fi
fi

echo "## Testing Tentacle"
/opt/octopus/tentacle/Tentacle version || exit
echo
echo "## Testing Octopus CLI"
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
