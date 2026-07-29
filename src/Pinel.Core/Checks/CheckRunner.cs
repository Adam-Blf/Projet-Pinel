using System.Collections.Concurrent;
using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Orchestrates a set of <see cref="IFileCheck"/>s over one or more
/// ATIH files. Runs validators in parallel, aggregates findings, and
/// returns a flat list sorted by severity then file.
/// </summary>
public sealed class CheckRunner
{
    private readonly IReadOnlyList<IFileCheck> _validators;
    private readonly IReadOnlyList<ICrossFileCheck> _crossValidators;

    public CheckRunner(
        IEnumerable<IFileCheck>? validators = null,
        IEnumerable<ICrossFileCheck>? crossValidators = null)
    {
        _validators = (validators ?? Default()).ToList();
        _crossValidators = (crossValidators ?? DefaultCross()).ToList();
    }

    /// <summary>
    /// Contrôles retenus : ceux qui fiabilisent la sortie de la moulinette et
    /// l'identitovigilance. Les contrôles de conformité DRUIDES, hors périmètre
    /// du cahier des charges, ne font pas partie de cette version.
    /// </summary>
    public static IEnumerable<IFileCheck> Default() => new IFileCheck[]
    {
        new FinessCheck(),
        new BirthDateFormatCheck(),
        new AnonymizationCheck(),
        new ChainageNirCheck(),
        new DuplicateLineCheck(),
        new FileYearCheck(),
    };

    /// <summary>Contrôles entre fichiers : chaînage de l'activité avec VID-HOSP.</summary>
    public static IEnumerable<ICrossFileCheck> DefaultCross() => new ICrossFileCheck[]
    {
        new ChainageCoverageCheck(),
    };

    public IReadOnlyList<CheckFinding> Run(IEnumerable<(string Path, string Format)> files)
    {
        var fileList = files.ToList();
        var bag = new ConcurrentBag<CheckFinding>();
        Parallel.ForEach(fileList, file =>
        {
            foreach (var validator in _validators)
            {
                if (validator.AppliesTo is not null && !validator.AppliesTo.Contains(file.Format)) continue;
                foreach (var finding in validator.Validate(file.Path, file.Format))
                {
                    bag.Add(finding);
                }
            }
        });

        // Cross-file validators run sequentially: they already correlate multiple files.
        foreach (var cross in _crossValidators)
        {
            foreach (var finding in cross.Validate(fileList))
            {
                bag.Add(finding);
            }
        }

        return bag
            .OrderByDescending(f => (int)f.Severity)
            .ThenBy(f => f.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.LineNumber)
            .ToList();
    }

    /// <summary>
    /// Summarizes findings by severity for dashboard display.
    /// </summary>
    public static Dictionary<CheckSeverity, int> Summarize(IEnumerable<CheckFinding> findings)
    {
        var dict = Enum.GetValues<CheckSeverity>().ToDictionary(s => s, _ => 0);
        foreach (var f in findings) dict[f.Severity]++;
        return dict;
    }
}
