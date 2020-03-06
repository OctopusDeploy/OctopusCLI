#!/bin/bash

if [[ -z "$PUBLISH_LINUX_EXTERNAL" || -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" \
   || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  PUBLISH_LINUX_EXTERNAL, OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV' >&2
  exit 1
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  ORIGIN="https://rpm.octopus.com"
else
  ORIGIN="http://prerelease.rpm.octopus.com"
fi

# The following should be kept in sync with our documented download instructions (but more automated and quieter).

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
  subscription-manager register --username "$REDHAT_SUBSCRIPTION_USERNAME" \
    --password "$REDHAT_SUBSCRIPTION_PASSWORD" --auto-attach >/dev/null 2>&1 || { echo "Red Hat subscribe problem." >&2; exit 1; }
  yum --quiet --assumeyes install tentacle octopuscli 2>&1 >/dev/null
  STATUS=$?
  subscription-manager unsubscribe --all >/dev/null 2>&1 || { echo "Red Hat unsubscribe problem." >&2; exit 1; }
  if [[ $STATUS -ne 0 ]]; then
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
