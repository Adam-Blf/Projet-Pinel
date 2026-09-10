using Pinel.Core.Security;

namespace Pinel.Tests;

/// <summary>
/// Garde-fous sur l'ajout d'un dossier de travail : une racine nue (lecteur
/// entier ou partage UNC entier) doit etre refusee mecaniquement, sinon le
/// confinement de <see cref="SafePath"/> perd tout son sens des qu'on
/// l'ajoute a la liste des dossiers autorises.
/// </summary>
/// <remarks>
/// Le partage <c>\\serveur-dim\pmsi</c> n'existe pas dans ce bac a sable de
/// developpement : les assertions sur la racine UNC portent donc sur le
/// motif de refus (<see cref="FolderAdditionStatus.BareUncRoot"/>), pas sur
/// une acceptation prealable observee en direct. Le controle de racine nue
/// s'execute avant le controle d'existence, ce qui rend ce refus valable
/// meme quand le partage est reellement monte sur le poste du DIM.
/// </remarks>
public sealed class WorkspaceSettingsTests
{
    [Fact]
    public void Refuse_une_racine_de_volume_nue()
    {
        var settings = new WorkspaceSettings();
        Assert.Null(settings.AddFolder(@"C:\"));
    }

    [Fact]
    public void Refuse_une_racine_unc_nue()
    {
        var settings = new WorkspaceSettings();
        Assert.Null(settings.AddFolder(@"\\serveur-dim\pmsi"));
    }

    [Fact]
    public void TryAddFolder_explique_le_refus_d_une_racine_de_volume_nue()
    {
        var settings = new WorkspaceSettings();
        var result = settings.TryAddFolder(@"C:\");

        Assert.False(result.Accepted);
        Assert.Equal(FolderAdditionStatus.BareVolumeRoot, result.Status);
        Assert.Null(result.NormalizedPath);
        Assert.Contains("lecteur", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryAddFolder_explique_le_refus_d_une_racine_unc_nue()
    {
        var settings = new WorkspaceSettings();
        var result = settings.TryAddFolder(@"\\serveur-dim\pmsi");

        Assert.False(result.Accepted);
        Assert.Equal(FolderAdditionStatus.BareUncRoot, result.Status);
        Assert.Null(result.NormalizedPath);
        Assert.Contains("partage", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryAddFolder_refuse_un_serveur_unc_sans_partage()
    {
        var settings = new WorkspaceSettings();
        var result = settings.TryAddFolder(@"\\serveur-dim");

        Assert.False(result.Accepted);
        Assert.Equal(FolderAdditionStatus.BareUncRoot, result.Status);
    }

    [Fact]
    public void TryAddFolder_refuse_une_racine_avant_meme_de_verifier_qu_elle_existe()
    {
        // Un partage nue fictif, jamais monte sur ce poste : le motif doit
        // rester BareUncRoot et non NotFound, preuve que le controle de
        // racine passe avant le controle d'existence.
        var settings = new WorkspaceSettings();
        var result = settings.TryAddFolder(@"\\partage-jamais-monte\entier");

        Assert.Equal(FolderAdditionStatus.BareUncRoot, result.Status);
        Assert.NotEqual(FolderAdditionStatus.NotFound, result.Status);
    }

    [Fact]
    public void TryAddFolder_accepte_un_sous_dossier_existant()
    {
        var settings = new WorkspaceSettings();
        var temp = Directory.CreateTempSubdirectory("pinel-workspace-tests-");
        try
        {
            var result = settings.TryAddFolder(temp.FullName);

            Assert.True(result.Accepted);
            Assert.Equal(FolderAdditionStatus.Accepted, result.Status);
            Assert.Equal(Path.GetFullPath(temp.FullName), result.NormalizedPath);
            Assert.Contains(result.NormalizedPath!, settings.Folders);
        }
        finally
        {
            Directory.Delete(temp.FullName, recursive: true);
        }
    }

    [Fact]
    public void TryAddFolder_refuse_un_dossier_inexistant()
    {
        var settings = new WorkspaceSettings();
        var missing = Path.Combine(Path.GetTempPath(), "pinel-inexistant-" + Guid.NewGuid());

        var result = settings.TryAddFolder(missing);

        Assert.False(result.Accepted);
        Assert.Equal(FolderAdditionStatus.NotFound, result.Status);
    }
}
