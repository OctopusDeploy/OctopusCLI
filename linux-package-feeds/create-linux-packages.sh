#!/bin/bash
# Package files from BINARIES_PATH, with executable permission and a /usr/bin symlink, into .deb and .rpm packages in PACKAGES_PATH.

which fpm >/dev/null || {
  echo 'This script requires fpm and related tools, found in the container "octopusdeploy/package-linux-docker".' >&2
  exit 1
}
if [[ -z "$VERSION" ]]; then
  echo 'This script requires the environment variable VERSION - the version being packaged.' >&2
  exit 1
fi
if [[ -z "$BINARIES_PATH" ]]; then
  echo 'This script requires the environment variable BINARIES_PATH - the path containing binaries and related files to package.' >&2
  exit 1
fi
if [[ -z "$COMMAND_FILE" ]]; then
  echo 'This script requires the environment variable COMMAND_FILE - the path of a command (relative to BINARIES_PATH) to symlink in /usr/bin/.' >&2
  exit 1
fi
if [[ -z "$INSTALL_PATH" ]]; then
  echo 'This script requires the environment variable INSTALL_PATH - the path the packages will install to.' >&2
  exit 1
fi
if [[ -z "$PACKAGES_PATH" ]]; then
  echo 'This script requires the environment variable PACKAGES_PATH - the path where packages should be written.' >&2
  exit 1
fi
# Specify the environment variable FPM_OPTS to supply additional options to fpm.
# Specify the environment variable FPM_DEB_OPTS to supply additional options to fpm when building the .deb package.
# Specify the environment variable FPM_RPM_OPTS to supply additional options to fpm when building the .rpm package.


# Remove existing packages, fpm doesnt like to overwrite
rm -f *.{deb,rpm} || exit

# Remove build files
if [[ -d tmp_usr_bin ]]; then
  rm -f "tmp_usr_bin/$COMMAND_FILE" || exit
  rmdir tmp_usr_bin || exit
fi

# Create executable symlink to include in package
mkdir tmp_usr_bin && ln -s "$INSTALL_PATH/$COMMAND_FILE" tmp_usr_bin/ || exit

# Make sure the command has execute permissions
chmod a+x "$BINARIES_PATH/$COMMAND_FILE" || exit

# Create packages
fpm --version "$VERSION" \
  --name "$PACKAGE_NAME" \
  --input-type dir \
  --output-type deb \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description "$PACKAGE_DESC" \
  --deb-no-default-config-files \
  $FPM_DEB_OPTS \
  $FPM_OPTS \
  "$BINARIES_PATH=$INSTALL_PATH" \
  tmp_usr_bin/=/usr/bin/ \
  || exit

fpm --version "$VERSION" \
  --name "$PACKAGE_NAME" \
  --input-type dir \
  --output-type rpm \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description "$PACKAGE_DESC" \
  $FPM_RPM_OPTS \
  $FPM_OPTS \
  "$BINARIES_PATH=$INSTALL_PATH" \
  tmp_usr_bin/=/usr/bin/ \
  || exit

# Remove build files
if [[ -d tmp_usr_bin ]]; then
  rm -f "tmp_usr_bin/$COMMAND_FILE" || exit
  rmdir tmp_usr_bin || exit
fi

# Move to output path
mv -f *.{deb,rpm} "$PACKAGES_PATH" || exit
