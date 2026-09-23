using Pinel.Core.Formats;

namespace Pinel.Core.Processing;

/// <summary>
/// Parcourt des dossiers et classe chaque fichier candidat par format ATIH.
/// </summary>
/// <remarks>
/// <para>
/// Deux corrections tirées du lot OSPI, douze ans de fichiers réels du GHT,
/// mesurées le 23/09/2026.
/// </para>
/// <para>
/// <b>L'extension ne decide plus.</b> Le scanner ne retenait que les .txt et
/// les .csv. Or le lot porte des fichiers PMSI SANS extension du tout, nommes
/// <c>vh</c> ou <c>VIDHOSP_PSY</c> : ils etaient ecartes en silence, donc
/// absents des conversions et des controles, sans le moindre message. La liste
/// est desormais une liste de ce qu'on EXCLUT, pas de ce qu'on accepte : un
/// classeur, une archive ou un PDF ne sont pas des fichiers a largeur fixe,
/// tout le reste merite d'etre regarde.
/// </para>
/// <para>
/// <b>Le contenu prime sur le nom.</b> L'identification par le nom rate les
/// conventions d'un etablissement a l'autre : sur le site d'Erasme, elle
/// manquait <c>_ano_</c>, <c>_transp_</c> et <c>_vip_</c>. Le VID-IPP non
/// reconnu suffisait a faire ressortir des milliers de patients comme non
/// chaines. Quand un detecteur par le contenu est fourni, c'est lui qui
/// tranche, et le nom ne sert plus que de recours.
/// </para>
/// </remarks>
public static class DirectoryScanner
{
    /// <summary>
    /// Extensions qui ne designent jamais un fichier PMSI a largeur fixe.
    /// Meme liste que celle de la pseudonymisation, pour que les deux ne
    /// divergent pas : un fichier ecarte ici doit l'etre la aussi.
    /// </summary>
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls", ".xlsx", ".xlsm", ".zip", ".7z", ".rar", ".gz", ".pdf", ".json",
        ".log", ".docx", ".doc", ".odt", ".html", ".htm", ".xml", ".png", ".jpg",
        ".jpeg", ".gif", ".bmp", ".exe", ".dll", ".msi", ".lnk", ".tmp", ".bak",
    };

    public static IReadOnlyList<ScannedFile> Scan(
        IEnumerable<string> folders,
        ContentFormatDetector? detector = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ScannedFile>();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in EnumerateCandidates(folder))
            {
                if (!seen.Add(file)) continue;
                result.Add(ToScannedFile(file, detector));
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateCandidates(string folder)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var f in files)
        {
            if (!ExcludedExtensions.Contains(Path.GetExtension(f))) yield return f;
        }
    }

    private static ScannedFile ToScannedFile(string path, ContentFormatDetector? detector)
    {
        var fmt = Identify(path, detector);
        long size = 0;
        try { size = new FileInfo(path).Length; } catch (IOException) { /* best effort */ }

        return new ScannedFile(
            Path: path,
            Name: System.IO.Path.GetFileName(path),
            Format: fmt,
            SizeKb: Math.Round(size / 1024d, 1),
            Dir: System.IO.Path.GetDirectoryName(path) ?? string.Empty);
    }

    /// <summary>
    /// Contenu d'abord, nom en recours. Un echec de lecture n'ecarte pas le
    /// fichier : il retombe sur le nom, et le contrôle de longueur dira plus
    /// tard si la structure correspond.
    /// </summary>
    private static string Identify(string path, ContentFormatDetector? detector)
    {
        if (detector is not null)
        {
            try
            {
                var detected = detector.Detect(path);
                if (detected.Spec is not null) return detected.Spec.Format;
            }
            catch (IOException) { /* recours sur le nom */ }
            catch (UnauthorizedAccessException) { /* recours sur le nom */ }
        }

        return AtihFormatIdentifier.Identify(path) ?? "INCONNU";
    }
}

public sealed record ScannedFile(
    string Path,
    string Name,
    string Format,
    double SizeKb,
    string Dir);
