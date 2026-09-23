using Pinel.Core.Formats;

namespace Pinel.Core.Checks;

/// <summary>
/// Refuse qu'un contrôle lise un champ à sa position quand la longueur des
/// lignes du fichier ne correspond à aucune longueur déclarée pour ce format.
/// </summary>
/// <remarks>
/// <para>
/// Motif, mesuré le 23/09/2026 sur douze ans de fichiers réels du GHT
/// (lot OSPI, 2014 à 2026). Les formats ATIH changent de longueur presque
/// chaque année : le VID-HOSP est passé par 314, 324, 369, 432, 452, 468, 518
/// puis 520 caractères, le RPS par 152 avant 154, le RAA par 92 puis 93 avant
/// 96. La matrice de Pinel ne porte que les longueurs courantes.
/// </para>
/// <para>
/// Or le format était établi sur le NOM du fichier, et les contrôles lisaient
/// ensuite leurs champs aux positions de l'année courante. Sur un seul dossier
/// de 2024, cela produisait 7 549 anomalies de chaînage et 12 anomalies de NIR,
/// toutes fausses : le fichier de chaînage n'avait simplement pas la structure
/// que le contrôle supposait.
/// </para>
/// <para>
/// La règle est donc la même que celle du registre de descriptifs : sans
/// position établie, on ne lit rien du contenu de la ligne, et on le dit. Une
/// anomalie explicite vaut mieux que sept mille fausses.
/// </para>
/// </remarks>
public static class LineLengthGate
{
    /// <summary>Code émis quand la longueur ne correspond à aucune longueur déclarée.</summary>
    public const string Code = "ERR-LONGUEUR-INATTENDUE";

    /// <summary>
    /// Lignes échantillonnées pour établir la longueur dominante. Un fichier
    /// PMSI est homogène : quelques milliers de lignes suffisent, et cela évite
    /// de parcourir un fichier de plusieurs dizaines de méga-octets deux fois.
    /// </summary>
    private const int SampleLines = 3_000;

    /// <summary>
    /// Part des lignes qui doivent porter la longueur dominante pour que le
    /// fichier soit considéré comme homogène. Même seuil que le détecteur de
    /// format par le contenu, pour que les deux ne se contredisent pas.
    /// </summary>
    private const double DominanceRatio = 0.8;

    public sealed record Verdict(bool Accepted, int ObservedLength, CheckFinding? Finding);

    /// <summary>
    /// Établit si les lignes de <paramref name="filePath"/> ont une longueur
    /// compatible avec <paramref name="formatName"/>.
    /// </summary>
    public static Verdict Inspect(string filePath, string formatName)
    {
        if (!AtihMatrix.All.TryGetValue(formatName, out var format))
        {
            // Format inconnu de la matrice : rien à comparer, on laisse passer.
            // Le contrôle lui-même décidera s'il s'applique.
            return new Verdict(true, 0, null);
        }

        int observed;
        try
        {
            observed = DominantLength(filePath);
        }
        catch (IOException)
        {
            // L'erreur de lecture est déjà signalée en Blocker par les contrôles
            // eux-mêmes ; ne pas la signaler deux fois.
            return new Verdict(true, 0, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new Verdict(true, 0, null);
        }

        if (observed == 0) return new Verdict(true, 0, null);

        var accepted = new List<int> { format.Length };
        if (AtihMatrix.Variants.TryGetValue(formatName, out var variants))
        {
            accepted.AddRange(variants.Select(v => v.Length));
        }

        if (accepted.Contains(observed)) return new Verdict(true, observed, null);

        var attendues = string.Join(" ou ", accepted.OrderBy(x => x));
        return new Verdict(false, observed, new CheckFinding(
            Code,
            CheckSeverity.Error,
            $"Lignes de {observed} caractères, alors que le format {formatName} en déclare "
            + $"{attendues}. Les contrôles qui lisent un champ à sa position n'ont pas été "
            + "appliqués : sans position établie pour ce millésime, tout ce qui en serait tiré "
            + "serait faux.",
            filePath,
            0,
            formatName,
            $"Déposer le descriptif officiel du millésime de ce fichier, ou vérifier que le "
            + $"format déduit du nom est bien {formatName}."));
    }

    /// <summary>
    /// Longueur portée par au moins <see cref="DominanceRatio"/> des lignes
    /// lues, ou 0 si le fichier est vide ou trop hétérogène pour conclure.
    /// </summary>
    private static int DominantLength(string filePath)
    {
        var counts = new Dictionary<int, int>();
        var total = 0;

        using var reader = new StreamReader(filePath, PmsiEncoding.Latin1);
        for (string? line = reader.ReadLine(); line is not null && total < SampleLines; line = reader.ReadLine())
        {
            if (line.Length == 0) continue;
            counts[line.Length] = counts.GetValueOrDefault(line.Length) + 1;
            total++;
        }

        if (total == 0) return 0;

        var (length, count) = counts.MaxBy(kv => kv.Value);
        return count >= total * DominanceRatio ? length : 0;
    }
}
