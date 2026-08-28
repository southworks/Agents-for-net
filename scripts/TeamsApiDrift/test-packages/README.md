# Temporary Teams API drift fixture

This directory is a temporary local NuGet source for manually testing the scheduled and PR Teams API drift workflows.

Place the following generated package here and commit it with the workflow test branch:

```text
Microsoft.Teams.Apps.2.1.0-driftfixture.20260827.1.nupkg
```

The scheduled workflow uses this package only for `workflow_dispatch` runs. The PR workflow uses it when its candidate version is the fixture version. Weekly scheduled runs continue comparing the repository baseline with the latest stable release from NuGet.org.

Remove this directory, the matching `.gitignore` exception, and every `TEMPORARY LOCAL DRIFT FIXTURE` block in the Teams API drift workflows after testing is complete.
