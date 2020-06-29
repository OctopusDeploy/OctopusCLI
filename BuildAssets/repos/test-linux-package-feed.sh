#!/bin/bash
# Test that octopuscli and tentacle can be installed from our APT and RPM feeds, and octo can list-environments.

if [[ -z "$PUBLISH_LINUX_EXTERNAL" ]]; then
  echo 'This script requires the environment variable PUBLISH_LINUX_EXTERNAL - specify "true" to test the external public feed.' >&2
  exit 1
fi
OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\nthe test system to install packages.' >&2
  exit 1
fi
if [[ -z "$OCTOPUS_CLI_SERVER" || -z "$OCTOPUS_CLI_API_KEY" || -z "$OCTOPUS_SPACE" || -z "$OCTOPUS_EXPECT_ENV" ]]; then
  echo -e 'This script requires the environment variables OCTOPUS_CLI_SERVER, OCTOPUS_CLI_API_KEY, OCTOPUS_SPACE, and'\
    '\nOCTOPUS_EXPECT_ENV - specifying an Octopus server for testing "list-environments", an API key to access it, the'\
    '\nSpace to search, and an environment name expected to be found there.' >&2
  exit 1
fi

if [[ ! -e /opt/linux-package-feeds ]]; then
  echo "This script requires tools in '/opt/linux-package-feeds'. If running inside a container, check the volume mounts." >&2
  exit 1
fi


export PKG_NAMES="octopuscli tentacle"
# TODO: (ZZDY) Remove this workaround once tentacle is added to focal repo
if grep --quiet focal /etc/apt/sources.list 2>/dev/null; then
  PKG_NAMES="octopuscli"
fi

bash /opt/linux-package-feeds/install-linux-feed-package.sh || exit

if command -v dpkg > /dev/null; then
  echo Detected dpkg. Installing ca-certificates to support octo HTTPS communication.
  export DEBIAN_FRONTEND=noninteractive
  apt-get --no-install-recommends --yes install ca-certificates >/dev/null || exit
fi

echo Testing octo.
octo version || exit
OCTO_RESULT="$(octo list-environments --space="$OCTOPUS_SPACE")" || { echo "$OCTO_RESULT"; exit 1; }
echo "$OCTO_RESULT" | grep "$OCTOPUS_EXPECT_ENV" || { echo "Expected environment not found: $OCTOPUS_EXPECT_ENV." >&2; exit 1; }

# TODO: (ZZDY) Remove this workaround once tentacle is added to focal repo
if [[ ! "$PKG_NAMES" == *tentacle ]]; then exit 0; fi

echo Softly smoke-testing tentacle.
/opt/octopus/tentacle/Tentacle version
echo
