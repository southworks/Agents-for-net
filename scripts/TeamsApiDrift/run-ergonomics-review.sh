#!/usr/bin/env bash
set -euo pipefail
: "${ARTIFACTS:?ARTIFACTS is required}"
: "${REVIEW_WORKSPACE:?REVIEW_WORKSPACE is required}"
: "${TOOL:?TOOL is required}"
: "${COPILOT_MODEL:?COPILOT_MODEL is required}"
artifacts_dir=$(cd "$ARTIFACTS" && pwd)
workspace_dir=$(cd "$REVIEW_WORKSPACE" && pwd)
task_dir="$workspace_dir/ergonomics-tasks"
mkdir -p "$artifacts_dir/ergonomics-tasks"
input="$artifacts_dir/ergonomics-input.json"
discovery="$artifacts_dir/ergonomics-discovery.json"
copilot_home=${COPILOT_HOME:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}/teams-ergonomics-copilot}
failed=0
: > "$artifacts_dir/ergonomics-review.raw.json"
dotnet run --project "$TOOL" -c Release --no-build -- write-agent-metadata \
    --output "$artifacts_dir/ergonomics-review-agent.json" --model "$COPILOT_MODEL" --cli-version "$(copilot --version)"

run_task() (
    local task=$1
    export ARTIFACTS="$artifacts_dir" REVIEW_WORKSPACE="$workspace_dir"
    export REVIEW_PREFIX="ergonomics-$task" REVIEW_INPUT=ergonomics-input.json
    export REVIEW_COMMAND=validate-ergonomics-task
    export REVIEW_INSTRUCTIONS=.github/instructions/teams-ergonomics-review-instructions.md
    export REVIEW_TASK="$task_dir/$task.json"
    export REVIEW_FEEDBACK_NAME="ergonomics-tasks/$task-feedback.json"
    export REVIEW_PREVIOUS_NAME="ergonomics-tasks/$task-previous.txt"
    export COPILOT_HOME="$copilot_home/$task"
    export GITHUB_STEP_SUMMARY="$artifacts_dir/ergonomics-$task.summary.md"
    unset REVIEW_DISCOVERY
    [[ "$task" != operations-* ]] || export REVIEW_DISCOVERY="$discovery"
    bash scripts/TeamsApiDrift/run-review.sh
)

wait_tasks() {
    local pid
    for pid in "${pids[@]}"; do
        if ! wait "$pid"; then failed=1; fi
    done
    pids=()
}

# Each task has independent context, correction files and CLI state. Bound concurrency
# to three processes; this launches no delegation tools inside a reviewer session.
for phase in discovery operations; do
    plan_arguments=()
    [[ "$phase" != operations ]] || plan_arguments+=(--discovery "$discovery")
    dotnet run --project "$TOOL" -c Release --no-build -- plan-ergonomics-tasks \
        --input "$input" --workspace "$workspace_dir" --phase "$phase" \
        --tasks-dir "$task_dir" --output "$artifacts_dir/ergonomics-$phase-plan.json" "${plan_arguments[@]}"
    mapfile -t tasks < <(jq -r '.[]' "$artifacts_dir/ergonomics-$phase-plan.json" | tr -d '\r')
    pids=()
    for task in "${tasks[@]}"; do
        cp "$task_dir/$task.json" "$artifacts_dir/ergonomics-tasks/$task.json"
        run_task "$task" &
        pids+=("$!")
        if (( ${#pids[@]} == 3 )); then wait_tasks; fi
    done
    wait_tasks
    parts=()
    if [[ "$phase" == operations ]]; then parts+=(--part "$discovery"); fi
    for task in "${tasks[@]}"; do
        summary="$artifacts_dir/ergonomics-$task.summary.md"
        if [[ -n "${GITHUB_STEP_SUMMARY:-}" && -f "$summary" ]]; then
            cat "$summary" >> "$GITHUB_STEP_SUMMARY"
        fi
        accepted="$artifacts_dir/ergonomics-$task.raw.json.accepted.json"
        [[ ! -s "$accepted" ]] || parts+=(--part "$accepted")
    done
    if [[ "$phase" == discovery ]]; then
        output="$discovery"
    else
        output="$artifacts_dir/ergonomics-review.raw.json"
    fi
    dotnet run --project "$TOOL" -c Release --no-build -- merge-ergonomics-tasks \
        --input "$input" --phase "$phase" --output "$output" "${parts[@]}"
done
printf 'Summary: Merged accepted ergonomics tasks; failed tasks: %s. The final validator and completion gate assess the complete review.\n' "$failed"
exit "$failed"
