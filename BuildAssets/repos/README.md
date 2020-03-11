# README

# JFrog Artifactory Initial Setup

*TODO: Automate this. It might be more appropriate as a runbook than part of package deployment, because (1) it belongs equally to Tentacle and Octopus CLI, (2) we'd usually want to bootstrap some initial packages, (3) it requires administrative permissions.*

- Admin: General Security Configuration
  - First remove anonymous from permissions & groups
  - [x] Allow Anonymous Access
  - [x] Hide Existence of Unauthorized Resources
  - Allow Basic Read of Build Related Info
        [ ] Apply on Anonymous Access
- Admin: Repositories: Local: New
  - Type: debian
  - Key: apt-prerelease
- Admin: Repositories: Local: New
  - Type: debian
  - Key: apt
- Admin: Repositories: Local: New
  - Type: rpm
  - Key: rpm-prerelease
  - [ ] Auto Calculate RPM Metadata
  - [x] Enable file list indexing
  - RPM Metadata Folder Depth: 1
- Admin: Repositories: Local: New
  - Type: rpm
  - Key: rpm
  - [ ] Auto Calculate RPM Metadata
  - [x] Enable file list indexing
  - RPM Metadata Folder Depth: 1
- Admin: Signing Keys
  - Added public/private/passphrase
- Admin: Users: New
  - User Name: linux-package-uploader
  - Email Address: devops@octopus.com
  - Set password
  - Remove from groups
- Admin: Permissions: Add
  - Name: linux-package-uploader
  - Repos: apt-prerelease, apt, rpm-prerelease, rpm
  - Users: linux-package-uploader
  - Actions: Repository up to Manage
- Admin: Permissions: Add
  - Name: linux-package-downloader
  - Repos: apt-prerelease, apt, rpm-prerelease, rpm
  - Users: anonymous
  - Actions: Read
