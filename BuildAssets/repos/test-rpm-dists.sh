#!/bin/bash

which docker >/dev/null || {
  echo 'This script requires docker.' >&2
  exit 1
}
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
if [[ -z "$TEST_QUICK" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\na test system, or TEST_QUICK set to any value to skip Red Hat and some other distributions.' >&2
  exit 1
fi

test_in_docker () {
  echo "# Testing in '$1'"
  docker pull "$1" >/dev/null || exit
  docker run --rm --volume $(pwd):/pkgs --hostname "testrpm$RANDOM" \
    --env PUBLISH_LINUX_EXTERNAL --env OCTOPUS_CLI_SERVER --env OCTOPUS_CLI_API_KEY --env OCTOPUS_SPACE \
    --env OCTOPUS_EXPECT_ENV --env REDHAT_SUBSCRIPTION_USERNAME --env REDHAT_SUBSCRIPTION_PASSWORD \
    "$1" bash -c 'cd /pkgs && bash test-rpm.sh' || exit
}

test_in_docker centos:latest
if [ -n "$TEST_QUICK" ]; then
  echo "TEST_QUICK is enabled. Skipping the remaining distros."
  exit 0
fi
test_in_docker fedora:latest
test_in_docker centos:7
test_in_docker roboxes/rhel8
test_in_docker roboxes/rhel7
