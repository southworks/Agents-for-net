#!/usr/bin/env bash
# Summarize external build, test, and AI commands without changing their output or exit status.
set -uo pipefail
if (( $# < 2 )); then
  printf 'Usage: run-step.sh <description> <command> [arguments...]\n' >&2
  exit 2
fi
description=$1
shift
printf 'Starting: %s\n' "$description" >&2
if "$@"; then
  printf 'Summary: %s succeeded.\n' "$description" >&2
else
  status=$?
  printf 'Summary: %s FAILED (exit code %s).\n' "$description" "$status" >&2
  exit "$status"
fi
