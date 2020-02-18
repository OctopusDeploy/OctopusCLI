#!/bin/bash
echo 'Note: The Octo command has been renamed to octo. Redirecting...' >&2
"$(dirname "$0")/octo" "$@"
