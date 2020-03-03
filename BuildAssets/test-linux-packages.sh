#!/bin/bash

if [[ -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV' >&2
  exit 1
fi
if [[ -z "$TEST_QUICK" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD,
or TEST_QUICK to skip some distributions.' >&2
  exit 1
fi

TEST_DEB_SH='
  # Configure apt
  export DEBIAN_FRONTEND=noninteractive
  apt-get update --quiet 2 || exit
  apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings

  # Install octo
  dpkg -i /pkgs/octopuscli*.deb >/dev/null 2>&1 # Silenced and expected to fail due to missing deps, which apt can fix
  apt-get --no-install-recommends --yes --fix-broken install >/dev/null 2>&1 || exit

  # Test octo
  octo version || exit
  apt-get --no-install-recommends --yes install ca-certificates >/dev/null || exit
  OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
  echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
'

TEST_RPM_SH='
  # Install octo
  yum --quiet --assumeyes localinstall /pkgs/octopuscli*.rpm 2>&1 || exit

  # Test octo
  octo version || exit
  OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
  echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
'

TEST_RHEL_SH='
  # Install octo
  subscription-manager register --username "$REDHAT_SUBSCRIPTION_USERNAME" \
    --password "$REDHAT_SUBSCRIPTION_PASSWORD" --auto-attach >/dev/null 2>&1
  yum --quiet --assumeyes localinstall /pkgs/octopuscli*.rpm >/dev/null 2>&1
  subscription-manager unsubscribe --all >/dev/null 2>&1

  # Test octo
  octo version || exit
  OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
  echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
'

test_in_docker () {
  echo "== Testing $1 =="
  docker pull "$1" >/dev/null || exit
  docker run --rm --volume "$(pwd):/pkgs" --env OCTOPUS_CLI_SERVER --env OCTOPUS_CLI_API_KEY --env OCTOPUS_SPACE \
    --env OCTOPUS_EXPECT_ENV --env REDHAT_SUBSCRIPTION_USERNAME --env REDHAT_SUBSCRIPTION_PASSWORD "$1" bash -c "$2" || exit
}

test_in_docker debian:stable-slim "$TEST_DEB_SH"
test_in_docker ubuntu:latest "$TEST_DEB_SH"
test_in_docker centos:latest "$TEST_RPM_SH"
if [ -n "$TEST_QUICK" ]; then
  echo "TEST_QUICK is enabled. Skipping the remaining distros."
  exit 0
fi
test_in_docker fedora:latest "$TEST_RPM_SH"
test_in_docker debian:oldstable-slim "$TEST_DEB_SH"
test_in_docker ubuntu:rolling "$TEST_DEB_SH"
test_in_docker centos:7 "$TEST_RPM_SH"
test_in_docker debian:oldoldstable-slim "$TEST_DEB_SH"
test_in_docker ubuntu:xenial "$TEST_DEB_SH"
test_in_docker linuxmintd/mint19.3-amd64 "$TEST_DEB_SH"
test_in_docker roboxes/rhel8 "$TEST_RHEL_SH"
test_in_docker roboxes/rhel7 "$TEST_RHEL_SH"
