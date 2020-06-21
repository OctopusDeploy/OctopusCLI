#!/bin/bash
# Add Octopus package sources and install the packages listed in "$PKG_NAMES".

if [[ -z "$PUBLISH_LINUX_EXTERNAL" ]]; then
  echo 'This script requires the environment variable PUBLISH_LINUX_EXTERNAL - specify "true" to test the external public feed.' >&2
  exit 1
fi
if [[ -z "$PKG_NAMES" ]]; then
  echo 'This script requires the environment variable PKG_PATH_PREFIX - the prefix of the package filename to install.' >&2
  exit 1
fi
OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\na test system, or TEST_QUICK set to any value to skip Red Hat and some other distributions.' >&2
  exit 1
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  FEEDPREFIX="https://"
else
  FEEDPREFIX="http://prerelease."
fi

# The following commands are intended to test our instructions at: https://octopus.com/downloads/octopuscli
# Keep the process similar to how they might be applied (in a more automatic and quiet way)

if command -v dpkg > /dev/null; then
  echo Detected dpkg.
  ORIGIN="${FEEDPREFIX}apt.octopus.com"

  echo Configuring apt.
  export DEBIAN_FRONTEND=noninteractive
  apt-get update --quiet 2 || exit
  apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings in Docker

  DIST=$({ grep --perl --no-filename --only-matching --no-messages \
    '^deb\s+(\[[^\]#]*?\]\s+)?[^\s#]+(debian|ubuntu)[^\s#]*\s+\K\w+' /etc/apt/sources.list /etc/apt/sources.list.d/* \
    | sort | uniq --count | sort --reverse --numeric; echo 0 stable; } | awk '{ print $2; exit; }')
  apt-get install --no-install-recommends --yes gnupg curl ca-certificates apt-transport-https 2>&1 >/dev/null || exit
  set -o pipefail
  curl --silent --show-error --fail --location "$ORIGIN/public.key" | APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=1 apt-key add - 2>&1 || exit
  set +o pipefail
  echo "deb $ORIGIN/ $DIST main" > /etc/apt/sources.list.d/octopus.com.list || exit
  apt-get update --quiet 2 || exit

  echo Installing package.
  apt-get install --no-install-recommends --yes $PKG_NAMES >/dev/null || exit

else
  echo Configuring yum.
  ORIGIN="${FEEDPREFIX}rpm.octopus.com"

  for PKG_NAME in $PKG_NAMES; do
    curl --silent --show-error --fail --location "$ORIGIN/$PKG_NAME.repo" --output "/etc/yum.repos.d/$PKG_NAME.repo" || exit
  done

  if [[ "$OSRELID" == "rhel" ]]; then

    echo Detected RHEL.
    echo Registering with RHEL to enable yum installation of dependencies.
    # Suppressing output unless there is a failure, because yum is noisy in these RHEL containers
    SUB_OUT="$(
      subscription-manager register --username "$REDHAT_SUBSCRIPTION_USERNAME" --password "$REDHAT_SUBSCRIPTION_PASSWORD" \
        --auto-attach 2>&1
    )" || { echo "Error while registering Red Hat subscription:" >&2; echo "$SUB_OUT" >&2; exit 1; }

    TO_INSTALL="$PKG_NAMES"
    if [[ "$(source /etc/os-release && echo "${VERSION_ID:0:1}")" -lt 8 ]]; then
      echo Detected RHEL version older than 8. Adding yum-plugin-ovl to reduce chance of "Rpmdb checksum is invalid" error.
      TO_INSTALL="yum-plugin-ovl $PKG_NAMES"
    fi
  
    echo Installing packages: $PKG_NAMES.
    ERR_OUT="$(
      yum --quiet --assumeyes install $PKG_NAMES 2>&1
    )"
    STATUS=$?
  
    echo Unsubscribing RHEL system registration.
    SUB_OUT="$(
      subscription-manager unsubscribe --all 2>&1
    )" || { echo "Error while removing Red Hat subscription:" >&2; echo "$SUB_OUT" >&2; exit 1; }
  
    if [[ $STATUS -ne 0 ]]; then
      echo "Error while installing packages:" >&2
      echo "$ERR_OUT" >&2
      exit $STATUS
    fi
  else
    echo Installing packages: $PKG_NAMES.
    yum --quiet --assumeyes install $PKG_NAMES 2>&1 || exit
  fi
fi
