# Teams API Drift Detector — Technical Description

## Purpose

The Teams API Drift Detector identifies API changes between two versions of `Microsoft.Teams.Apps`, determines whether the MSTeams extension uses the affected APIs, and produces deterministic findings for maintainers. CI workflows give bounded evidence to Copilot CLI for an advisory report and a separate structured usage assessment. A validated safe assessment can clear an internal nullability finding from the final gate; the original deterministic findings remain unchanged for audit.

The tool is a .NET command-line application in `scripts/TeamsApiDrift`. It reads NuGet package metadata without loading package assemblies into the running process.

## Component inventory

| Component | Responsibility |
| --- | --- |
| `Microsoft.Agents.TeamsApiDrift.csproj` | Builds the `net10.0` CLI and declares its NuGet metadata-reading dependencies. |
| `Program.cs` | Defines the CLI commands, parses arguments, coordinates services, writes outputs, and maps validation failures or exceptions to exit codes. |
| `Contracts.cs` | Defines the serialized contracts for API models, changes, usage data, findings, test summaries, and agent-report validation. |
| `MetadataApiExtractor.cs` | Downloads or resolves package versions, selects reference/implementation assets for supported target frameworks, and extracts the public API from assembly metadata. |
| `ComparisonAndUsage.cs` | Compares normalized API models, scans the compiled MSTeams assembly for dependency use, and validates collected use against the curated manifest. |
| `MetadataValidation.cs` | Validates the repository metadata against built assemblies and requires fresh, document-specific review of relevant source changes. |
| `DriftPipeline.cs` | Resolves centrally managed package versions, classifies changes, renders reports, creates bounded agent context, and validates an agent-authored report. |
| `UsageAssessment.cs` | Builds bounded source/test context, validates AI nullability assessments and exact citations, and produces effective gate findings. |
| `ToolUtilities.cs` | Supplies common argument, JSON, path-containment, hashing, and normalization helpers. |
| `teams-api-usage.json` | Curated inventory of the `Microsoft.Teams.Apps` symbols and members intentionally used by MSTeams, including exposure and affected source files. |
| `teams-capabilities.json` | Maps Teams namespaces and types to capability areas, owners, and additive-change adoption policies. |
| `teams-api-usage-assessment-instructions.md` | Defines the structured safe/review verdicts and evidence required for internal nullability changes. |
| `teams-api-drift-instructions.md` | Constrains the optional Copilot report format, accepted evidence, required finding IDs, and advisory language. |
| `teams-api-drift-pr.yml` | Runs drift detection for package-version pull requests or manual PR-style comparisons, publishes artifacts, and updates an internal PR comment. |
| `teams-api-drift-scheduled.yml` | Compares the repository baseline with the latest stable package on a schedule or by manual dispatch and creates or updates an advisory issue. |
| `Microsoft.Agents.TeamsApiDrift.Tests` | Exercises package resolution, extraction, comparison, usage and repository-metadata validation, classification, rendering, context generation, and report validation. |
| `TeamsApiContractTests.cs` | Compile-time contract coverage that detects source-level breaks in the APIs consumed by MSTeams. |
| `TeamsApiClientExtensionsTests.cs` | Runtime boundary tests for selected MSTeams-to-Teams API interactions. |

The two workflow files are under `.github/workflows`; the test projects and files are under `src/tests`.

## End-to-end data flow

```mermaid
flowchart TD
    Props["Directory.Packages.props"] --> Resolve["resolve-version"]
    Resolve --> Versions["baseline and candidate versions"]
    Versions --> Compare["compare"]
    Sources["NuGet sources and optional NuGet.Config"] --> Compare

    Compare --> Before["before API model"]
    Compare --> After["after API model"]
    Compare --> Diff["raw API diff"]

    CandidateBuild["compiled MSTeams assemblies and PDBs"] --> Collect["collect-usage"]
    Collect --> Collected["collected usage"]
    UsageManifest["teams-api-usage.json"] --> ValidateUsage["validate-usage"]
    Collected --> ValidateUsage
    Before -->|"scheduled: validate the repository manifest"| ValidateUsage
    After -->|"PR: validate the updated manifest"| ValidateUsage

    Diff --> Classify["classify"]
    UsageManifest --> Classify
    Capabilities["teams-capabilities.json"] --> Classify
    Classify --> Findings["classified findings"]

    TestRuns["build and test outcomes"] --> TestSummary["write-test-summary"]
    Findings --> Render["render-report"]
    TestSummary --> Render
    Render --> Deterministic["deterministic report"]

    Findings --> Context
    TestSummary --> Context
    UsageManifest --> Context
    Capabilities --> Context
    Deterministic --> Context["prepare-agent-context"]
    AffectedSource["affected MSTeams C# files"] --> Context
    Context --> AgentContext["bounded agent context"]
    AgentContext --> Copilot["optional Copilot CLI report"]
    Copilot --> ValidateAgent["validate-agent-report"]

    Deterministic --> Publication["workflow artifact and PR comment or issue"]
    ValidateAgent --> Publication
    Findings --> UsageContext["prepare-usage-assessment"]
    AffectedSource --> UsageContext
    BoundaryTests["related source and test files"] --> UsageContext
    TestSummary --> UsageContext
    UsageContext --> Assess["Copilot: safe or needs-human-review"]
    Assess --> ValidateAssessment["validate-usage-assessment"]
    ValidateAssessment --> GateFindings["effective gate findings"]
    Findings --> GateFindings
    ValidateAssessment --> Publication
```

The package comparison is always **baseline → candidate**:

- For a pull request, the baseline is the `Microsoft.Teams.Apps` version on the PR base branch and the candidate is the version being introduced by the PR.
- For a scheduled run, the baseline is the version currently used by `Microsoft.Agents.Extensions.MSTeams` and the candidate is the latest stable released version.

`validate-usage` is a separate manifest-integrity check; it does not compare package versions. It accepts one API model and verifies that `teams-api-usage.json` describes valid APIs for the repository state to which the manifest belongs. In a package-update PR, that repository state has moved to the candidate, so validation uses the `after` model. During a scheduled probe, the repository has not adopted the latest release and its manifest still declares the baseline version, so validation uses the `before` model. The scheduled workflow still compares, builds, and tests the baseline against the latest stable candidate.

`validate-metadata` is the ordinary repository CI gate. It does not contact a NuGet source or compare package releases. Instead, it validates the checked-in manifests against the centrally declared version and the already-built `net8.0` and `net10.0` MSTeams assemblies. When Git history is available, it also compares relevant source and metadata changes with their merge base.

## CLI commands

All commands write diagnostic errors to standard error. An unhandled command or configuration error returns exit code `2`. `validate-usage`, `validate-metadata`, `validate-agent-report`, and `validate-usage-assessment` return `1` for an invalid result; `classify --fail-on-drift` returns `1` for blocking or required findings. Successful commands return `0`.

| Command | Primary inputs | Output | Notes |
| --- | --- | --- | --- |
| `resolve-version` | `--props`, optionally `--package` | Resolved version on standard output | Reads `Directory.Packages.props` and follows MSBuild property indirection. `--props -` reads XML from standard input. |
| `compare` | `--from`, optional `--to`, `--output`, repeated `--source`, optional `--config-file` | Before model, after model, raw diff, and the resolved candidate version on standard output | When `--to` is omitted, selects the latest stable version found in the configured sources. |
| `collect-usage` | One or more `--assembly` values and `--output` | Collected dependency usage JSON | Uses assembly metadata and portable PDBs; it does not execute the MSTeams assembly. |
| `validate-usage` | `--manifest`, `--collected`, `--api-model`, optional `--repository-root`, `--output` | Usage-validation JSON | Checks package/version alignment, symbol/member existence, source paths, PDB evidence, exposure, and stale or missing manifest entries. |
| `validate-metadata` | Repeated `--assembly`, `--manifest`, `--capabilities`, `--props`, optional `--repository-root` and `--base-ref` | Diagnostics and exit status | Performs offline current-state validation and Git-based source-review validation. An explicitly supplied base ref must resolve. |
| `classify` | `--comparison`, `--manifest`, `--capabilities`, `--output`, optional `--fail-on-drift` | Findings JSON | With `--fail-on-drift`, returns `1` when blocking or required findings exist. |
| `write-test-summary` | Repeated `--check <name>=<status>` values and `--output` | Test-summary JSON | Converts workflow step outcomes into one normalized artifact. |
| `render-report` | `--findings`, optional `--test-summary`, `--output` | Deterministic Markdown report | Always derives the maintainer-facing sections from machine-readable artifacts. |
| `prepare-agent-context` | `--findings`, `--manifest`, `--capabilities`, `--deterministic-report`, optional `--test-summary` and `--repository-root`, `--output` | Bounded agent-context JSON | Includes only affected in-repository C# files, redacts secrets, and limits source text size. |
| `prepare-usage-assessment` | `--findings`, `--test-summary`, optional `--repository-root`, `--output` | Bounded source/test context JSON and eligible finding count on stdout | Selects internal `nullability-review` findings, related extension source, and tests. |
| `validate-usage-assessment` | `--findings`, `--context`, `--report`, `--output`, `--gate-output`, `--summary-output` | Validation JSON, effective gate findings, and Markdown assessment summary | Only writes effective findings after the entire assessment validates; invalid/missing JSON returns `1`. |
| `validate-agent-report` | `--report`, `--findings`, `--output` | Agent-report-validation JSON and validation errors on standard error when invalid | Enforces the exact title and section order, known IDs, required IDs, advisory wording, and ID-linked suggested implementation actions. |

Example comparison using an authenticated private feed:

```powershell
dotnet run --project scripts/TeamsApiDrift/Microsoft.Agents.TeamsApiDrift.csproj -- `
  compare `
  --from <installed-version> `
  --to <candidate-version> `
  --source <private-feed-source-name-or-url> `
  --config-file <path-to-NuGet.Config> `
  --output artifacts/teams-api-drift
```

The source name is resolved through the supplied NuGet configuration, including its configured credentials. If no source is supplied, comparison uses NuGet.org. Multiple `--source` arguments are evaluated in order.

## Inputs and manifests

### Package versions and NuGet configuration

The repository baseline is normally the `Microsoft_Teams_Apps_PkgVer` value in `Directory.Packages.props`. Workflows can override the baseline and candidate through dispatch inputs.

`compare` accepts either source URLs or source aliases from a NuGet configuration file. The package service retrieves exact requested versions or enumerates stable versions when choosing the latest candidate. It examines `net8.0` and `net10.0`, preferring the nearest compatible assembly under `ref/` and falling back to `lib/`.

### API models

Each API model records:

- Package name and version.
- Selected asset for each target framework.
- Public and protected types, including kind, accessibility, base type, interfaces, generic constraints, and deprecation state.
- Members, including normalized keys and signatures, accessibility, nullability, and deprecation state.

The metadata reader includes enough signature information to identify enum value changes, nullability changes, generic constraint changes, and member-shape changes without reflection-based assembly loading.

### Usage manifest

`teams-api-usage.json` is a reviewed, source-controlled assertion of intended API use. Each usage associates a dependency type and optional members with:

- Usage kinds.
- Whether the dependency is publicly exposed or internal-only.
- The MSTeams source files affected by a change.

The compiled assembly/PDB scan is independent evidence. `validate-usage` compares that evidence with the manifest so that a stale or incomplete manifest cannot silently drive classification.

The repository gate additionally checks the manifest against both built target frameworks. It verifies the centrally managed dependency version, symbols, tracked members, public exposure in both directions, safe existing source paths, and stale manifest entries. A non-impact acknowledgment cannot suppress these provable errors.

### Reviewed compatibility changes

When a blocking or required finding has been reviewed and the extension already handles it safely, the usage manifest can record a `compatibilityReviews` entry. Each entry specifies `fromVersion`, `toVersion`, `upstreamSymbol`, `member`, `kind`, `before`, `after`, `targetFrameworks`, and a non-empty `reason` explaining why no adaptation is needed and identifying supporting evidence.

Copy the symbol, member, change kind, signatures, and complete framework set from `raw-api-diff.json`, not the generated finding ID. Classification applies the review only when both versions and every change field match exactly (framework order does not matter). Different versions, signatures, or framework sets retain their normal compatibility classification. Historical reviews may remain in the manifest but do not acknowledge future changes.

A matching review changes the finding to `no-action` with category `reviewed-compatible`, adds `compatibility-review` evidence, and includes the justification in its recommended action and deterministic report. The raw comparison and finding remain available for auditing. Reviews do not bypass manifest-integrity checks, compilation, contract tests, or runtime boundary tests. Malformed review entries fail classification explicitly.

### Capability manifest

`teams-capabilities.json` maps namespaces and, where necessary, specific types to product capability areas. A mapping supplies ownership and an adoption policy. The classifier uses the most specific matching mapping to distinguish additive APIs that require feature review from internal implementation opportunities.

The .NET capabilities document keeps `owners` as maintainer labels rather than source-path patterns. Repository validation requires every usage symbol to resolve through an exact `upstreamTypes` entry or the longest matching `upstreamNamespaces` entry.

### Source-review acknowledgments

Relevant MSTeams changes must either update the affected metadata or record a fresh, document-specific non-impact review. For `teams-api-usage.json`, add or update this top-level property:

```json
"sourceReview": {
  "outcome": "no-usage-metadata-change",
  "reason": "Explain specifically why the changed source does not alter Teams API usage metadata."
}
```

For `teams-capabilities.json`, use:

```json
"sourceReview": {
  "outcome": "no-capability-metadata-change",
  "reason": "Explain specifically why the changed usage symbols do not alter capability mapping or policy."
}
```

Use an acknowledgment only after reviewing the corresponding source change. The reason must be non-empty and must differ from the review at the Git base. A usage review cannot satisfy capability review, or vice versa. For committed changes, the metadata update must be in the same commit as the relevant source change or a later commit. For staged, unstaged, or untracked source changes, the corresponding metadata file must also change in the working tree.

Usage review applies to changed C# files that currently or previously reference `Microsoft.Teams.Apps`, files listed in the current or base usage manifest, new C# files, and the MSTeams project file. A substantive change to the `usages` array satisfies the review. When the set of consumed upstream symbols changes, the capability document must receive either a targeted change to the matching capability or its own fresh non-impact acknowledgment.

Run the same offline check used by CI after building the solution:

```powershell
dotnet run --project scripts/TeamsApiDrift/Microsoft.Agents.TeamsApiDrift.csproj -c Debug --no-build -- `
  validate-metadata `
  --assembly bin/Debug/CplTeams/net8.0/Microsoft.Agents.Extensions.MSTeams.dll `
  --assembly bin/Debug/CplTeams/net10.0/Microsoft.Agents.Extensions.MSTeams.dll `
  --manifest scripts/TeamsApiDrift/teams-api-usage.json `
  --capabilities scripts/TeamsApiDrift/teams-capabilities.json `
  --props Directory.Packages.props `
  --repository-root . `
  --base-ref main
```

Omit `--base-ref` to let the tool use the pull-request environment or known `main` refs. Outside a Git checkout, current-state validation still runs and change-review validation is skipped.

## Generated artifacts

The standard artifact directory is `artifacts/teams-api-drift`.

| Artifact | Producer | Main consumers |
| --- | --- | --- |
| `microsoft-teams-apps-before.api.json` | `compare` | Scheduled-workflow usage validation and uploaded evidence |
| `microsoft-teams-apps-after.api.json` | `compare` | PR-workflow usage validation and uploaded evidence |
| `raw-api-diff.json` | `compare` | `classify` and uploaded evidence |
| `collected-usage.json` | `collect-usage` | `validate-usage` |
| `usage-validation.json` | `validate-usage` | Workflow policy and uploaded diagnostics |
| `findings.json` | `classify` | Deterministic report, agent context, report validation, workflow policy |
| `test-summary.json` | `write-test-summary` | Deterministic report and agent context |
| `deterministic-report.md` | `render-report` | Uploaded artifact, scheduled issue body, and full evidence linked by the PR comment |
| `agent-context.json` | `prepare-agent-context` | Copilot CLI only |
| `copilot-report.md` | Copilot CLI in a workflow | `validate-agent-report`, uploaded evidence, and scheduled issue body when valid |
| `agent-report-validation.json` | `validate-agent-report` | Workflow policy and uploaded diagnostics |

The serialized JSON uses camel-case property names and stable ordering where the producer controls a collection. API changes receive deterministic IDs such as `MTAPI-0001` after normalized sorting.

## Comparison and classification

The comparer first detects changes separately for each target framework, then aggregates identical changes and records the frameworks where each change occurs. It can report:

- Framework asset addition or removal.
- Type addition, removal, kind change, accessibility change, base-type change, interface change, and generic-constraint change.
- Member addition, removal, signature change, accessibility change, and nullability change.
- Type or member deprecation addition/removal.
- Enum value changes.

Each raw change has an initial compatibility assessment: breaking, potentially breaking, non-breaking, or unknown. The classifier then combines that change with the usage and capability manifests:

- A removal or structural change to an API used by MSTeams is blocking.
- A potentially breaking change to a publicly exposed API is blocking; the same kind of internal-only change requires adaptation.
- Deprecation of a used API requires maintainer review.
- An additive, unused API is either a feature-review candidate or an internal implementation opportunity according to its capability adoption policy.
- Unused changes that do not require action are retained as no-action findings for traceability.

Every finding carries the raw change evidence, dependency-usage evidence where applicable, and its capability mapping. The deterministic report groups findings into blocking issues, required adaptations, feature-review candidates, internal opportunities, maintainer decisions, and no-action changes.

## Workflow integration

### Main repository CI

`.github/workflows/ci.yml` runs `validate-metadata` immediately after the normal Debug solution build. It reuses the built drift tool and both MSTeams assemblies with `--no-build`, passes the pull-request base SHA through the step environment, and fails before packaging when either metadata document is stale or lacks required review. Push builds still perform current-state validation without a PR base.

### Pull-request workflow

`.github/workflows/teams-api-drift-pr.yml` runs on relevant pull requests and manual dispatch. It:

1. Resolves the baseline from the PR base branch and the candidate from the PR head, unless dispatch inputs override them.
2. Compares the two packages.
3. Builds MSTeams against the candidate and runs the drift-tool tests, compile-time contract tests, and runtime boundary tests for supported frameworks.
4. Collects and validates actual API usage.
5. Classifies findings and renders the deterministic report.
6. For manual runs and trusted internal pull requests, prepares bounded context, invokes Copilot CLI, and validates its report.
7. Uploads the artifacts and upserts a marker-delimited PR comment containing a compact deterministic finding summary and a link to the workflow run. The full deterministic and Copilot reports remain in the artifact rather than being embedded in the comment.
8. Fails policy when a required step failed or when blocking/required findings remain.

Fork pull requests do not receive the agent context, Copilot invocation, or write access needed for comments.

### Scheduled workflow

`.github/workflows/teams-api-drift-scheduled.yml` runs weekly and by manual dispatch. It compares the repository baseline with the latest stable version, performs the same deterministic build/test/collection/classification pipeline, uploads the artifacts, and creates or updates one marker-delimited advisory issue when findings exist.

Scheduled runs use the deterministic report and run the focused AI usage assessment when eligible nullability findings exist. Manual dispatch can additionally create and validate a Copilot report. Copilot CLI authenticates with the short-lived workflow `GITHUB_TOKEN`, which requires `copilot-requests: write` and the corresponding organization policy. The job applies policy independently of the prose report, using validated effective gate findings when available and the original findings otherwise.

## Verification layers

The feature deliberately uses multiple evidence layers:

1. Metadata extraction verifies the shape of the dependency API for both target frameworks.
2. The raw comparison records every normalized API difference independently of current repository use.
3. Assembly and PDB scanning discovers compiled MSTeams references and source locations.
4. Usage validation checks the curated manifest against both compiled evidence and an API model.
5. Repository metadata validation checks the built extension and requires fresh review of relevant source changes.
6. Compile-time contract tests catch source compatibility failures in selected critical contracts.
7. Runtime boundary tests exercise selected behavior at the MSTeams boundary.
8. Unit tests verify deterministic comparison, classification, report, security-boundary, and validation behavior.
9. Workflow policy combines step outcomes and finding severity into the final pass/fail result.

These layers are complementary: a successful build does not replace API comparison, and an agent-authored prose report cannot replace deterministic validation or policy. The structured usage assessment can clear only eligible internal nullability findings after validation.

## Security and trust boundaries

- NuGet credentials remain in the selected NuGet configuration or credential provider; they are not written into drift artifacts.
- Package assemblies and MSTeams assemblies are inspected as metadata rather than executed by the detector.
- Agent context is limited to known artifacts and affected C# files contained beneath the configured source root; Copilot CLI receives that context without shell, file, URL, memory, or built-in MCP tools.
- Sensitive-looking values are redacted, and included source text is length-limited.
- The agent report is advisory and must pass structural and finding-ID validation before publication.
- JSON artifacts, test results, and workflow exit codes are the authoritative inputs to CI policy.

## AI assessment of internal nullability findings

Consumed `nullability-changed` findings with `internal-only` exposure receive the `nullability-review` category. Their initial classification stays `required` (or `review` according to the existing compatibility policy). Publicly exposed contracts, removals, signature changes, and unconsumed changes are not eligible for AI clearance.

Both PR and scheduled workflows prepare a separate `usage-assessment-context.json` after classification and tests. The context contains exact finding signatures and target frameworks, test-check outcomes, affected files, and related implementation/test files selected by member/type names or affected file names. This textual selection is a bounded aid, not a complete semantic reference search. The agent must return `needs-human-review` if it cannot account for a value's downstream use or public contract. Context is limited to 12,000 characters per file and 48,000 source characters total, redacts credentials, rejects source paths outside the extension/test roots and symlinks, and records missing, omitted, and truncated files.

Copilot receives only this context with tools and built-in MCPs disabled. It returns one structured `safe` or `needs-human-review` assessment per eligible finding, with reasoning and exact file/line/quote evidence. A safe verdict must explain the nullability direction, all affected uses, null propagation, intended boundary behavior, and relevant tests that execute under the workflow filters. The agent is instructed to treat source text and comments as untrusted evidence and uncertainty as needing review.

`validate-usage-assessment` checks schema and comparison versions, exact current finding identities/signatures/frameworks, exhaustive unique finding IDs, allowed verdicts, nonempty reasoning, and exact source citations. A safe assessment additionally requires complete supplied source context, citations for every affected implementation file and at least one test file, and passing comparison, drift tests, build, manifest, and contract/boundary checks for both frameworks. The scheduled combined `candidate-tests` check covers both frameworks. Citation validation establishes that quoted evidence exists; semantic correctness of the assessment remains an AI judgment.

Only a completely valid report produces `gate-findings.json`. Safe findings become `no-action` with category `ai-assessed-safe` and evidence `ai-usage-assessment` in that separate artifact; all other findings remain unchanged. A human-review verdict retains the original severity. Malformed, missing, timed-out, or unsupported assessments cannot clear drift. Original `findings.json`, deterministic reports, structured assessments, validation results, effective findings, and the Markdown summary are uploaded together. The summary appears in the job summary and PR comment or scheduled issue. Original counts in the PR comment describe the deterministic result; the assessment section identifies the findings cleared by AI.

PR assessments run only for same-repository PRs or manual dispatches. Fork PRs retain the original gate. Scheduled assessments run when eligible findings exist; no additional AI invocation is made when there are none. Build, manifest, and test failures remain independent workflow failures regardless of an AI verdict.
