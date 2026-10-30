#!/usr/bin/env bash
# Exercise the workflow's JavaScript publisher offline, with Bash fixtures and assertions.
set -euo pipefail

repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$repo_dir"

for tool in jq node; do
    command -v "$tool" > /dev/null || {
        printf '%s is required for publication tests.\n' "$tool" >&2
        exit 1
    }
done

temp_parent=$(cd "${TMPDIR:-/tmp}" && pwd -L)
test_root=$(mktemp -d "$temp_parent/teams-ergonomics-publication-tests.XXXXXX")

cleanup() {
    case "$test_root" in
        "$temp_parent"/teams-ergonomics-publication-tests.*)
            rm -rf -- "$test_root"
            ;;
    esac
}

trap cleanup EXIT

fail() {
    printf 'FAIL: %s\n' "$*" >&2
    exit 1
}

contains() {
    [[ "$1" == *"$2"* ]] || fail "Expected '$2'"
}

pass() {
    printf 'PASS: ergonomics publication: %s\n' "$1"
}

workflow=.github/workflows/teams-api-drift-scheduled.yml
publication_step=$(awk '
    $0 == "      - name: Create or update ergonomics review issue" { found=1; next }
    found && /^      - name:/ { exit }
    found { print }
' "$workflow")
contains "$publication_step" "needs.ergonomics_analysis.result == 'success'"
contains "$publication_step" "steps.download_ergonomics.outcome == 'success'"
awk '/^          script: \|$/ { script=1; next } script { sub(/^            /, ""); print }' \
    <<< "$publication_step" > "$test_root/publisher.js"
[[ -s "$test_root/publisher.js" ]] || fail 'Missing ergonomics issue publisher'
pass 'failed jobs and downloads cannot overwrite the issue'

case_index=0
marker='<!-- teams-api-ergonomics-scheduled -->'

new_case() {
    local recommendations=${1:-true} existing=${2:-false}
    case_index=$((case_index + 1))
    case_dir="$test_root/case-$case_index"
    mkdir -p "$case_dir"
    jq -n --argjson recommendations "$recommendations" \
        '{schemaVersion: 1, valid: true, hasRecommendations: $recommendations, decisions: [], coverage: [], additionalOperations: [], discovery: []}' > "$case_dir/validation.json"
    jq -n --arg marker "$marker" --argjson existing "$existing" '
        [{number: 10, body: "<!-- teams-api-drift-scheduled --> Existing drift issue."},
         {number: 11, body: $marker, pull_request: {}}] +
        (if $existing then [{number: 42, body: ($marker + "\nOld report")}] else [] end)
    ' > "$case_dir/issues.json"
    printf '# MSTeams API ergonomics review\n\nProposals.\n' > "$case_dir/report.md"
}

expect_publication() {
    local expected=$1 status=0
    # Node executes the actual GitHub Script code; fixture setup and assertions stay in Bash.
    (
        cd "$case_dir"
        node - <<'JS'
const fs = require('node:fs');
const vm = require('node:vm');
const calls = [];
const response = action => async args => {
    calls.push({ action, ...args });
    return { data: { html_url: `https://example.com/${action}` } };
};
const context = vm.createContext({
    require: name => {
        if (name !== 'fs') throw new Error(`Unexpected module: ${name}`);
        return fs;
    },
    process: { env: { REPORT_PATH: 'report.md', VALIDATION_PATH: 'validation.json', GITHUB_STEP_SUMMARY: 'summary.md' } },
    context: { repo: { owner: 'test', repo: 'sdk' }, runId: 123 },
    github: {
        paginate: async () => JSON.parse(fs.readFileSync('issues.json', 'utf8')),
        rest: { issues: { listForRepo: {}, create: response('create'), update: response('update') } }
    },
    console
});
(async () => {
    try {
        await vm.runInContext(`(async () => { ${fs.readFileSync('../publisher.js', 'utf8')}\n })()`, context);
    } finally {
        fs.writeFileSync('calls.json', JSON.stringify(calls));
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
JS
    ) > "$case_dir/publication.log" 2>&1 || status=$?

    [[ "$status" == "$expected" ]] || {
        cat "$case_dir/publication.log"
        fail "Publisher exit $status, expected $expected"
    }
}

new_case
expect_publication 0
jq -e --arg marker "$marker" '
    length == 1 and .[0].action == "create" and .[0].title == "MSTeams API ergonomics review" and
    (.[0].body | startswith($marker)) and (.[0].body | contains("/actions/runs/123"))
' "$case_dir/calls.json" > /dev/null || fail 'Recommendations did not create the separate issue'
pass 'recommendations create a separate issue with the report and artifact link'

new_case true true
expect_publication 0
jq -e 'length == 1 and .[0].action == "update" and .[0].issue_number == 42 and (.[0] | has("state") | not)' \
    "$case_dir/calls.json" > /dev/null || fail 'Existing issue was not updated or was automatically closed'
pass 'updates target the matching open issue, excluding drift issues and pull requests'

new_case false
expect_publication 0
jq -e 'length == 0' "$case_dir/calls.json" > /dev/null || fail 'Empty recommendations created an issue'
pass 'empty recommendations do not create an issue'

new_case false true
printf 'No proposals.\nReview limitations.\n' > "$case_dir/report.md"
expect_publication 0
jq -e 'length == 1 and .[0].action == "update" and .[0].issue_number == 42 and
    (.[0].body | contains("Review limitations.")) and (.[0] | has("state") | not)' \
    "$case_dir/calls.json" > /dev/null || fail 'Empty review did not update the existing issue with limitations'
pass 'empty reviews update existing issues with their latest limitations'

new_case true true
jq '.valid = false' "$case_dir/validation.json" > "$case_dir/invalid.json"
mv "$case_dir/invalid.json" "$case_dir/validation.json"
expect_publication 1
contains "$(cat "$case_dir/publication.log")" 'A valid ergonomics review is required'
jq -e 'length == 0' "$case_dir/calls.json" > /dev/null || fail 'Invalid review changed the issue'
pass 'invalid reviews preserve the existing issue'

new_case true true
printf '{broken' > "$case_dir/validation.json"
expect_publication 1
jq -e 'length == 0' "$case_dir/calls.json" > /dev/null || fail 'Malformed review changed the issue'
pass 'malformed validation preserves the existing issue'

new_case true true
jq '.schemaVersion = 99' "$case_dir/validation.json" > "$case_dir/unsupported.json"
mv "$case_dir/unsupported.json" "$case_dir/validation.json"
expect_publication 1
jq -e 'length == 0' "$case_dir/calls.json" > /dev/null || fail 'Unsupported schema changed the issue'
pass 'unsupported schema versions cannot publish'

new_case true true
jq 'del(.coverage)' "$case_dir/validation.json" > "$case_dir/incomplete.json"
mv "$case_dir/incomplete.json" "$case_dir/validation.json"
expect_publication 1
jq -e 'length == 0' "$case_dir/calls.json" > /dev/null || fail 'Report without coverage changed the issue'
pass 'missing operation coverage prevents publication'

for field in additionalOperations discovery; do
    new_case true true
    jq --arg field "$field" 'del(.[$field])' "$case_dir/validation.json" > "$case_dir/incomplete.json"
    mv "$case_dir/incomplete.json" "$case_dir/validation.json"
    expect_publication 1
    jq -e 'length == 0' "$case_dir/calls.json" > /dev/null || fail "Report without $field changed the issue"
    pass "missing $field prevents publication"
done

new_case
awk 'BEGIN {
    print "# Long report\n\n````csharp"
    for (i=0; i<6000; i++) print "caller example"
    print "````"
}' > "$case_dir/report.md"
expect_publication 0
jq -e 'length == 1 and (.[0].body | length < 56000) and (.[0].body | contains("Report excerpt"))' \
    "$case_dir/calls.json" > /dev/null || fail 'Long report was not bounded'
jq -e '.[0].body | contains("\n````\n\n_Report excerpt")' "$case_dir/calls.json" > /dev/null ||
    fail 'Truncated code fence was not closed'
pass 'long reports are bounded and truncated code fences are closed'
