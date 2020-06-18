#!/bin/bash
# Test that .deb and .rpm packages in the working directory install an octo command that can list-environments.

which docker >/dev/null || {
  echo 'This script requires docker.' >&2
  exit 1
}
if [[ -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the environment variables OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, and'\
    '\nOCTOPUS_EXPECT_ENV - specifying an Octopus server for testing "list-environments", an API key to access it, the'\
    '\nSpace to search, and an environment name expected to be found there.' >&2
  exit 1
fi
if [[ -z "$TEST_QUICK" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\na test system, or TEST_QUICK set to any value to skip Red Hat and some other distributions.' >&2
  exit 1
fi

TEST_SH='
  # Test octo
  octo version || exit
  OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
  echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
'

TEST_DEB_SH='
  # Configure apt
  export DEBIAN_FRONTEND=noninteractive
  apt-get update --quiet 2 || exit
  apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings

  # Install octo
  dpkg -i /pkgs/octopuscli*.deb >/dev/null 2>&1 # Silenced and expected to fail due to missing deps, which apt can fix
  apt-get --no-install-recommends --yes --fix-broken install 2>&1 >/dev/null || exit
  apt-get --no-install-recommends --yes install ca-certificates >/dev/null || exit
'"$TEST_SH"

TEST_RPM_SH='
if [[ $(. /etc/os-release && echo $ID) != "rhel" ]]; then
  # Install octo
  yum --quiet --assumeyes localinstall /pkgs/octopuscli*.rpm 2>&1 || exit
else
  # Install octo, but first:
  #   - Register with Red Hat to enable yum
  #   - Install yum-plugin-ovl to reduce chance of "Rpmdb checksum is invalid"
  #   - Suppress output unless there is a failure, because yum is noisy in these RHEL containers
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
fi
'"$TEST_SH"

test_in_docker() {
  echo "== Testing in '$1' =="
  docker pull "$1" >/dev/null || exit
  docker run --rm --volume "$(pwd):/pkgs" --hostname "testpkgs$RANDOM" --env OCTOPUS_CLI_SERVER --env OCTOPUS_CLI_API_KEY \
    --env OCTOPUS_SPACE --env OCTOPUS_EXPECT_ENV --env REDHAT_SUBSCRIPTION_USERNAME --env REDHAT_SUBSCRIPTION_PASSWORD \
    "$1" bash -c "$2" || exit
}

test_in_docker debian:stable-slim "$TEST_DEB_SH"
# ZZDY NOSHIP test_in_docker ubuntu:latest "$TEST_DEB_SH"
test_in_docker centos:latest "$TEST_RPM_SH"
# ZZDY NOSHIP if [ -n "$TEST_QUICK" ]; then
  echo "TEST_QUICK is enabled. Skipping the remaining distros."
  exit 0
# ZZDY NOSHIP fi
test_in_docker fedora:latest "$TEST_RPM_SH"
test_in_docker debian:oldstable-slim "$TEST_DEB_SH"
test_in_docker ubuntu:rolling "$TEST_DEB_SH"
test_in_docker linuxmintd/mint19.3-amd64 "$TEST_DEB_SH"
test_in_docker centos:7 "$TEST_RPM_SH"
test_in_docker ubuntu:xenial "$TEST_DEB_SH"
test_in_docker roboxes/rhel8 "$TEST_RPM_SH"
test_in_docker debian:oldoldstable-slim "$TEST_DEB_SH"
test_in_docker ubuntu:trusty "$TEST_DEB_SH"
test_in_docker roboxes/rhel7 "$TEST_RPM_SH"
