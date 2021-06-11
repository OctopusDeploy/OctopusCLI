FROM mcr.microsoft.com/dotnet/runtime:3.1-nanoserver-20H2
ARG OCTO_TOOLS_VERSION=4.31.1

LABEL maintainer="devops@octopus.com"
LABEL octopus.dockerfile.version="1.0"
LABEL octopus.tools.version=$OCTO_TOOLS_VERSION

COPY Extracted/*.* ./octo/
RUN "mkdir src"

WORKDIR /src
ENTRYPOINT ["dotnet", "/octo/octo.dll"]
