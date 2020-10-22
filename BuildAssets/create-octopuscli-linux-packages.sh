#!/bin/bash
# Package octopuscli from BINARIES_PATH, with executable permission and a /usr/bin symlink, into .deb and .rpm packages in PACKAGES_PATH.

if [[ -z "$VERSION" ]]; then
  echo 'This script requires the environment variable VERSION - the version being packaged.' >&2
  exit 1
fi
if [[ -z "$BINARIES_PATH" ]]; then
  echo 'This script requires the environment variable BINARIES_PATH - the path containing binaries and related files to package.' >&2
  exit 1
fi
if [[ -z "$PACKAGES_PATH" ]]; then
  echo 'This script requires the environment variable PACKAGES_PATH - the path where packages should be written.' >&2
  exit 1
fi

which fpm >/dev/null || {
  echo 'This script requires fpm and related tools, and is intended to be run in the container "octopusdeploy/package-linux-docker".' >&2
  exit 1
}
if [[ ! -e /opt/linux-package-feeds ]]; then
  echo "This script requires 'linux-package-feeds' scripts, installed in '/opt/linux-package-feeds'." >&2
  echo "They come from https://github.com/OctopusDeploy/linux-package-feeds, distributed in TeamCity" >&2
  echo "  via 'Infrastructure / Linux Package Feeds'. If running inside a Docker container, supply them using a volume mount." >&2
  exit 1
fi


# Create .deb and .rpm packages, with executable permission and a /usr/bin symlink, using a script from 'linux-package-feeds'.
COMMAND_FILE=octo
INSTALL_PATH=/opt/octopus/octopuscli
PACKAGE_NAME=octopuscli
PACKAGE_DESC='Command line tool for Octopus Deploy'
FPM_OPTS=(
  --exclude 'opt/octopus/octopuscli/Octo' # Octo wrapper not needed here (it provides backwards compat in the .tar.gz direct download)
)
FPM_DEB_OPTS=(
  --depends 'liblttng-ust0'
  --depends 'libcurl3 | libcurl4'
  --depends 'libssl1.0.0 | libssl1.0.2 | libssl1.1'
  --depends 'libkrb5-3'
  --depends 'zlib1g'
  --depends 'libicu52 | libicu55 | libicu57 | libicu60 | libicu63 | libicu66'
)
# Note: Microsoft recommends dep 'lttng-ust' but it seems to be unavailable in CentOS 7, so we're omitting it for now.
# As it's related to tracing, hopefully it will not be required for normal usage.
FPM_RPM_OPTS=(
  --depends 'libcurl'
  --depends 'openssl-libs'
  --depends 'krb5-libs'
  --depends 'zlib'
  --depends 'libicu'
)
source /opt/linux-package-feeds/create-linux-packages.sh || exit
