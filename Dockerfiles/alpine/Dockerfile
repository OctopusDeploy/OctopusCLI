FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine

# Alpine base image does not have ICU libraries available
# https://github.com/dotnet/corefx/blob/master/Documentation/architecture/globalization-invariant-mode.md
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT false
ENV LC_ALL en_US.UTF-8
ENV LANG en_US.UTF-8

# The dotnetcore bootstrapper doesnt work in alpine shell (built for bash)
# This allows invoking octo if running interactive container
RUN mkdir /octo &&\
        echo "dotnet /octo/octo.dll \"\$@\"" > /octo/alpine &&\
        ln /octo/alpine /usr/bin/octo &&\
        chmod +x /usr/bin/octo

ARG OCTO_TOOLS_VERSION=4.31.1

LABEL maintainer="devops@octopus.com"
LABEL octopus.dockerfile.version="1.0"
LABEL octopus.tools.version=$OCTO_TOOLS_VERSION

ADD OctopusTools.$OCTO_TOOLS_VERSION.portable.tar.gz /octo

WORKDIR /src
ENTRYPOINT ["dotnet", "/octo/octo.dll"]
