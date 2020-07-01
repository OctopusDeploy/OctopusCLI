#!/bin/bash
# Test that the octopuscli*.deb or octopuscli*.rpm package in the working directory installs an octo command that can list-environments.

if [[ -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the environment variables OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, and'\
    '\nOCTOPUS_EXPECT_ENV - specifying an Octopus server for testing "list-environments", an API key to access it, the'\
    '\nSpace to search, and an environment name expected to be found there.' >&2
  exit 1
fi
OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\nthe test system to install packages.' >&2
  exit 1
fi

if [[ ! -e /opt/linux-package-feeds ]]; then
  echo "This script requires 'linux-package-feeds' scripts, installed in '/opt/linux-package-feeds'." >&2
  echo "They come from https://github.com/OctopusDeploy/linux-package-feeds, distributed in TeamCity" >&2
  echo "  via 'Infrastructure / Linux Package Feeds'. If running inside a Docker container, supply them using a volume mount." >&2
  exit 1
fi


# Install the package (with any needed docker config, system registration, dependencies) using a script from 'linux-package-feeds'.
export PKG_PATH_PREFIX="octopuscli"
bash /opt/linux-package-feeds/install-linux-package.sh || exit

if command -v dpkg > /dev/null; then
  echo Detected dpkg. Installing ca-certificates to support octo HTTPS communication.
  export DEBIAN_FRONTEND=noninteractive
  apt-get --no-install-recommends --yes install ca-certificates >/dev/null || exit
fi

echo Testing octo.
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }
