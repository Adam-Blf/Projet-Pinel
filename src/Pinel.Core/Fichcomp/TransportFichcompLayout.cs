using System.Globalization;
using System.Text;

namespace Pinel.Core.Fichcomp;

/// <summary>
/// Constantes d'établissement injectées dans la feuille Fichcomp transports.
/// Un autre établissement du GHT a d'autres FINESS : ces valeurs ne doivent
/// jamais être écrites en dur dans la logique de construction.
/// </summary>
public sealed record TransportFichcompOptions(
    string FinessEPmsi,
    string TypeDePrestation,
    string FinessGeographique,
    string CodeForfait,
    string ClasseDistance)
{
    /// <summary>Valeurs par défaut relevées sur le classeur transports de la Fondation Vallée.</summary>
    public static TransportFichcompOptions Default { get; } = new(
        FinessEPmsi: "940140049",
        TypeDePrestation: "17",
        FinessGeographique: "940000631",
        CodeForfait: "ST2",
        ClasseDistance: "06");
}

/// <summary>
/// Positions et intitulés de la feuille Fichcomp transports, et composition de
/// sa colonne M (la ligne FICHCOMP à largeur fixe).
/// </summary>
/// <remarks>
/// <para>
/// La formule Excel d'origine, relevée dans le classeur cible, était cassée de
/// trois façons que cette classe corrige explicitement : elle visait une ligne
/// 196 rangs plus bas que la ligne courante, elle lisait la date de transport
/// dans la colonne E (étiquetée "IPP", laissée vide par construction) au lieu
/// de la colonne J ("Date transp. Aller"), et elle concaténait le libellé
/// d'UF, de longueur variable, sans le compléter à une largeur fixe, ce qui
/// décalait tous les champs suivants.
/// </para>
/// <para>
/// Composition retenue pour la colonne M : UF, puis libellé d'UF, puis la
/// colonne C complétée à 20 caractères, puis la date de naissance complétée à
/// 9 caractères, puis la date de transport au format JJMMAAAA, puis le
/// Finess e-PMSI, puis le type de prestation, puis 10 espaces. Toutes les
/// complétions se font à droite par des espaces.
/// </para>
/// </remarks>
public static class TransportFichcompLayout
{
    /// <summary>
    /// Largeur du code UF dans la ligne FICHCOMP composée. A VALIDER PAR LE
    /// DIM : la formule d'origine ne complétait pas ce champ, sa largeur
    /// réelle n'est donc attestée par aucune source.
    /// </summary>
    public const int UfWidth = 4;

    /// <summary>
    /// Largeur du libellé d'UF dans la ligne FICHCOMP composée. A VALIDER PAR
    /// LE DIM, pour la même raison que <see cref="UfWidth"/>.
    /// </summary>
    public const int LibelleWidth = 30;

    /// <summary>Largeur de la colonne C (colonne F du fichcomp rapport 1), relevée dans le classeur cible.</summary>
    public const int ColumnCWidth = 20;

    /// <summary>Largeur de la date de naissance, relevée dans le classeur cible.</summary>
    public const int BirthDateWidth = 9;

    /// <summary>Largeur de la date de transport au format JJMMAAAA.</summary>
    public const int TransportDateWidth = 8;

    /// <summary>Nombre d'espaces qui terminent la ligne, relevé dans le classeur cible.</summary>
    public const int TrailingSpaces = 10;

    /// <summary>Numéro de la première ligne de données dans "Rapport 1 modifié" (Fichcomp ligne 2 correspond à cette ligne, puis un pour un).</summary>
    public const int SourceFirstDataRow = 4;

    /// <summary>Intitulés des colonnes A à S de la feuille Fichcomp, dans l'ordre.</summary>
    public static readonly IReadOnlyList<string> ColumnHeaders = new[]
    {
        "UF",
        "Libellé - Uf",
        "Colonne F du fichcomp rapport 1",
        "Date de naissance",
        "IPP",
        "Finess e-PMSI",
        "Type de prestation",
        "NDA",
        "Numéro FINESS géographique",
        "Date transp. Aller",
        "Code forfait",
        "Classe de distance",
        "FICHCOMP",
        "Nombre de kilomètres",
        "Commentaire",
        "Nom fournisseur",
        "Adresse fournisseur ligne 1",
        "Code postal fournisseur",
        "Ville fournisseur",
    };

    /// <summary>
    /// Construit la ligne FICHCOMP à largeur fixe de la colonne M pour une
    /// ligne de transport. Une valeur qui dépasse la largeur de son champ
    /// lève une <see cref="FichcompOverflowException"/> plutôt que d'être
    /// tronquée en silence.
    /// </summary>
    public static string ComposeLine(
        string uf,
        string libelleUf,
        string columnC,
        string birthDate,
        DateOnly transportDate,
        TransportFichcompOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(FichcompConverter.Pad(uf, UfWidth, zeroPadLeft: false, field: "UF"));
        builder.Append(FichcompConverter.Pad(libelleUf, LibelleWidth, zeroPadLeft: false, field: "libellé d'UF"));
        builder.Append(FichcompConverter.Pad(columnC, ColumnCWidth, zeroPadLeft: false, field: "colonne F du fichcomp rapport 1"));
        builder.Append(FichcompConverter.Pad(birthDate, BirthDateWidth, zeroPadLeft: false, field: "date de naissance"));
        builder.Append(transportDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture));
        builder.Append(options.FinessEPmsi.Trim());
        builder.Append(options.TypeDePrestation.Trim());
        builder.Append(new string(' ', TrailingSpaces));
        return builder.ToString();
    }
}
