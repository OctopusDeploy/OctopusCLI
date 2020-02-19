#!/bin/bash

# this is inlined from this dockerfile - https://github.com/OctopusDeploy/OctopusTentacle/blob/master/docker/debian-tools/Dockerfile
# we should look to pre-build it, so we dont have to waste time during tentacle / octopuscli builds 

apt-get update && apt-get install -y \
    gnupg1 \ 
    wget \
    ruby-dev \
    gcc \
    make \
    ruby \
    gpgv1 \
    rpm

rm -rf /var/lib/apt/lists/*
gem install fpm --no-ri --no-rdoc
gem install pleaserun

#### end copy from dockerfile  

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