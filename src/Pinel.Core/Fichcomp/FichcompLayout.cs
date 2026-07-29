namespace Pinel.Core.Fichcomp;

/// <summary>Variante de fichier complémentaire prise en charge.</summary>
public enum FichcompKind
{
    /// <summary>Médicaments en sus, code UCD, quantité au millième.</summary>
    Medicament,

    /// <summary>Dispositifs médicaux implantables, code LPP, quantité entière.</summary>
    DispositifMedical,
}

/// <summary>
/// Positions du fichier complémentaire, reprises de la moulinette utilisée au
/// DIM pour les fichiers FICHCOMP, et vérifiées par les contrôles de longueur.
/// </summary>
/// <remarks>
/// <para>Médicaments, longueur totale 53 :</para>
/// <list type="table">
///   <item><term>1 à 9</term><description>FINESS, complété à gauche par des zéros</description></item>
///   <item><term>10 à 29</term><description>numéro administratif de séjour, complété à droite par des espaces</description></item>
///   <item><term>30 à 38</term><description>code UCD, complété à gauche par des zéros</description></item>
///   <item><term>39 à 45</term><description>quantité, valeur multipliée par 1000</description></item>
///   <item><term>46 à 53</term><description>date au format JJMMAAAA, ou huit espaces</description></item>
/// </list>
/// <para>Dispositifs médicaux, longueur totale 50 : identique, sauf la
/// quantité sur 4 caractères en positions 39 à 42 et la date en 43 à 50.</para>
/// </remarks>
public sealed record FichcompLayout(
    FichcompKind Kind,
    int TotalLength,
    int FinessLength,
    int StayLength,
    int CodeLength,
    int QuantityLength,
    int QuantityDecimals)
{
    public static FichcompLayout Medicament { get; } = new(
        FichcompKind.Medicament,
        TotalLength: 53,
        FinessLength: 9,
        StayLength: 20,
        CodeLength: 9,
        QuantityLength: 7,
        QuantityDecimals: 3);

    public static FichcompLayout DispositifMedical { get; } = new(
        FichcompKind.DispositifMedical,
        TotalLength: 50,
        FinessLength: 9,
        StayLength: 20,
        CodeLength: 9,
        QuantityLength: 4,
        QuantityDecimals: 0);

    public static FichcompLayout For(FichcompKind kind) =>
        kind == FichcompKind.Medicament ? Medicament : DispositifMedical;

    public int FinessStart => 0;
    public int StayStart => FinessLength;
    public int CodeStart => StayStart + StayLength;
    public int QuantityStart => CodeStart + CodeLength;
    public int DateStart => QuantityStart + QuantityLength;
    public int DateLength => 8;

    /// <summary>Libellé court, utilisé dans l'interface et les rapports.</summary>
    public string Label => Kind == FichcompKind.Medicament
        ? "FICHCOMP médicaments"
        : "FICHCOMP dispositifs médicaux";
}

/// <summary>Une ligne de fichier complémentaire, sous forme exploitable.</summary>
public sealed record FichcompRecord(
    string Finess,
    string StayNumber,
    string Code,
    decimal Quantity,
    DateOnly? Date);
