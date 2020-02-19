#!/bin/bash
echo 'Deprecated: The Octo command has been renamed to octo.' >&2
"$(dirname "$0")/octo" "$@"
