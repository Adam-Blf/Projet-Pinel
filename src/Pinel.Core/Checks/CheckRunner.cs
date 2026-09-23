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

        // Fichiers dont la longueur de ligne ne correspond à aucune longueur
        // déclarée pour leur format. Ils sont écartés des contrôles qui lisent
        // un champ à sa position, et des contrôles croisés : un fichier de
        // chaînage dont la structure n'est pas celle qu'on croit ferait
        // apparaître tous les patients comme non chaînés. Voir LineLengthGate.
        var suspects = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        Parallel.ForEach(fileList, file =>
        {
            var verdict = LineLengthGate.Inspect(file.Path, file.Format);
            if (!verdict.Accepted)
            {
                suspects[file.Path] = 0;
                if (verdict.Finding is not null) bag.Add(verdict.Finding);
            }

            foreach (var validator in _validators)
            {
                if (validator.AppliesTo is not null && !validator.AppliesTo.Contains(file.Format)) continue;
                if (!verdict.Accepted && validator.ReadsFieldPositions) continue;
                foreach (var finding in validator.Validate(file.Path, file.Format))
                {
                    bag.Add(finding);
                }
            }
        });

        // Cross-file validators run sequentially: they already correlate multiple files.
        var croisables = fileList.Where(f => !suspects.ContainsKey(f.Path)).ToList();
        foreach (var cross in _crossValidators)
        {
            foreach (var finding in cross.Validate(croisables))
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
