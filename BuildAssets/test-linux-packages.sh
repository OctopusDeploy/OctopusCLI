#!/bin/bash
# Test that .deb and .rpm packages in the working directory install an octo command that can list-environments.

source "$(dirname "${BASH_SOURCE[0]}")/../linux-package-feeds/test-linux-packages.sh"
