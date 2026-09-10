namespace Pinel.Core.Security;

/// <summary>
/// Confine les opérations fichier aux dossiers de travail autorisés, pour
/// qu'une page compromise dans l'interface ne puisse ni lire ni écrire
/// n'importe où sur le poste.
/// </summary>
/// <remarks>
/// <para>
/// Les dossiers autorisés se choisissent dans l'application, écran Dossiers de
/// travail, et sont conservés dans <see cref="WorkspaceSettings"/>. Ils peuvent
/// être des dossiers locaux, des lecteurs réseau montés (<c>O:\RIMP</c>) ou des
/// partages UNC (<c>\\serveur\partage</c>) : le DIM travaille sur les partages
/// du GHT, les refuser rendrait l'outil inutilisable.
/// </para>
/// <para>
/// L'espace de travail local <c>%LOCALAPPDATA%\Pinel\travail</c> est toujours
/// autorisé. La variable d'environnement <c>PINEL_WORKSPACE</c>, si elle est
/// posée par la DSI, ajoute des dossiers séparés par <c>;</c>.
/// </para>
/// <para>
/// Sont toujours refusés : les chemins qui sortent de tous les dossiers
/// autorisés, y compris par <c>..</c>, et les flux de données alternés NTFS.
/// </para>
/// </remarks>
public static class SafePath
{
    /// <summary>Variable d'environnement facultative, pour un déploiement par GPO.</summary>
    public const string EnvironmentVariable = "PINEL_WORKSPACE";

    private static IReadOnlyList<string>? _roots;
    private static readonly object Gate = new();

    /// <summary>Dossiers autorisés. Le premier est l'espace de travail local.</summary>
    public static IReadOnlyList<string> Roots
    {
        get
        {
            if (_roots is not null) return _roots;
            lock (Gate)
            {
                return _roots ??= Compute(WorkspaceSettings.Load());
            }
        }
    }

    /// <summary>Espace de travail local, destination par défaut des fichiers produits.</summary>
    public static string Root => Roots[0];

    /// <summary>
    /// Recalcule les dossiers autorisés après modification des réglages, sans
    /// redémarrage de l'application.
    /// </summary>
    public static void Reload(WorkspaceSettings settings)
    {
        lock (Gate)
        {
            _roots = Compute(settings);
        }
    }

    private static IReadOnlyList<string> Compute(WorkspaceSettings settings)
    {
        var roots = new List<string>();

        var workspace = WorkspaceSettings.DefaultWorkspace;
        Directory.CreateDirectory(workspace);
        roots.Add(Path.GetFullPath(workspace));

        foreach (var folder in settings.Folders)
        {
            AddRoot(roots, folder);
        }

        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            foreach (var entry in configured.Split(';',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddRoot(roots, entry);
            }
        }

        AddRoot(roots, settings.ResolveOutputFolder());
        return roots;
    }

    private static void AddRoot(List<string> roots, string candidate)
    {
        string full;
        try
        {
            full = Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return; // entrée illisible : ignorée, jamais fatale au démarrage
        }

        if (!roots.Any(r => string.Equals(r, full, StringComparison.OrdinalIgnoreCase)))
        {
            roots.Add(full);
        }
    }

    /// <summary>
    /// True when <paramref name="path"/> carries a Win32 extended device
    /// prefix. Public so the guard can be proven on both the raw candidate and
    /// the normalized path, which are not equivalent: "//?/C:/x" carries no
    /// prefix as written and acquires one only through normalization.
    /// </summary>
    public static bool IsExtendedDevicePath(string path)
        => path.StartsWith(@"\\?\", StringComparison.Ordinal)
        || path.StartsWith(@"\\.\", StringComparison.Ordinal);

    /// <summary>
    /// Cœur de la règle, sans état : normalise <paramref name="candidate"/> et
    /// vérifie qu'il tombe sous une des <paramref name="roots"/>. Renvoie le
    /// chemin normalisé, ou <c>null</c> s'il est refusé.
    /// </summary>
    public static string? Resolve(string candidate, IEnumerable<string> roots)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;

        // Chemins étendus : Win32 ne normalise pas les ".." derrière ces
        // préfixes, la comparaison de préfixe ne prouverait donc rien.
        // Premier passage sur la chaîne brute : court-circuit peu coûteux.
        if (IsExtendedDevicePath(candidate)) return null;

        string full;
        try
        {
            full = Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        // Second passage, celui qui compte. La forme en barres obliques
        // "//?/C:/..." ne porte pas le préfixe Win32 dans la chaîne brute :
        // elle ne l'acquiert qu'à la normalisation. Tester avant elle laissait
        // donc passer un chemin étendu, et la garde n'appliquait pas ce qu'elle
        // annonçait. On teste la valeur qui sera réellement ouverte.
        if (IsExtendedDevicePath(full)) return null;

        // Flux de données alterné : un ':' au-delà de la lettre de lecteur.
        if (full.Length > 2 && full.IndexOf(':', 2) >= 0) return null;

        var rootList = roots as IReadOnlyCollection<string> ?? roots.ToList();
        if (!IsUnder(full, rootList)) return null;

        // Point de jonction ou lien : la comparaison de chaînes ne suffit pas,
        // l'ouverture réelle suivrait la cible. Sur un partage du GHT, n'importe
        // quel compte peut poser une jonction sans droits d'administration.
        var real = ResolveFinalTarget(full);
        if (real is null) return null;
        if (!string.Equals(real, full, StringComparison.OrdinalIgnoreCase) && !IsUnder(real, rootList))
        {
            return null;
        }

        return full;
    }

    private static bool IsUnder(string full, IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            var rooted = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(rooted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Remonte le chemin segment par segment. Renvoie la cible réelle si un
    /// maillon est un lien ou une jonction, le chemin lui-même sinon, et
    /// <c>null</c> si la résolution échoue, cas dans lequel on refuse.
    /// </summary>
    private static string? ResolveFinalTarget(string full)
    {
        try
        {
            var current = full;
            var suffix = string.Empty;

            // On cherche le premier ancêtre qui existe : c'est le seul dont on
            // puisse interroger les attributs.
            while (!string.IsNullOrEmpty(current) && !File.Exists(current) && !Directory.Exists(current))
            {
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current) return full;
                suffix = Path.Combine(Path.GetFileName(current), suffix);
                current = parent;
            }

            if (string.IsNullOrEmpty(current)) return full;

            // Chaque maillon existant est inspecté jusqu'à la racine.
            var probe = current;
            while (!string.IsNullOrEmpty(probe))
            {
                var attributes = File.GetAttributes(probe);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    var target = File.ResolveLinkTarget(probe, returnFinalTarget: true)?.FullName;
                    if (target is null) return null;

                    var tail = Path.GetRelativePath(probe, current);
                    var rebuilt = tail == "." ? target : Path.Combine(target, tail);
                    return suffix.Length == 0 ? rebuilt : Path.Combine(rebuilt, suffix);
                }

                var parent = Path.GetDirectoryName(probe);
                if (string.IsNullOrEmpty(parent) || parent == probe) break;
                probe = parent;
            }

            return full;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Impossible de trancher : on refuse plutôt que de laisser passer.
            return null;
        }
    }

    /// <summary>Normalise et valide un chemin, lève si le chemin est refusé.</summary>
    public static string Require(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("chemin vide", nameof(candidate));
        }

        return Resolve(candidate, Roots)
               ?? throw new UnauthorizedAccessException($"chemin hors des dossiers autorisés : {candidate}");
    }

    /// <summary>Variante non levante, renvoie null quand le chemin est refusé.</summary>
    public static string? TryRequire(string candidate) => Resolve(candidate, Roots);
}
