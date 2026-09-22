using System.Globalization;
using Microsoft.ML.Data;
using Pinel.Core.Anonymization;
using Pinel.Core.Formats;

namespace Pinel.Ml;

/// <summary>
/// Une ligne RAA vue par le modele : des codes et des decomptes, jamais un
/// identifiant ni une date. L'IPP et la date de l'acte servent a calculer les
/// variables de repetition, puis ne sont pas conserves.
/// </summary>
public sealed class RaaExample
{
    public string Sexe { get; set; } = "";
    public string Forme { get; set; } = "";
    public string Um { get; set; } = "";
    public string Secteur { get; set; } = "";
    public string ModeLegal { get; set; } = "";
    public string Nature { get; set; } = "";
    public string Lieu { get; set; } = "";
    public string Modalite { get; set; } = "";
    public string Categorie { get; set; } = "";
    public string Liberal { get; set; } = "";
    public string ChapitreDp { get; set; } = "";
    public string FinessGeo { get; set; } = "";

    public float Age { get; set; }
    public float JourSemaine { get; set; }
    public float Intervenants { get; set; }
    public float DiagnosticsAssocies { get; set; }
    public float ActesDuJour { get; set; }
    public float RangIdentique { get; set; }
    public float RangNatureDuJour { get; set; }

    /// <summary>Vrai si le DIM a supprime la ligne. Absent a la prediction.</summary>
    [ColumnName("Label")]
    public bool Supprimee { get; set; }

    public static readonly string[] Categorical =
    {
        nameof(Sexe), nameof(Forme), nameof(Um), nameof(Secteur), nameof(ModeLegal), nameof(Nature), nameof(Lieu),
        nameof(Modalite), nameof(Categorie), nameof(Liberal), nameof(ChapitreDp), nameof(FinessGeo),
    };

    public static readonly string[] Numeric =
    {
        nameof(Age), nameof(JourSemaine), nameof(Intervenants), nameof(DiagnosticsAssocies),
        nameof(ActesDuJour), nameof(RangIdentique), nameof(RangNatureDuJour),
    };
}

/// <summary>Score d'une ligne.</summary>
public sealed class RaaPrediction
{
    [ColumnName("Probability")]
    public float Probabilite { get; set; }
}

/// <summary>
/// Transforme les lignes d'un fichier RAA en exemples. Les champs sont
/// retrouves par leur libelle officiel dans le descriptif, pas par une position
/// ecrite en dur : un changement de format ATIH ne demande qu'un nouveau
/// descriptif.
/// </summary>
public sealed class RaaFeatureBuilder
{
    private readonly Func<string, string> _ipp, _ddn, _date, _sexe, _forme, _um, _secteur, _legal, _nature,
        _lieu, _modalite, _categorie, _intervenants, _liberal, _dp, _nda, _finessGeo;

    public RaaFeatureBuilder(RecordSpec raa)
    {
        FormatField Find(params string[] words)
        {
            foreach (var field in raa.FixedFields)
            {
                var label = AtihWorkbookImporter.Normalize(field.Label);
                if (words.All(w => label.Contains(w, StringComparison.Ordinal))) return field;
            }
            throw new InvalidOperationException($"Champ RAA introuvable dans le descriptif : {string.Join(' ', words)}");
        }
        Func<string, string> Reader(params string[] words)
        {
            var field = Find(words);
            return line => field.Read(line);
        }

        _ipp = Reader("IDENTIFICATION PERMANENT");
        _ddn = Reader("DATE DE NAISSANCE");
        _date = Reader("DATE DE L", "ACTE");
        _sexe = Reader("SEXE");
        _forme = Reader("FORME D", "ACTIVITE");
        _um = Reader("UNITE MEDICALE");
        _secteur = Reader("SECTEUR");
        _legal = Reader("MODE LEGAL");
        _nature = Reader("NATURE DE L", "ACTE");
        _lieu = Reader("LIEU DE L", "ACTE");
        _modalite = Reader("MODALITE DE REALISATION");
        _categorie = Reader("CATEGORIE PROFESSIONNELLE");
        _intervenants = Reader("NOMBRE D", "INTERVENANTS");
        _liberal = Reader("LIBERAL");
        _dp = Reader("DIAGNOSTIC PRINCIPAL");
        _nda = Reader("NOMBRE DE DIAGNOSTICS");
        _finessGeo = Reader("FINESS GEOGRAPHIQUE");
    }

    public List<RaaExample> Build(IReadOnlyList<string> lines)
    {
        var perDay = new Dictionary<(string, string), int>();
        foreach (var line in lines)
        {
            var key = (_ipp(line), _date(line));
            perDay[key] = perDay.GetValueOrDefault(key) + 1;
        }

        var identical = new Dictionary<string, int>(StringComparer.Ordinal);
        var sameNature = new Dictionary<(string, string, string), int>();
        var examples = new List<RaaExample>(lines.Count);
        foreach (var line in lines)
        {
            var ipp = _ipp(line);
            var date = _date(line);
            identical[line] = identical.GetValueOrDefault(line) + 1;
            var natureKey = (ipp, date, _nature(line));
            sameNature[natureKey] = sameNature.GetValueOrDefault(natureKey) + 1;

            bool dated = DateTime.TryParseExact(date, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var act);
            var ddn = _ddn(line);
            int birthYear = ddn.Length == 8 && int.TryParse(ddn[4..], out var y) ? y : 0;

            examples.Add(new RaaExample
            {
                Sexe = _sexe(line),
                Forme = _forme(line),
                Um = _um(line),
                Secteur = _secteur(line),
                ModeLegal = _legal(line),
                Nature = _nature(line),
                Lieu = _lieu(line),
                Modalite = _modalite(line),
                Categorie = _categorie(line),
                Liberal = _liberal(line),
                ChapitreDp = _dp(line).Length >= 3 ? _dp(line)[..3] : _dp(line),
                FinessGeo = _finessGeo(line),
                Age = dated && birthYear > 1900 ? act.Year - birthYear : -1,
                JourSemaine = dated ? (float)act.DayOfWeek : -1,
                Intervenants = float.TryParse(_intervenants(line), out var n) ? n : -1,
                DiagnosticsAssocies = float.TryParse(_nda(line), out var d) ? d : 0,
                ActesDuJour = perDay[(ipp, date)],
                RangIdentique = identical[line],
                RangNatureDuJour = sameNature[natureKey],
            });
        }
        return examples;
    }

    /// <summary>
    /// Etiquette chaque ligne d'origine : supprimee si le fichier corrige ne
    /// garde plus assez de lignes du meme patient, du meme jour et de la meme
    /// nature d'acte.
    /// </summary>
    public void Label(List<RaaExample> examples, IReadOnlyList<string> original, IReadOnlyList<string> corrected)
    {
        var left = new Dictionary<(string, string, string), int>();
        foreach (var line in corrected)
        {
            var key = (_ipp(line), _date(line), _nature(line));
            left[key] = left.GetValueOrDefault(key) + 1;
        }
        for (int i = 0; i < original.Count; i++)
        {
            var key = (_ipp(original[i]), _date(original[i]), _nature(original[i]));
            int remaining = left.GetValueOrDefault(key);
            examples[i].Supprimee = remaining == 0;
            if (remaining > 0) left[key] = remaining - 1;
        }
    }
}
