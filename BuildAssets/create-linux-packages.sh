#!/bin/bash

# Remove existing packages, fpm doesnt like to overwrite
rm *.{deb,rpm}

fpm --version $VERSION \
  --name octopuscli \
  --input-type dir \
  --output-type deb \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Command line tool for Octopus Deploy' \
  --deb-no-default-config-files \
  $OCTOPUSCLI_BINARIES=/opt/octopus/octopuscli

fpm --version $VERSION \
  --name octopuscli \
  --input-type dir \
  --output-type rpm \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Command line tool for Octopus Deploy' \
  $OCTOPUSCLI_BINARIES=/opt/octopus/octopuscli

#mkdir octopuscli
#
#cp -a $OCTOPUSCLI_BINARIES/. octopuscli/
#
#tar czvf octopuscli-$VERSION-linux_x64.tar.gz tentacle
#
#mkdir -p $ARTIFACTS
#
#cp -f *.tar.gz $ARTIFACTS

cp -f *.{deb,rpm} $ARTIFACTS
