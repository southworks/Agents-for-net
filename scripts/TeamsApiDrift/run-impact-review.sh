#!/usr/bin/env bash
# Run only repository read/search tools; capture the final response separately from diagnostics.
set -euo pipefail
: "${ARTIFACTS:?ARTIFACTS is required}"
: "${REVIEW_WORKSPACE:?REVIEW_WORKSPACE is required}"
artifacts_dir=$(cd "$ARTIFACTS" && pwd)
workspace_dir=$(cd "$REVIEW_WORKSPACE" && pwd)
instructions=$(cat .github/instructions/teams-api-impact-review-instructions.md)
printf 'Starting implementation review with read/search tools in %s\n' "$workspace_dir"
# Keep this list explicit. No shell, editing, network tools, MCPs, custom instructions, or delegation.
if copilot -C "$workspace_dir" --silent --no-ask-user --no-custom-instructions \
  --available-tools='view,grep,glob' --allow-tool=read \
  --deny-tool='shell,write,url,memory' --disable-builtin-mcps --disallow-temp-dir \
  --log-level none -p "$instructions" > "$artifacts_dir/impact-review.raw.json" \
  2> "$artifacts_dir/impact-review-agent.stderr.log"; then
  printf 'Summary: AI implementation review completed; raw response: %s/impact-review.raw.json\n' "$artifacts_dir"
else
  status=$?
  printf 'Summary: AI implementation review FAILED (exit %s); see impact-review-agent.stderr.log.\n' "$status" >&2
  exit "$status"
fi
