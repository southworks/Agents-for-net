# Streaming source fixture

`streaming-source-patch.json` is a source-only test of the behavior reviewer. It pins the official `Microsoft.Teams.Apps` 2.1.2 source commit and changes three outbound streaming values: informative updates use `streaming`, final messages use `streaming`, and emitted stream sequence numbers increase by one. Other source files remain at the pinned revision. This fixture does not represent a published Teams release or a compiled package.

From the repository root, prepare an ignored local snapshot:

```sh
mkdir -p artifacts/behavior-source-fixture
dotnet run --project scripts/TeamsApiDrift -c Release -- prepare-behavior-review \
  --repository-root . \
  --workspace artifacts/behavior-source-fixture/snapshot \
  --catalog scripts/TeamsApiDrift/behaviors.json \
  --to 2.1.2-local-streaming-source \
  --source-patch scripts/TeamsApiDriftFixtures/streaming-source-patch.json \
  --output artifacts/behavior-source-fixture/behavior-input.json
```

Preparation verifies the official package-to-commit mapping and that every patch context occurs exactly once. The resulting `behavior-input.json` identifies the local fixture and hashes the patch and complete source snapshot. Run the existing behavior review and validator against that snapshot to assess findings.

For a manual scheduled-workflow run, select **behavior_source_fixture** and leave both version inputs blank. The workflow resolves the pinned 2.1.2 source, runs only the behavior job, and publishes the report as an artifact without updating the standing issue. The removed `2.1.3-streamfixture.20261026.1` version is not a package and must not be entered as `to_version`. Normal scheduled runs and manual runs without the checkbox still compare released package versions.
