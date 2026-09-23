using Pinel.Core.Formats;

namespace Pinel.Core.Anonymization;

/// <summary>Bilan d'un fichier traite ou ecarte.</summary>
public sealed record AnonymizedFile(string RelativePath, string? Format, int Lines, int LinesAtExpectedLength, string Note);

/// <summary>Bilan d'une execution. Ne contient aucune valeur de champ.</summary>
public sealed record AnonymizationReport(
    IReadOnlyList<AnonymizedFile> Written,
    IReadOnlyList<AnonymizedFile> Excluded,
    int IdentifiersSeen,
    IReadOnlyDictionary<string, int> LeaksByField);

/// <summary>
/// Produit une copie pseudonymisee d'une arborescence de fichiers PMSI.
/// </summary>
/// <remarks>
/// <para>
/// Liste blanche au caractere pres. Chaque ligne de sortie part d'une copie
/// masquee de la ligne d'origine (chiffres en 9, lettres en X, espaces
/// gardes) : la structure reste lisible, le contenu non. Seuls les caracteres
/// couverts par un champ connu du descriptif sont ensuite reecrits, recopies
/// s'il s'agit d'un code (diagnostic, UM, mode legal) ou transformes s'il
/// s'agit d'un identifiant, d'une date ou d'un code postal. Un champ ajoute par
/// un editeur, un filler rempli, une fin de ligne en trop restent masques.
/// </para>
/// <para>
/// Un fichier dont le format n'est pas reconnu n'est pas copie du tout : listes
/// Excel, questionnaires e-Satis, sorties MAGIC et exports internes restent sur
/// le poste d'origine.
/// </para>
/// <para>
/// Controle final : toute valeur d'identifiant de 6 caracteres ou plus vue en
/// entree est recherchee dans les champs recopies tels quels. Une occurrence
/// signale un identifiant range dans un champ de code, et le rapport la compte
/// par champ, sans jamais l'afficher.
/// </para>
/// </remarks>
public sealed class PmsiAnonymizer
{
    private static readonly HashSet<string> SkippedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls", ".xlsx", ".xlsm", ".zip", ".pdf", ".json", ".csv", ".log", ".docx", ".doc", ".html", ".png", ".jpg",
    };

    private readonly Pseudonymizer _pseudo;
    private readonly ContentFormatDetector _detector;

    public PmsiAnonymizer(Pseudonymizer pseudonymizer, IReadOnlyList<RecordSpec> specs)
    {
        _pseudo = pseudonymizer;
        _detector = new ContentFormatDetector(specs);
    }

    public AnonymizationReport Run(string sourceRoot, string targetRoot)
    {
        var detected = new List<(string Path, RecordSpec Spec)>();
        var excluded = new List<AnonymizedFile>();
        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var relative = Path.GetRelativePath(sourceRoot, path);
            if (SkippedExtensions.Contains(Path.GetExtension(path)))
            {
                excluded.Add(new AnonymizedFile(relative, null, 0, 0, "type de fichier non traite"));
                continue;
            }
            var result = _detector.Detect(path);
            if (result.Spec is null) excluded.Add(new AnonymizedFile(relative, null, result.LinesRead, 0, result.Reason));
            else detected.Add((path, result.Spec));
        }

        var index = IdentityIndex.Build(detected);
        var leaks = new Dictionary<string, int>(StringComparer.Ordinal);
        var written = new List<AnonymizedFile>();

        foreach (var (path, spec) in detected)
        {
            var relative = Path.GetRelativePath(sourceRoot, path);
            var target = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            int lines = 0, conform = 0;
            using (var writer = new StreamWriter(target, append: false, PmsiEncoding.Latin1))
            {
                foreach (var line in File.ReadLines(path, PmsiEncoding.Latin1))
                {
                    lines++;
                    if (line.Length == spec.ExpectedLength(line)) conform++;
                    writer.WriteLine(Transform(line, spec, index, leaks));
                }
            }
            written.Add(new AnonymizedFile(relative, spec.Format, lines, conform, ""));
        }

        return new AnonymizationReport(written, excluded, index.Identifiers.Count, leaks);
    }

    internal string Transform(string line, RecordSpec spec, IdentityIndex index, IDictionary<string, int> leaks)
    {
        var output = Pseudonymizer.Mask(line).ToCharArray();
        int offset = _pseudo.DayOffset(index.PatientKey(line, spec));

        for (int i = 0; i < spec.FixedFields.Count; i++)
        {
            var field = spec.FixedFields[i];
            Apply(line, output, field.Offset, field.Length, spec.FixedRoles[i], offset, spec.Format + "." + field.Name, index, leaks);
        }

        int position = spec.FixedLength;
        foreach (var zone in spec.Zones)
        {
            int count = zone.Count(line);
            for (int i = 0; i < count && position < line.Length; i++, position += zone.BlockLength)
            {
                foreach (var f in zone.Fields)
                {
                    Apply(line, output, position + f.Offset, f.Length, f.Role, offset, spec.Format + "." + zone.Name, index, leaks);
                }
            }
        }
        return new string(output);
    }

    private void Apply(string line, char[] output, int start, int length, FieldRole role, int dayOffset,
        string fieldName, IdentityIndex index, IDictionary<string, int> leaks)
    {
        if (start >= line.Length) return;
        length = Math.Min(length, line.Length - start);
        var value = line.Substring(start, length);

        string replacement = role switch
        {
            FieldRole.Keep => value,
            FieldRole.PatientId => ReplaceSpan(value, v => _pseudo.Token("patient", v)),
            FieldRole.StayId => ReplaceSpan(value, v => _pseudo.Token("sejour", v)),
            FieldRole.Nir => ReplaceSpan(value, v => _pseudo.Token("nir", v)),
            FieldRole.ChainingKey => ReplaceSpan(value, v => _pseudo.Token("chainage", v)),
            FieldRole.NirKey => ReplaceSpan(value, v => new string('0', v.Length)),
            FieldRole.BirthDate => length == 8 ? _pseudo.BirthDate(value) : Pseudonymizer.Mask(value),
            FieldRole.Date => length == 8 ? Pseudonymizer.ShiftDate(value, dayOffset) : Pseudonymizer.Mask(value),
            FieldRole.PostalCode => Pseudonymizer.PostalCode(value),
            _ => new string(' ', length),
        };

        if (role == FieldRole.Keep && index.ContainsIdentifier(value))
        {
            leaks[fieldName] = leaks.TryGetValue(fieldName, out var n) ? n + 1 : 1;
            replacement = Pseudonymizer.Mask(value);
        }
        replacement.AsSpan(0, Math.Min(replacement.Length, length)).CopyTo(output.AsSpan(start));
    }

    /// <summary>Remplace la partie non blanche du champ, en gardant son cadrage.</summary>
    private static string ReplaceSpan(string value, Func<string, string> transform)
    {
        int first = 0, last = value.Length - 1;
        while (first <= last && value[first] == ' ') first++;
        while (last >= first && value[last] == ' ') last--;
        if (first > last) return value;
        var core = value.Substring(first, last - first + 1);
        return value[..first] + transform(core) + value[(last + 1)..];
    }
}
