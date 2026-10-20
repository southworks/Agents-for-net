using System.Globalization;

namespace Microsoft.Agents.TeamsApiDrift;

internal static class StepLog
{
    // Keep human-readable summaries off stdout: workflows capture versions and counts there.
    public static void Write(string title, params string[] details)
    {
        Console.Error.WriteLine($"Summary: {title}");
        foreach (var detail in details) Console.Error.WriteLine($"  {detail}");
    }

    public static void Findings(FindingsResult result, string title, string output)
    {
        Write(title,
            $"{result.Package}: {result.FromVersion} -> {result.ToVersion}",
            $"Total findings: {result.Findings.Count}",
            $"Blocking: {result.Summary.Blocking} | Required: {result.Summary.Required} | Review: {result.Summary.Review} | No action: {result.Summary.NoAction}");
        Console.Error.WriteLine("  Findings by API change type:");
        Console.Error.WriteLine($"  {"Type",-32} {"Total",6} {"Blocking",9} {"Required",9} {"Review",7} {"No action",10}");
        foreach (var group in result.Findings.GroupBy(finding => finding.Kind).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {group.Key,-32} {group.Count(),6} {group.Count(f => f.Classification == "blocking"),9} {group.Count(f => f.Classification == "required"),9} {group.Count(f => f.Classification == "review"),7} {group.Count(f => f.Classification == "no-action"),10}"));
        }
        if (result.Findings.Count == 0) Console.Error.WriteLine("  No API change types detected.");
        Counts("Review categories", result.Findings.Select(finding => finding.Category ?? "uncategorized"));
        foreach (var finding in result.Findings.Where(finding => finding.Classification is "blocking" or "required"))
            Console.Error.WriteLine($"  {finding.Id}: {finding.Classification} | {finding.Kind} | {finding.UpstreamSymbol}{(finding.Member is null ? string.Empty : "." + finding.Member)}");
        Console.Error.WriteLine($"  Findings requiring policy action: {result.Summary.Blocking + result.Summary.Required}");
        Console.Error.WriteLine($"  Findings artifact: {output}");
    }

    public static void Counts(string label, IEnumerable<string> values)
    {
        var groups = values.GroupBy(value => value).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        Console.Error.WriteLine($"  {label}: {(groups.Length == 0 ? "none" : string.Join(" | ", groups.Select(group => $"{group.Key}: {group.Count()}")))}");
    }
}
