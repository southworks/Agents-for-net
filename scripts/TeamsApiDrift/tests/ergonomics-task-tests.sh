#!/usr/bin/env bash
# Offline orchestration tests. Real capture/metadata/feedback tools, mocked model
# and task planning/validation; C# tests cover task expansion and canonical merge.
set -euo pipefail
repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$repo_dir"
export OUTPUT_TOOL_DLL="$repo_dir/bin/Release/Microsoft.Agents.TeamsApiDrift/Release/net10.0/Microsoft.Agents.TeamsApiDrift.dll"
[[ -s "$OUTPUT_TOOL_DLL" ]] || { echo 'Build the drift tool in Release first.' >&2; exit 1; }
test_root=$(mktemp -d "${TMPDIR:-/tmp}/teams-ergonomics-tasks.XXXXXX")
cleanup() {
    case "$test_root" in "${TMPDIR:-/tmp}"/teams-ergonomics-tasks.*) rm -rf -- "$test_root" ;; esac
}
trap cleanup EXIT

copilot() {
    if [[ "${1:-}" == --version ]]; then echo 'GitHub Copilot CLI mock'; return; fi
    [[ "$REVIEW_COMMAND" == validate-ergonomics-task && -s "$REVIEW_TASK" ]] || return 91
    [[ "$COPILOT_HOME" == */"${REVIEW_PREFIX#ergonomics-}" ]] || return 91
    [[ "$REVIEW_FEEDBACK_NAME" == "ergonomics-tasks/${REVIEW_PREFIX#ergonomics-}-feedback.json" ]] || return 91
    [[ " $* " == *'Do not read the global ergonomics-input.json wholesale.'* ]] || return 91
    [[ " $* " == *'--available-tools=view,grep,glob'* && " $* " == *'--deny-tool=shell,write,url,memory'* ]] || return 91
    local count=0
    [[ ! -f "$ARTIFACTS/$REVIEW_PREFIX.calls" ]] || count=$(cat "$ARTIFACTS/$REVIEW_PREFIX.calls")
    count=$((count + 1)); echo "$count" > "$ARTIFACTS/$REVIEW_PREFIX.calls"
    if [[ "$MOCK_CASE" == execution-failure && "$REVIEW_PREFIX" == ergonomics-discovery-002 ]]; then return 42; fi
    if (( count > 1 )); then
        [[ -s "$REVIEW_WORKSPACE/$REVIEW_FEEDBACK_NAME" && -s "$REVIEW_WORKSPACE/$REVIEW_PREVIOUS_NAME" ]] || return 92
        [[ " $* " == *"read $REVIEW_FEEDBACK_NAME and $REVIEW_PREVIOUS_NAME"* ]] || return 92
    fi
    jq -cn --arg task "$REVIEW_PREFIX" '{type:"assistant.message",data:{content:$task,model:"test-model"}}'
    echo '{"type":"result","sessionId":"test","exitCode":0}'
}

dotnet() {
    while [[ "$1" != -- ]]; do shift; done; shift
    local command=$1; shift
    if [[ "$command" == write-agent-metadata || "$command" == extract-agent-response || "$command" == write-review-feedback ]]; then
        command dotnet "$OUTPUT_TOOL_DLL" "$command" "$@"; return
    fi
    local output='' tasks_dir='' phase='' review='' task='' part parts=0
    while (( $# )); do
        case "$1" in
            --output) output=$2;;
            --tasks-dir) tasks_dir=$2;;
            --phase) phase=$2;;
            --review) review=$2;;
            --task) task=$2;;
            --report) report=$2;;
            --part)
                part=$2
                [[ -s "$part" ]] || return 95
                parts=$((parts + 1));;
        esac
        shift 2
    done
    case "$command" in
        plan-ergonomics-tasks)
            mkdir -p "$tasks_dir"
            if [[ "$phase" == discovery ]]; then
                printf '["discovery-001","discovery-002","discovery-003","discovery-004"]' > "$output"
            else
                printf '["operations-001","operations-002"]' > "$output"
            fi
            while IFS= read -r task; do echo '{}' > "$tasks_dir/$task.json"; done < <(jq -r '.[]' "$output" | tr -d '\r')
            ;;
        validate-ergonomics-task)
            [[ "$task" == "$REVIEW_TASK" ]] || return 91
            if [[ "$MOCK_CASE" == correction && "$REVIEW_PREFIX" == ergonomics-operations-001 && "$(cat "$ARTIFACTS/$REVIEW_PREFIX.calls")" == 1 ]]; then
                echo '{"valid":false,"errors":["Invalid source quote"]}' > "$output"
                echo 'Invalid evidence' > "$report"
                return 1
            fi
            echo '{"valid":true,"analysisComplete":false,"errors":[]}' > "$output"
            echo 'Task diagnostics' > "$report"
            echo '{}' > "$review.accepted.json"
            ;;
        merge-ergonomics-tasks)
            if [[ "$phase" == discovery ]]; then
                local expected=4
                [[ "$MOCK_CASE" != execution-failure ]] || expected=3
                [[ "$parts" == "$expected" ]] || return 96
            else
                [[ "$parts" == 3 ]] || return 97
            fi
            echo '{"merged":true}' > "$output"
            ;;
        *) return 93;;
    esac
}
export -f copilot dotnet
for scenario in success correction execution-failure; do
    export MOCK_CASE=$scenario ARTIFACTS="$test_root/$scenario/artifacts" REVIEW_WORKSPACE="$test_root/$scenario/snapshot"
    export TOOL=mock COPILOT_MODEL=test-model GITHUB_STEP_SUMMARY="$test_root/$scenario/summary.md"
    mkdir -p "$ARTIFACTS" "$REVIEW_WORKSPACE"
    echo '{}' > "$ARTIFACTS/ergonomics-input.json"
    echo stale > "$ARTIFACTS/ergonomics-discovery-002.raw.json.accepted.json"
    status=0 expected=0
    bash scripts/TeamsApiDrift/run-ergonomics-review.sh > "$test_root/$scenario/log" 2>&1 || status=$?
    [[ "$scenario" != execution-failure ]] || expected=1
    [[ "$status" == "$expected" ]] || { cat "$test_root/$scenario/log"; exit 1; }
    [[ -s "$ARTIFACTS/ergonomics-review-agent.json" && -s "$ARTIFACTS/ergonomics-review.raw.json" ]]
    [[ "$(cat "$ARTIFACTS/ergonomics-review.raw.json")" == *merged* ]]
    if [[ "$scenario" == correction ]]; then
        [[ "$(cat "$ARTIFACTS/ergonomics-operations-001.calls")" == 2 ]]
        [[ "$(cat "$ARTIFACTS/ergonomics-operations-002.calls")" == 1 ]]
    fi
    if [[ "$scenario" == execution-failure ]]; then
        [[ ! -s "$ARTIFACTS/ergonomics-discovery-002.raw.json.accepted.json" ]]
        [[ -s "$ARTIFACTS/ergonomics-operations-001.raw.json.accepted.json" ]]
    fi
    printf 'PASS: ergonomics tasks: %s; isolated corrections, merged results and diagnostics retained\n' "$scenario"
done
