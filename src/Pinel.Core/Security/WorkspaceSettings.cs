using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinel.Core.Security;

/// <summary>
/// Dossiers de travail autorises, choisis par l'utilisateur depuis
/// l'application et conserves d'une session a l'autre.
/// </summary>
/// <remarks>
/// <para>
/// Le DIM travaille sur les partages du GHT. Les dossiers autorises sont donc
/// declares ici, y compris des lecteurs reseau montes et des partages UNC. Le
/// confinement <see cref="SafePath"/> s'appuie sur cette liste : tout ce qui
/// est en dehors est refuse, ce qui garde une regle simple a expliquer a la
/// direction des ressources numeriques.
/// </para>
/// <para>
/// Fichier : <c>%LOCALAPPDATA%\Pinel\settings.json</c>. Il ne contient que des
/// chemins, jamais de donnee patient.
/// </para>
/// </remarks>
public sealed class WorkspaceSettings
{
    /// <summary>Dossiers de travail autorises, dans l'ordre d'ajout.</summary>
    [JsonPropertyName("dossiers")]
    public List<string> Folders { get; set; } = new();

    /// <summary>Dossier ou sont ecrits les fichiers produits.</summary>
    [JsonPropertyName("dossierSortie")]
    public string? OutputFolder { get; set; }

    /// <summary>Dossier des descriptifs de format deposes par le DIM.</summary>
    [JsonPropertyName("dossierFormats")]
    public string? FormatsFolder { get; set; }

    /// <summary>
    /// Identite de l'etablissement qui utilise Pinel. Aucune valeur par
    /// defaut : l'outil sert n'importe quel departement d'information
    /// medicale, et un FINESS ecrit en dur enverrait l'activite d'un
    /// etablissement sous le numero d'un autre.
    /// </summary>
    public EstablishmentSettings Etablissement { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Repertoire applicatif local, hors donnees patient.</summary>
    public static string AppDirectory => Path.Combine(
        PinelPaths.DataRoot);

    public static string DefaultPath => Path.Combine(AppDirectory, "settings.json");

    /// <summary>Espace de travail par defaut, toujours autorise.</summary>
    public static string DefaultWorkspace => Path.Combine(AppDirectory, "travail");

    public static WorkspaceSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return new WorkspaceSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WorkspaceSettings>(json, JsonOptions) ?? new WorkspaceSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Reglages illisibles : on repart sur les valeurs par defaut plutot
            // que d'empecher l'application de demarrer.
            return new WorkspaceSettings();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>
    /// Ajoute un dossier, en refusant les doublons et les chemins illisibles.
    /// Retourne le chemin normalise, ou null si le dossier est refuse.
    /// </summary>
    /// <remarks>
    /// Sucre pour <see cref="TryAddFolder"/> quand seul le resultat binaire
    /// compte a l'appel. Conserve pour ne pas casser les appelants existants ;
    /// le refus mecanique des racines nues s'applique aussi a travers elle.
    /// </remarks>
    public string? AddFolder(string folder)
    {
        var result = TryAddFolder(folder);
        return result.Accepted ? result.NormalizedPath : null;
    }

    /// <summary>
    /// Ajoute un dossier et explique pourquoi si c'est refuse. Une racine de
    /// volume nue (<c>C:\</c>) ou une racine UNC nue (<c>\\serveur\partage</c>)
    /// sont refusees meme si elles existent : les accepter transformerait le
    /// confinement de <see cref="SafePath"/> en decoration, puisque tout
    /// chemin du poste ou du partage tomberait alors sous une racine
    /// autorisee.
    /// </summary>
    public FolderAdditionResult TryAddFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new FolderAdditionResult(FolderAdditionStatus.NotFound, null);
        }

        string full;
        try
        {
            full = Path.GetFullPath(folder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new FolderAdditionResult(FolderAdditionStatus.NotFound, null);
        }

        if (IsBareRoot(full))
        {
            var status = full.StartsWith(@"\\", StringComparison.Ordinal)
                ? FolderAdditionStatus.BareUncRoot
                : FolderAdditionStatus.BareVolumeRoot;
            return new FolderAdditionResult(status, null);
        }

        if (!Directory.Exists(full))
        {
            return new FolderAdditionResult(FolderAdditionStatus.NotFound, null);
        }

        if (!Folders.Any(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase)))
        {
            Folders.Add(full);
        }
        return new FolderAdditionResult(FolderAdditionStatus.Accepted, full);
    }

    /// <summary>
    /// True quand <paramref name="full"/> est exactement la racine de son
    /// volume ou de son partage UNC, sans aucun sous-dossier derriere. La
    /// comparaison se fait separateur de fin retire des deux cotes : Windows
    /// rend <c>C:\</c> egal a lui-meme mais <c>\\serveur\partage</c> (sans
    /// barre finale) n'est egal a sa racine qu'apres ce retrait.
    /// </summary>
    private static bool IsBareRoot(string full)
    {
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root)) return false;
        return string.Equals(
            full.TrimEnd(Path.DirectorySeparatorChar),
            root.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    public bool RemoveFolder(string folder) =>
        Folders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase)) > 0;

    /// <summary>Dossier de sortie effectif, l'espace de travail par defaut a defaut.</summary>
    public string ResolveOutputFolder()
    {
        if (!string.IsNullOrWhiteSpace(OutputFolder)) return OutputFolder!;
        return DefaultWorkspace;
    }

    /// <summary>Dossier des descriptifs effectif.</summary>
    public string ResolveFormatsFolder()
    {
        if (!string.IsNullOrWhiteSpace(FormatsFolder)) return FormatsFolder!;
        return Path.Combine(AppDirectory, "formats");
    }
}

/// <summary>Statut mecanique d'une tentative d'ajout de dossier de travail.</summary>
public enum FolderAdditionStatus
{
    /// <summary>Dossier ajoute, ou deja present.</summary>
    Accepted,

    /// <summary>Chemin vide, illisible, ou dossier inexistant.</summary>
    NotFound,

    /// <summary>Racine nue d'un lecteur (<c>C:\</c>), sans sous-dossier.</summary>
    BareVolumeRoot,

    /// <summary>Racine nue d'un partage UNC (<c>\\serveur\partage</c>), sans sous-dossier.</summary>
    BareUncRoot,
}

/// <summary>
/// Resultat explicite d'une tentative d'ajout de dossier de travail, pour que
/// l'appelant puisse afficher pourquoi un chemin est refuse plutot que de se
/// contenter d'un booleen muet.
/// </summary>
public sealed record FolderAdditionResult(FolderAdditionStatus Status, string? NormalizedPath)
{
    /// <summary>True quand le dossier a ete ajoute (ou etait deja present).</summary>
    public bool Accepted => Status == FolderAdditionStatus.Accepted;

    /// <summary>Motif lisible par l'utilisateur, nommant une cible atteignable quand le dossier est refuse.</summary>
    public string Reason => Status switch
    {
        FolderAdditionStatus.Accepted => "Dossier ajoute.",
        FolderAdditionStatus.NotFound => "Dossier introuvable ou illisible.",
        FolderAdditionStatus.BareVolumeRoot =>
            "Ce chemin est un lecteur entier, pas un dossier de travail. Choisissez un sous-dossier precis, par exemple C:\\Donnees\\Rimp.",
        FolderAdditionStatus.BareUncRoot =>
            "Ce chemin est un partage reseau entier, pas un dossier de travail. Choisissez un sous-dossier precis, par exemple \\\\serveur\\partage\\Rimp.",
        _ => "Dossier refuse.",
    };
}

/// <summary>
/// Ce qui change d'un etablissement a l'autre : son nom, tel qu'il s'affiche
/// dans l'application, et les constantes que l'ATIH attend dans les fichiers
/// complementaires. Renseigne une fois, a l'installation, dans l'ecran des
/// emplacements autorises.
/// </summary>
public sealed class EstablishmentSettings
{
    /// <summary>Nom affiche, par exemple « CH de Bourgogne - Departement d'information medicale ».</summary>
    public string? Nom { get; set; }

    /// <summary>FINESS d'inscription e-PMSI, neuf chiffres.</summary>
    public string? FinessEPmsi { get; set; }

    /// <summary>FINESS geographique de l'entite qui produit l'activite.</summary>
    public string? FinessGeographique { get; set; }

    /// <summary>Type de prestation des transports, deux caracteres.</summary>
    public string? TypeDePrestation { get; set; }

    /// <summary>Code forfait des transports.</summary>
    public string? CodeForfait { get; set; }

    /// <summary>Classe de distance des transports, deux caracteres.</summary>
    public string? ClasseDistance { get; set; }

    /// <summary>Vrai quand les constantes indispensables aux transports sont renseignees.</summary>
    public bool Complete =>
        !string.IsNullOrWhiteSpace(FinessEPmsi) && !string.IsNullOrWhiteSpace(FinessGeographique);
}
