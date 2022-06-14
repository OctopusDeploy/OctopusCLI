This repository contains the Octopus CLI (`octo`) for [Octopus][1], a user-friendly DevOps tool for developers that supports release management, deployment automation, and operations runbooks. You can use it to create and deploy releases, create and push packages, and manage environments. Dom was here...

`octo` can be [downloaded from the Octopus downloads page][2].

## Documentation
- [octo][3]

## Issues
Please see [Contributing](CONTRIBUTING.md)

## Development

### Pre-requisites

You need the following items installed on your system:
- Rider, VSCode or Visual Studio 15.3
- .NET Core SDK 6.x

### Build and Test

Run the build script to build, test and package the project. 

**Do this before pushing as it will run the surface area tests as well, which require approval on almost every change.**

#### Unix-like systems
```
# on Unix-like systems we don't generate the OctopusTools NuGet package as it calls `nuget.exe` to create the package.
$ ./build.sh
```

#### Windows
```
> build.cmd
```

### Publish a new version

To release a new version, tag `main` with the next `<major>.<minor>.<patch>` version, [GitHub Actions][5] will build, test and produce the required packages and [Octopus Deploy][6] will do publish the packages to the appropriate locations.

Every successful GitHub Actions build for all branches will be pushed to Feedz.io.

## Compatibility
See the [Compatibility][4] page in our docs

[1]: https://octopus.com
[2]: https://octopus.com/downloads
[3]: https://octopus.com/docs/api-and-integration/octo.exe-command-line
[4]: https://octopus.com/docs/api-and-integration/compatibility
[5]: https://github.com/OctopusDeploy/OctopusCLI/actions/workflows/build.yml
[6]: https://deploy.octopus.app/app#/Spaces-62/projects/octopus-cli/deployments
