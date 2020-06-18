#!/bin/bash
# Test that the .deb or .rpm package in the working directory installs an octo command that can list-environments.

if [[ -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the environment variables OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, and'\
    '\nOCTOPUS_EXPECT_ENV - specifying an Octopus server for testing "list-environments", an API key to access it, the'\
    '\nSpace to search, and an environment name expected to be found there.' >&2
  exit 1
fi

OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\na test system, or TEST_QUICK set to any value to skip Red Hat and some other distributions.' >&2
  exit 1
fi

if which dpkg > /dev/null; then
  echo Detected dpkg.
  echo Configuring apt.
  export DEBIAN_FRONTEND=noninteractive
  apt-get update --quiet 2 || exit
  apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings

  echo Installing octo.
  dpkg -i /pkgs/octopuscli*.deb >/dev/null 2>&1 # Silenced and expected to fail due to missing deps, which apt can fix
  apt-get --no-install-recommends --yes --fix-broken install 2>&1 >/dev/null || exit
  apt-get --no-install-recommends --yes install ca-certificates >/dev/null || exit
elif [[ "$OSRELID" == "rhel" ]]; then
  echo Detected RHEL.
  echo Registering with RHEL to enable yum installation of dependencies.
  # Suppressing output unless there is a failure, because yum is noisy in these RHEL containers
  SUB_OUT="$(
    subscription-manager register --username "$REDHAT_SUBSCRIPTION_USERNAME" --password "$REDHAT_SUBSCRIPTION_PASSWORD" \
      --auto-attach 2>&1
  )" || { echo "Error while registering Red Hat subscription:" >&2; echo "$SUB_OUT" >&2; exit 1; }
  ERR_OUT="$(
    yum --quiet --assumeyes localinstall /pkgs/octopuscli*.rpm 2>&1
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
else
  echo Installing octo.
  yum --quiet --assumeyes localinstall /pkgs/octopuscli*.rpm 2>&1 || exit
fi

echo Testing octo.
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
