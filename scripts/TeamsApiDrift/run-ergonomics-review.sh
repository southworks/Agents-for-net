#!/usr/bin/env bash
set -euo pipefail
export REVIEW_PREFIX=ergonomics-review
export REVIEW_INPUT=ergonomics-input.json
export REVIEW_COMMAND=validate-ergonomics-review
export REVIEW_INSTRUCTIONS=.github/instructions/teams-ergonomics-review-instructions.md
bash scripts/TeamsApiDrift/run-review.sh
