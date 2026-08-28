# Temporary Teams API drift fixture

This directory is a temporary local NuGet source for manually testing the scheduled Teams API drift workflow.

Place the following generated package here and commit it with the workflow test branch:

```text
Microsoft.Teams.Apps.2.1.0-driftfixture.20260827.1.nupkg
```

The workflow uses this package only for `workflow_dispatch` runs. Weekly scheduled runs continue comparing the repository baseline with the latest stable release from NuGet.org.

Remove this directory, the matching `.gitignore` exception, and every `TEMPORARY LOCAL DRIFT FIXTURE` block in `.github/workflows/teams-api-drift-scheduled.yml` after testing is complete.
