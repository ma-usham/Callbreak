#!/usr/bin/env bash
# Runs icontool.cs (a single-file .NET program; the SDK builds it into its own cache on first use).
# Usage: Tools/ArtPipeline/Kit/Icons/icons.sh <command> [options]    (commands: see the header of icontool.cs)
here="$(cd "$(dirname "$0")" && pwd)"
cd "$here/../../../.." || exit 1
exec dotnet run --file "$here/icontool.cs" -- "$@"
