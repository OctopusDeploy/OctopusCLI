#!/bin/bash
# Test that .deb and .rpm packages in the working directory install an octo command that can list-environments.

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
which docker >/dev/null || {
  echo 'This script requires docker.' >&2
  exit 1
}

SCRIPT_DIR="$(dirname "$(realpath "${BASH_SOURCE[0]}")")"

for DOCKER_IMAGE in $(cat "$SCRIPT_DIR/../linux-package-feeds/test-env-docker-images.conf" | grep -o '^[^#]*')
do
  echo "== Testing in '$DOCKER_IMAGE' =="
  docker pull "$DOCKER_IMAGE" >/dev/null || exit
  docker run --rm \
    --hostname "testpkgs$RANDOM" \
    --volume "$(pwd):/working" --volume "$SCRIPT_DIR/test-octo-package.sh:/test-octo-package.sh" \
    --volume "$SCRIPT_DIR/../linux-package-feeds:/linux-package-feeds"
    --env OCTOPUS_CLI_SERVER --env OCTOPUS_CLI_API_KEY --env OCTOPUS_SPACE --env OCTOPUS_EXPECT_ENV \
    --env REDHAT_SUBSCRIPTION_USERNAME --env REDHAT_SUBSCRIPTION_PASSWORD \
    "$DOCKER_IMAGE" bash -c 'cd /working && bash /test-octo-package.sh' || exit
done
