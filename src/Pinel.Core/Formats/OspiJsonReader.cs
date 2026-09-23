using System.Text.Json;

namespace Pinel.Core.Formats;

/// <summary>Décompte des lignes extraites d'un export OSPI, par format.</summary>
public sealed record OspiExtraction(
    string SourceFile,
    int Records,
    IReadOnlyDictionary<string, int> LinesByFormat)
{
    public int TotalLines => LinesByFormat.Values.Sum();
}

/// <summary>
/// Lit un export JSON de la plateforme OSPI et en ressort les lignes ATIH
/// qu'il transporte, une par format.
/// </summary>
/// <remarks>
/// <para>
/// Relevé le 23/09/2026 sur les exports réels du GHT. Un export OSPI n'est pas
/// un format ATIH de plus : c'est un GROUPEMENT déjà fait, où chaque
/// enregistrement rassemble un patient ou un séjour avec toutes ses lignes
/// ATIH et sa clé de chaînage. Deux fichiers par mois, l'un pour l'ambulatoire,
/// l'autre pour les séjours.
/// </para>
/// <para>
/// Les lignes embarquées sont les lignes ATIH telles quelles, zones répétées
/// comprises : les RAA y font 96, 104 ou 112 caractères selon le nombre de
/// diagnostics associés, les RPS 154, 162 ou 177. Elles sont donc directement
/// exploitables par le reste de Pinel, sans transformation.
/// </para>
/// <para>
/// L'intérêt pratique est double. D'abord un DIM qui dispose de ces exports
/// peut alimenter Pinel sans repasser par les fichiers à plat. Ensuite le
/// chaînage y est DÉJÀ établi, identifiant du patient en regard de sa clé
/// anonyme, ce que les contrôles passent leur temps à reconstituer.
/// </para>
/// </remarks>
public static class OspiJsonReader
{
    /// <summary>
    /// Champ de l'export, et format ATIH des lignes qu'il porte. Les noms sont
    /// ceux des exports réels, au pluriel et sans accent.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> LineFields =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["raas"] = "RAA",
            ["rpss"] = "RPS",
            ["iums"] = "FICUM-PSY",
            ["tpspartiels"] = "FICHCOMP-TP",
            ["isolconts"] = "FICHCOMP-ISO",
            ["transps"] = "FICHCOMP",
        };

    /// <summary>
    /// Champs qui portent une clé de chaînage, une seule valeur par
    /// enregistrement. Ce sont des lignes d'ANO-HOSP.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ChainFields =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["anoipp"] = "ANO-HOSP",
            ["anohosp"] = "ANO-HOSP",
        };

    /// <summary>
    /// Formats dont chaque ligne décrit une STRUCTURE et non une activité.
    /// </summary>
    /// <remarks>
    /// L'export attache la description de l'unité médicale à chacun de ses
    /// patients : la même ligne revient autant de fois qu'il y a eu de venues.
    /// Sur un mois réel, cela faisait 21 563 lignes d'unités médicales pour 108
    /// unités distinctes, et les contrôles de doublons signalaient à juste titre
    /// un biais d'activité. Ces lignes-là ne sont donc écrites qu'une fois.
    /// Les lignes d'activité, elles, sont toutes conservées : deux entretiens
    /// identiques le même jour sont deux actes réels.
    /// </remarks>
    public static readonly IReadOnlySet<string> DeduplicatedFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FICUM-PSY" };

    /// <summary>
    /// Reconnaît un export OSPI sans le charger : un tableau JSON dont le
    /// premier objet porte au moins un champ de lignes connu.
    /// </summary>
    public static bool LooksLikeOspi(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                // On ne lit que le début : inutile de matérialiser 45 Mo.
                MaxDepth = 8,
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            foreach (var first in doc.RootElement.EnumerateArray())
            {
                if (first.ValueKind != JsonValueKind.Object) return false;
                return first.EnumerateObject().Any(p =>
                    LineFields.ContainsKey(p.Name) || ChainFields.ContainsKey(p.Name));
            }
            return false;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Écrit dans <paramref name="outputFolder"/> un fichier à plat par format
    /// rencontré, et rend le décompte. Les lignes sont recopiées telles quelles,
    /// dans l'encodage des fichiers PMSI.
    /// </summary>
    public static OspiExtraction Extract(string path, string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);

        var prefix = Path.GetFileNameWithoutExtension(path);
        var writers = new Dictionary<string, StreamWriter>(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var records = 0;

        StreamWriter WriterFor(string format)
        {
            if (writers.TryGetValue(format, out var existing)) return existing;
            var file = Path.Combine(outputFolder, $"{prefix}.{format}.txt");
            var created = new StreamWriter(file, append: false, PmsiEncoding.Latin1);
            writers[format] = created;
            return created;
        }

        // Lignes déjà écrites, pour les seuls formats de structure.
        var alreadyWritten = new HashSet<string>(StringComparer.Ordinal);

        void Emit(string format, string line)
        {
            if (line.Length == 0) return;
            if (DeduplicatedFormats.Contains(format) && !alreadyWritten.Add(format + '\u0000' + line))
            {
                return;
            }
            WriterFor(format).WriteLine(line);
            counts[format] = counts.GetValueOrDefault(format) + 1;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Un export OSPI est un tableau JSON.");
            }

            foreach (var record in doc.RootElement.EnumerateArray())
            {
                if (record.ValueKind != JsonValueKind.Object) continue;
                records++;

                foreach (var property in record.EnumerateObject())
                {
                    if (LineFields.TryGetValue(property.Name, out var listFormat)
                        && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                Emit(listFormat, item.GetString() ?? string.Empty);
                            }
                        }
                    }
                    else if (ChainFields.TryGetValue(property.Name, out var chainFormat)
                             && property.Value.ValueKind == JsonValueKind.String)
                    {
                        Emit(chainFormat, property.Value.GetString() ?? string.Empty);
                    }
                }
            }
        }
        finally
        {
            foreach (var writer in writers.Values) writer.Dispose();
        }

        return new OspiExtraction(path, records, counts);
    }
}
