This repository contains the command line tool (`Octo.exe`) for [Octopus Deploy][1], an automated deployment server for professional .NET developers. You can use it to create and deploy releases, create and push packages, and manage environments.

`Octo.exe` can be [downloaded from the Octopus downloads page][2].

## Documentation
- [Octo.exe][3]

## Issues
Please see [Contributing](CONTRIBUTING.md)

## Development
You need:
- VSCode or Visual Studio 15.3 to compile the solution
- dotnet core 2.1.302 SDK

Run `Build.cmd` to build, test and package the project. Do this before pushing as it will run the surface area tests as well,
which require approval on almost every change.

To release to Nuget, tag `master` with the next major, minor or patch number, [TeamCity](https://build.octopushq.com/project.html?projectId=OctopusDeploy_OctopusCLI&tab=projectOverview) will do the rest. Kick off the `Release: OctopusCLI to Octopus3` build again if any of the dependencies fail.

Every successful TeamCity build for all branches will be pushed to Feedz.io.

## Compatibility
See the [Compatibility][4] page in our docs

[1]: https://octopus.com
[2]: https://octopus.com/downloads
[3]: https://octopus.com/docs/api-and-integration/octo.exe-command-line
[4]: https://octopus.com/docs/api-and-integration/compatibility
