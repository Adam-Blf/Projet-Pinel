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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Repertoire applicatif local, hors donnees patient.</summary>
    public static string AppDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pinel");

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
    /// Retourne le chemin normalise, ou null si le dossier n'existe pas.
    /// </summary>
    public string? AddFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;

        string full;
        try
        {
            full = Path.GetFullPath(folder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!Directory.Exists(full)) return null;

        if (!Folders.Any(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase)))
        {
            Folders.Add(full);
        }
        return full;
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
