namespace Pinel.Core.Learning;

/// <summary>Etat d'une regle dans le circuit de validation du DIM.</summary>
public enum RuleStatus
{
    /// <summary>Tiree des corrections, pas encore relue : proposee, jamais appliquee seule.</summary>
    Proposee,
    /// <summary>Relue et acceptee par le DIM : appliquee en suggestion sur les lots suivants.</summary>
    Validee,
    /// <summary>Relue et refusee : conservee pour ne plus etre reproposee.</summary>
    Rejetee,
}

/// <summary>
/// Correction reguliere du DIM, apprise en comparant des fichiers d'origine a
/// leur version corrigee : « dans le format F, le champ C passe de A a B »,
/// eventuellement sous condition « quand le champ K vaut V ».
/// </summary>
/// <remarks>
/// Une regle ne porte que des codes (formes d'activite, categories, UM,
/// motifs), jamais un identifiant ni une date : les champs de ce type sont
/// exclus de l'apprentissage. Le fichier des regles peut donc etre relu,
/// discute et versionne sans risque.
/// </remarks>
public sealed class LearnedRule
{
    public required string Format { get; init; }
    public required string Field { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public string? ConditionField { get; init; }
    public string? ConditionValue { get; init; }

    /// <summary>Lignes ou la correction a ete observee.</summary>
    public int Support { get; set; }

    /// <summary>Lignes qui remplissaient la condition et que le DIM n'a pas corrigees.</summary>
    public int Contradictions { get; set; }

    public RuleStatus Status { get; set; } = RuleStatus.Proposee;
    public DateOnly FirstSeen { get; set; }
    public DateOnly LastSeen { get; set; }

    /// <summary>Part des cas observes ou le DIM a bien applique la correction.</summary>
    public double Confidence => Support + Contradictions == 0 ? 0 : Support / (double)(Support + Contradictions);

    /// <summary>Identite de la regle, stable d'un apprentissage a l'autre.</summary>
    public string Key => $"{Format}|{Field}|{From}|{To}|{ConditionField}|{ConditionValue}";

    public string Describe() =>
        $"{Format} : {Field} « {Display(From)} » -> « {Display(To)} »" +
        (ConditionField is null ? "" : $" quand {ConditionField} = « {Display(ConditionValue ?? "")} »");

    private static string Display(string value) => value.Length == 0 ? "vide" : value;

    /// <summary>Vrai si la ligne remplit la condition et porte la valeur de depart.</summary>
    public bool Matches(Func<string, string?> read) =>
        read(Field) == From && (ConditionField is null || read(ConditionField) == ConditionValue);
}
