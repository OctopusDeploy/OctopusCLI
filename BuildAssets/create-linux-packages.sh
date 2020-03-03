#!/bin/bash

if [[ -z "$VERSION" || -z "$OCTOPUSCLI_BINARIES" || -z "$ARTIFACTS" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  VERSION, OCTOPUSCLI_BINARIES, ARTIFACTS' >&2
  exit 1
fi

# Remove existing packages, fpm doesnt like to overwrite
rm -f *.{deb,rpm} || exit

# Remove build files
rm -f tmp_usr_bin/octo || exit
if [[ -d tmp_usr_bin ]]; then
  rmdir tmp_usr_bin || exit
fi

# Create executable symlink to include in package
mkdir tmp_usr_bin && ln -s /opt/octopus/octopuscli/octo tmp_usr_bin/octo || exit

# Set permissions
chmod 755 "$OCTOPUSCLI_BINARIES/octo" || exit

# Exclude Octo legacy wrapper from distribution
rm -f "$OCTOPUSCLI_BINARIES/Octo" || exit

# Create packages
fpm --version "$VERSION" \
  --name octopuscli \
  --input-type dir \
  --output-type deb \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Command line tool for Octopus Deploy' \
  --deb-no-default-config-files \
  --depends 'liblttng-ust0' \
  --depends 'libcurl3 | libcurl4' \
  --depends 'libssl1.0.0 | libssl1.0.2 | libssl1.1' \
  --depends 'libkrb5-3' \
  --depends 'zlib1g' \
  --depends 'libicu52 | libicu55 | libicu57 | libicu60 | libicu63' \
  "$OCTOPUSCLI_BINARIES=/opt/octopus/octopuscli" \
  tmp_usr_bin/=/usr/bin/ \
  || exit

fpm --version "$VERSION" \
  --name octopuscli \
  --input-type dir \
  --output-type rpm \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Command line tool for Octopus Deploy' \
  --depends 'libcurl' \
  --depends 'openssl-libs' \
  --depends 'krb5-libs' \
  --depends 'zlib' \
  --depends 'libicu' \
  "$OCTOPUSCLI_BINARIES=/opt/octopus/octopuscli" \
  tmp_usr_bin/=/usr/bin/ \
  || exit
# Note: Microsoft recommends dep 'lttng-ust' but it seems to be unavailable in CentOS 7, so we're omitting it for now.
# As it's related to tracing, hopefully it will not be required for normal usage.

mv -f *.{deb,rpm} $ARTIFACTS || exit
