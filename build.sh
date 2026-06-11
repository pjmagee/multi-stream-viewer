#!/bin/sh
# Cloudflare Pages build script for this Blazor WebAssembly app.
#
# Cloudflare's build image ships no .NET SDK, so we install it on the fly and
# then publish. The static site lands in output/wwwroot.
#
# Cloudflare Pages project settings:
#   Build command:          ./build.sh   (or: sh ./build.sh)
#   Build output directory: output/wwwroot
#   Production branch:      main
set -e

curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --install-dir ./dotnet
./dotnet/dotnet --version
./dotnet/dotnet publish MultiStreamViewer -c Release -o output
