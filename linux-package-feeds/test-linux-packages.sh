#!/bin/bash
# Tests packages in the working directory by running $TEST_SCRIPT_FILE in a variety of Linux OS docker containers.

if [[ -z "$TEST_SCRIPT_FILE" ]]; then
  echo 'This script requires the environment variable TEST_SCRIPT_FILE - the path of a script file that installs and tests the package in the working directory.' >&2
  exit 1
fi
# Set the array DOCKER_OPTS to supply additional options to docker.

which docker >/dev/null || {
  echo 'This script requires docker.' >&2
  exit 1
}

test_in_docker() {
  echo "== Testing in '$1' =="
  docker pull "$1" >/dev/null || exit
  docker run --rm \
    --hostname "testpkgs$RANDOM" --volume "$(pwd):/working" --volume "$TEST_SCRIPT_FILE:/test.sh" \
    "${DOCKER_OPTS[@]}" \
    "$1" bash -c 'cd /working && bash /test.sh' || exit
}

test_in_docker debian:stable-slim
# ZZDY NOSHIP test_in_docker ubuntu:latest
test_in_docker centos:latest
# ZZDY NOSHIP if [ -n "$TEST_QUICK" ]; then
  echo "TEST_QUICK is enabled. Skipping the remaining distros."
  exit 0
# ZZDY NOSHIP fi
test_in_docker fedora:latest
test_in_docker debian:oldstable-slim
test_in_docker ubuntu:rolling
test_in_docker linuxmintd/mint19.3-amd64
test_in_docker centos:7
test_in_docker ubuntu:xenial
test_in_docker roboxes/rhel8
test_in_docker debian:oldoldstable-slim
test_in_docker ubuntu:trusty
test_in_docker roboxes/rhel7
