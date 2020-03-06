#!/bin/bash

if [[ -z "$PUBLISH_LINUX_EXTERNAL" || -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" \
   || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  PUBLISH_LINUX_EXTERNAL, OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, OCTOPUS_EXPECT_ENV' >&2
  exit 1
fi

test_in_docker () {
  echo "# Testing in '$1'"
  docker pull "$1" >/dev/null || exit
  docker run --rm --volume $(pwd):/pkgs \
    --env PUBLISH_LINUX_EXTERNAL --env OCTOPUS_CLI_SERVER --env OCTOPUS_CLI_API_KEY --env OCTOPUS_SPACE --env OCTOPUS_EXPECT_ENV \
    "$1" bash -c 'cd /pkgs && bash test-apt.sh' || exit
}

test_in_docker debian:stable-slim
test_in_docker ubuntu:latest
if [ -n "$TEST_QUICK" ]; then
  echo "TEST_QUICK is enabled. Skipping the remaining distros."
  exit 0
fi
test_in_docker debian:oldstable-slim
test_in_docker ubuntu:rolling
test_in_docker linuxmintd/mint19.3-amd64
test_in_docker ubuntu:xenial
test_in_docker debian:oldoldstable-slim
test_in_docker ubuntu:trusty
