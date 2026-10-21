#!/usr/bin/env bash
# Offline orchestration regressions: no model calls, credentials, or package downloads.
set -euo pipefail
repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$repo_dir"
test_root=$(mktemp -d "${TMPDIR:-/tmp}/teams-review-tests.XXXXXX")
cleanup() {
  # Only remove the directory created by mktemp for this test invocation.
  if [[ -d "$test_root" && "${test_root##*/}" == teams-review-tests.* ]]; then
    rm -rf -- "$test_root"
  fi
}
trap cleanup EXIT

copilot() {
  local count=0 arg
  [[ ! -f "$MOCK_STATE/calls" ]] || count=$(cat "$MOCK_STATE/calls")
  count=$((count + 1))
  printf '%s' "$count" > "$MOCK_STATE/calls"
  for arg in --silent --no-ask-user --no-custom-instructions --available-tools=view,grep,glob --allow-tool=read --deny-tool=shell,write,url,memory --disable-builtin-mcps --disallow-temp-dir; do
    [[ " $* " == *" $arg "* ]] || { printf 'Missing permission constraint: %s\n' "$arg" >&2; return 98; }
  done
  if (( count > 1 )); then
    [[ -s "$REVIEW_WORKSPACE/review-feedback.json" && -s "$REVIEW_WORKSPACE/previous-review.txt" ]] || return 97
    [[ " $* " == *'Correction attempt: read review-feedback.json and previous-review.txt'* ]] || return 96
  fi
  if [[ "$MOCK_CASE" == agent-failure ]]; then printf 'partial failed response'; return 42; fi
  printf 'response attempt %s' "$count"
}
dotnet() {
  local output='' report='' count
  count=$(cat "$MOCK_STATE/calls")
  printf '%s\n' "$count" >> "$MOCK_STATE/validations"
  [[ "$MOCK_CASE" != validator-failure ]] || return 2
  while (( $# )); do
    case "$1" in
      --output) output=$2; shift ;;
      --report) report=$2; shift ;;
    esac
    shift
  done
  [[ -n "$output" && -n "$report" ]] || return 95
  printf 'attempt %s report' "$count" > "$report"
  if [[ "$MOCK_CASE" == always-invalid || ( "$MOCK_CASE" == correction && "$count" == 1 ) ]]; then
    printf '{"valid":false,"canProceed":false,"errors":["Invalid source quote"]}' > "$output"
    return 1
  fi
  # A valid blocked review must not be retried in search of a safe verdict.
  printf '{"valid":true,"canProceed":false,"errors":[]}' > "$output"
}
export -f copilot dotnet

for scenario in first-valid correction always-invalid agent-failure validator-failure; do
  export MOCK_CASE=$scenario MOCK_STATE="$test_root/$scenario"
  export ARTIFACTS="$MOCK_STATE/artifacts" REVIEW_WORKSPACE="$MOCK_STATE/snapshot" TOOL=mock-project
  mkdir -p "$ARTIFACTS" "$REVIEW_WORKSPACE"
  printf 'stale successful response' > "$ARTIFACTS/impact-review.raw.json"
  status=0
  bash scripts/TeamsApiDrift/run-impact-review.sh > "$MOCK_STATE/log" 2>&1 || status=$?
  expected_calls=1 expected_status=0
  case "$scenario" in
    correction) expected_calls=2 ;;
    always-invalid) expected_calls=3; expected_status=1 ;;
    agent-failure) expected_status=42 ;;
    validator-failure) expected_status=2 ;;
  esac
  [[ "$status" == "$expected_status" && "$(cat "$MOCK_STATE/calls")" == "$expected_calls" ]] || { cat "$MOCK_STATE/log"; exit 1; }
  cmp "$ARTIFACTS/impact-review.raw.json" "$ARTIFACTS/impact-review.attempt-$expected_calls.raw.json"
  if [[ "$scenario" == agent-failure ]]; then
    [[ ! -f "$MOCK_STATE/validations" ]]
  elif [[ "$scenario" == validator-failure ]]; then
    [[ "$(cat "$MOCK_STATE/validations")" == 1 ]]
    [[ ! -f "$REVIEW_WORKSPACE/review-feedback.json" ]]
  else
    [[ "$(wc -l < "$MOCK_STATE/validations" | tr -d ' ')" == "$expected_calls" ]]
    for ((i=1; i<=expected_calls; i++)); do
      [[ -s "$ARTIFACTS/impact-review.attempt-$i.raw.json" && -s "$ARTIFACTS/impact-review.attempt-$i.validation.json" && -s "$ARTIFACTS/impact-review.attempt-$i.report.md" ]]
    done
  fi
  printf 'PASS: %s (%s attempts, exit %s)\n' "$scenario" "$expected_calls" "$status"
done
