#!/usr/bin/env sh
# Linux / macOS build. Can be run from any directory.
set -e
cd "$(dirname "$0")"
mkdir -p bin
case "$(uname -s)" in
  Darwin) gcc -std=c99 -O2 -Wall -Wextra -shared -fPIC -o bin/libloanrisk.dylib loanrisk.c -lm ;;
  *)      gcc -std=c99 -O2 -Wall -Wextra -shared -fPIC -o bin/libloanrisk.so    loanrisk.c -lm ;;
esac
echo "native build OK"
