using Pinel.Core.Anonymization;

namespace Pinel.Core.Formats;

/// <summary>Un champ situe dans un bloc repete, en position relative au bloc (0 = premier caractere).</summary>
public sealed record BlockField(int Offset, int Length, FieldRole Role);

/// <summary>
/// Zone repetee : un compteur de la partie fixe annonce le nombre de blocs de
/// longueur fixe qui suivent. Les zones se suivent dans l'ordre declare.
/// </summary>
public sealed record RepeatZone(string Name, int CountStart, int CountLength, int BlockLength, IReadOnlyList<BlockField> Fields)
{
    /// <summary>Nombre de blocs annonce par la ligne, nul si le compteur est illisible.</summary>
    public int Count(string line)
    {
        int offset = CountStart - 1;
        if (line.Length < offset + CountLength) return 0;
        return int.TryParse(line.AsSpan(offset, CountLength).Trim(), out var n) && n >= 0 ? n : 0;
    }
}

/// <summary>
/// Structure complete d'un enregistrement : partie fixe (le descriptif) et
/// zones repetees. C'est elle qui donne la longueur attendue ligne par ligne.
/// </summary>
/// <remarks>
/// <para>
/// Les classeurs ATIH decrivent la partie fixe en positions absolues, puis les
/// zones repetees en prose (« 154 + 8*nDA + 23*nZA »). Les trois formats a
/// zone du RIM-P sont declares ici, verifies sur les descriptifs 2026 et sur
/// les longueurs de ligne reellement produites par le DIM :
/// </para>
/// <list type="bullet">
///   <item>RPS : 154 + 8 x nDA (positions 151-152) + 23 x nZA (153-154), chaque
///     acte CCAM commencant par sa date de realisation ;</item>
///   <item>RAA : 96 + 8 x nDA (positions 95-96) ;</item>
///   <item>VID-HOSP : 470 + 50 x N (positions 467-470). Le classeur decrit un
///     seul bloc et annonce 520 caracteres, mais une ligne sans discipline de
///     prestation fait 470 caracteres et une ligne a trois disciplines en fait
///     620 : ce ne sont pas des erreurs.</item>
/// </list>
/// </remarks>
public sealed class RecordSpec
{
    public RecordSpec(FormatLayout layout, int fixedLength, IReadOnlyList<RepeatZone> zones)
    {
        Layout = layout;
        FixedLength = fixedLength;
        Zones = zones;
        FixedFields = layout.Fields.Where(f => f.End <= fixedLength).ToList();
        // Classement calcule une fois par format : le refaire a chaque ligne
        // coutait des millions de normalisations Unicode sur un lot mensuel.
        FixedRoles = FixedFields.Select(FieldClassifier.Classify).ToList();
    }

    /// <summary>Role de chaque champ de <see cref="FixedFields"/>, au meme rang.</summary>
    public IReadOnlyList<FieldRole> FixedRoles { get; }

    public FormatLayout Layout { get; }
    public string Format => Layout.Format;
    public int FixedLength { get; }
    public IReadOnlyList<RepeatZone> Zones { get; }
    public IReadOnlyList<FormatField> FixedFields { get; }

    /// <summary>Longueur attendue d'une ligne, d'apres ses propres compteurs.</summary>
    public int ExpectedLength(string line) =>
        FixedLength + Zones.Sum(z => z.Count(line) * z.BlockLength);

    /// <summary>
    /// Construit la structure d'un descriptif : zones connues pour RPS, RAA et
    /// VID-HOSP, partie fixe seule pour les autres.
    /// </summary>
    public static RecordSpec For(FormatLayout layout)
    {
        var diagnostic = new[] { new BlockField(0, 8, FieldRole.Keep) };
        switch (layout.Format.ToUpperInvariant())
        {
            case "RPS":
                return new RecordSpec(layout, 154, new[]
                {
                    new RepeatZone("DA", 151, 2, 8, diagnostic),
                    new RepeatZone("ZA", 153, 2, 23, new[]
                    {
                        new BlockField(0, 8, FieldRole.Date),
                        new BlockField(8, 15, FieldRole.Keep),
                    }),
                });
            case "RAA":
                return new RecordSpec(layout, 96, new[] { new RepeatZone("DA", 95, 2, 8, diagnostic) });
            case "VID-HOSP":
                const int blockStart = 471;
                var template = layout.Fields
                    .Where(f => f.Start >= blockStart)
                    .Select(f => new BlockField(f.Start - blockStart, f.Length, FieldClassifier.Classify(f)))
                    .ToList();
                return new RecordSpec(layout, blockStart - 1, new[] { new RepeatZone("DMT", 467, 4, 50, template) });
            default:
                return new RecordSpec(layout, layout.ExpectedLength, Array.Empty<RepeatZone>());
        }
    }
}
