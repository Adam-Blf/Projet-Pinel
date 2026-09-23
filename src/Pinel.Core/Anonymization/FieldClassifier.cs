using Pinel.Core.Formats;

namespace Pinel.Core.Anonymization;

/// <summary>Traitement applique a un champ lors de la pseudonymisation.</summary>
public enum FieldRole
{
    /// <summary>Recopie a l'identique : codes, compteurs, UM, diagnostics.</summary>
    Keep,
    /// <summary>Identifiant patient (IPP) : remplace par un pseudonyme stable.</summary>
    PatientId,
    /// <summary>Numero de sejour, administratif, d'entree ou de facture.</summary>
    StayId,
    /// <summary>Numero d'immatriculation (NIR) de l'assure ou du beneficiaire.</summary>
    Nir,
    /// <summary>Cle de controle du NIR : remplacee par des zeros.</summary>
    NirKey,
    /// <summary>Cle de chainage deja hachee : hachee a nouveau.</summary>
    ChainingKey,
    /// <summary>Date de naissance : annee conservee, jour et mois tires.</summary>
    BirthDate,
    /// <summary>Date de soins : decalee d'un nombre de jours propre au patient.</summary>
    Date,
    /// <summary>Code postal : departement conserve, commune effacee.</summary>
    PostalCode,
    /// <summary>Nom, prenom, adresse, commune : efface.</summary>
    Erase,
}

/// <summary>
/// Classe les champs d'un descriptif selon leur libelle officiel.
/// </summary>
/// <remarks>
/// Le classement se fait sur le libelle ATIH, pas sur la position : il suit
/// donc les changements de format d'une annee sur l'autre sans intervention.
/// Un libelle ambigu penche toujours vers le traitement le plus protecteur.
/// </remarks>
public static class FieldClassifier
{
    public static FieldRole Classify(FormatField field)
    {
        // Majuscules sans accents, toute ponctuation ramenee a une espace :
        // "N° d'entrée" devient "N D ENTREE".
        var normalized = AtihWorkbookImporter.Normalize(field.Label.Length > 0 ? field.Label : field.Name);
        var words = new string(normalized.Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var label = string.Join(' ', words);
        bool Has(string word) => words.Contains(word);
        bool Contains(string part) => label.Contains(part, StringComparison.Ordinal);

        // Montants, motifs et indicateurs portent parfois le mot "sejour" :
        // ce ne sont pas des identifiants.
        if (Has("MONTANT") || Has("MOTIF") || Has("NATURE") || Has("FACTURABLE") || Has("TAUX"))
        {
            return FieldRole.Keep;
        }
        if (Contains("IMMATRICULATION") || Has("NIR") || Has("INS") || Contains("SECURITE SOCIALE")
            || Contains("IDENTIFIANT NATIONAL DE SANTE"))
        {
            return Has("CLE") ? FieldRole.NirKey : FieldRole.Nir;
        }
        // Numero d'accident du travail, d'organisme complementaire, de titre de
        // recette : identifiants d'un dossier, sans utilite pour les controles.
        if (Has("ACCIDENT") || Contains("ORGANISME COMPLEMENTAIRE") || Contains("TITRE DE RECETTE"))
        {
            return FieldRole.Erase;
        }
        if ((Has("RSS") || Has("RUM")) && (Has("N") || Has("NUMERO")) && !Contains("VERSION") && !Contains("FORMAT"))
        {
            return FieldRole.StayId;
        }
        if (Contains("CHAINAGE") && (Has("CLE") || Contains("CRYPT") || Contains("HACH")))
        {
            return FieldRole.ChainingKey;
        }
        if (Contains("IDENTIFICATION PERMANENT") || Has("IPP") || Contains("IDENTIFIANT PERMANENT"))
        {
            return FieldRole.PatientId;
        }
        if (Contains("DATE DE NAISSANCE") || Contains("DATE NAISSANCE"))
        {
            return FieldRole.BirthDate;
        }
        if (Has("DATE") || label.StartsWith("DATE", StringComparison.Ordinal))
        {
            return FieldRole.Date;
        }
        if (Contains("CODE POSTAL") || Contains("CODE COMMUNE") || Contains("CODE GEOGRAPHIQUE"))
        {
            return FieldRole.PostalCode;
        }
        if (Has("NOM") || Has("PRENOM") || Contains("ADRESSE") || Has("COMMUNE") || Has("RUE"))
        {
            return FieldRole.Erase;
        }
        if ((Has("SEJOUR") && !Contains("MODE") && !Contains("TYPE") && !Contains("DUREE") && !Contains("NOMBRE"))
            || Contains("ADMINISTRATIF") || Contains("N D ENTREE") || Contains("NUMERO D ENTREE")
            || Contains("N DE FACTURE") || Contains("NUMERO DE FACTURE") || Contains("N D HOSPITALISATION"))
        {
            return FieldRole.StayId;
        }
        return FieldRole.Keep;
    }
}
