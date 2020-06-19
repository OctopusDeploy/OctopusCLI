#!/bin/bash
# Test that the .deb or .rpm package with the specified prefix installs, and passes the specified test.

if [[ -z "$PKG_PATH_PREFIX" ]]; then
  echo 'This script requires the environment variable PKG_PATH_PREFIX - the prefix of the package filename to install.' >&2
  exit 1
fi
OSRELID="$(. /etc/os-release && echo $ID)"
if [[ "$OSRELID" == "rhel" && ( -z "$REDHAT_SUBSCRIPTION_USERNAME" || -z "$REDHAT_SUBSCRIPTION_PASSWORD" ) ]]; then
  echo -e 'This script requires the environment variables REDHAT_SUBSCRIPTION_USERNAME and REDHAT_SUBSCRIPTION_PASSWORD to register'\
    '\na test system, or TEST_QUICK set to any value to skip Red Hat and some other distributions.' >&2
  exit 1
fi

if command -v dpkg > /dev/null; then
  echo Detected dpkg.
  echo Configuring apt.
  export DEBIAN_FRONTEND=noninteractive
  apt-get update --quiet 2 || exit
  apt-get install --no-install-recommends --yes apt-utils >/dev/null 2>&1 || exit # silence debconf warnings in Docker

  echo Installing package.
  dpkg -i "$PKG_PATH_PREFIX"*.deb >/dev/null 2>&1 # Silenced and expected to fail due to missing deps, which apt can fix
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
  echo Installing package.
  ERR_OUT="$(
    yum --quiet --assumeyes localinstall "$PKG_PATH_PREFIX"*.rpm 2>&1
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
  echo Installing package.
  yum --quiet --assumeyes localinstall "$PKG_PATH_PREFIX"*.rpm 2>&1 || exit
fi
