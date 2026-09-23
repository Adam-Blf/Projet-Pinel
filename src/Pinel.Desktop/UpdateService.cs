using System.IO;
using System.Text.Json;
using Velopack;
using Velopack.Sources;
using Pinel.Core.Security;

namespace Pinel.Desktop;

/// <summary>Etat de la mise a jour, tel que l'ecran "A propos" l'affiche.</summary>
public sealed record UpdateState(
    string Version,
    bool Installed,
    string? Source,
    string? Available,
    string? Note);

/// <summary>
/// Mise a jour de Pinel depuis un dossier du reseau du GHT.
/// </summary>
/// <remarks>
/// <para>
/// Les postes du DIM n'ont pas internet et leurs utilisateurs n'ont pas les
/// droits d'administration. Pinel s'installe donc pour l'utilisateur courant,
/// sous son profil, et se met a jour depuis un partage que la direction des
/// ressources numeriques alimente : aucun flux sortant, aucune elevation de
/// privilege, et un poste isole continue de fonctionner avec la version qu'il
/// a deja.
/// </para>
/// <para>
/// Le chemin du partage se lit dans <c>maj.json</c>, a cote des reglages. Tant
/// qu'il n'est pas renseigne, Pinel ne cherche aucune mise a jour et le dit.
/// </para>
/// </remarks>
public sealed class UpdateService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _configPath = PinelPaths.In("maj.json");

    private UpdateInfo? _pending;

    /// <summary>Version installee, lue sur l'assembly.</summary>
    public string Version =>
        typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "inconnue";

    /// <summary>
    /// Vrai quand Pinel tourne depuis une installation, faux en portable ou
    /// depuis un dossier de compilation. Le gestionnaire le dit lui-meme ; il
    /// accepte une source vide tant qu'on ne lui demande rien.
    /// </summary>
    public bool Installed
    {
        get
        {
            try
            {
                return new UpdateManager(new SimpleFileSource(new DirectoryInfo(Source ?? "."))).IsInstalled;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
            {
                return false;
            }
        }
    }

    /// <summary>Dossier des mises a jour, ou null s'il n'est pas configure.</summary>
    public string? Source
    {
        get
        {
            if (!File.Exists(_configPath)) return null;
            try
            {
                var config = JsonSerializer.Deserialize<UpdateConfig>(File.ReadAllText(_configPath));
                return string.IsNullOrWhiteSpace(config?.Source) ? null : config.Source;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                return null;
            }
        }
    }

    public void SetSource(string? folder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(
            new UpdateConfig(string.IsNullOrWhiteSpace(folder) ? null : folder.Trim()), Json));
    }

    /// <summary>Regarde si une version plus recente attend sur le partage.</summary>
    public async Task<UpdateState> CheckAsync()
    {
        if (!Installed)
        {
            return new UpdateState(Version, false, Source, null,
                "Pinel tourne en version portable : les mises a jour passent par l'installateur.");
        }
        var source = Source;
        if (source is null)
        {
            return new UpdateState(Version, true, null, null,
                "Aucun dossier de mise a jour n'est configure sur ce poste.");
        }
        if (!Directory.Exists(source))
        {
            return new UpdateState(Version, true, source, null,
                "Dossier de mise a jour injoignable. Le partage du GHT est peut-etre hors ligne.");
        }

        var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(source)));
        _pending = await manager.CheckForUpdatesAsync();
        return _pending is null
            ? new UpdateState(Version, true, source, null, "Pinel est a jour.")
            : new UpdateState(Version, true, source, _pending.TargetFullRelease.Version.ToString(),
                "Une version plus recente est disponible.");
    }

    /// <summary>
    /// Telecharge la mise a jour en attente et redemarre Pinel dessus. Le
    /// telechargement ne prend que la difference avec la version installee.
    /// </summary>
    public async Task<UpdateState> ApplyAsync()
    {
        var state = _pending is null ? await CheckAsync() : null;
        if (_pending is null) return state!;

        var source = Source!;
        var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(source)));
        await manager.DownloadUpdatesAsync(_pending);
        manager.ApplyUpdatesAndRestart(_pending);
        return new UpdateState(Version, true, source, _pending.TargetFullRelease.Version.ToString(),
            "Mise a jour installee, Pinel redemarre.");
    }

    private sealed record UpdateConfig(string? Source);
}
