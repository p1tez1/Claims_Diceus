#!/bin/sh
set -eu

if [ -z "${ConnectionStrings__Default:-}" ]; then
  echo "ConnectionStrings__Default must be set." >&2
  exit 1
fi

exec ./efbundle --connection "$ConnectionStrings__Default"

