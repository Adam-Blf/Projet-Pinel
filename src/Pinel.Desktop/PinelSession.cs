using System.IO;
using Pinel.Core.Formats;
using Pinel.Core.Processing;
using Pinel.Core.Security;

namespace Pinel.Desktop;

/// <summary>
/// État de la session de travail : réglages, descriptifs de format, moteur de
/// traitement. Un seul exemplaire vit pendant toute la durée de l'application,
/// et il est partagé avec le service HTTP local.
/// </summary>
public sealed class PinelSession
{
    public PinelSession()
    {
        Settings = WorkspaceSettings.Load();
        SafePath.Reload(Settings);
        Processor = new AtihProcessor();
        Layouts = LoadLayouts();
        SynchronizeFolders();
    }

    /// <summary>Réglages persistés : dossiers de travail, sortie, descriptifs.</summary>
    public WorkspaceSettings Settings { get; }

    /// <summary>Descriptifs de format connus, intégrés et déposés par le DIM.</summary>
    public LayoutRegistry Layouts { get; private set; }

    /// <summary>Moteur de lecture des fichiers ATIH.</summary>
    public AtihProcessor Processor { get; }

    /// <summary>Ajoute un dossier de travail, le persiste, et l'ouvre au traitement.</summary>
    public string? AddWorkFolder(string folder)
    {
        var added = Settings.AddFolder(folder);
        if (added is null) return null;

        Settings.Save();
        SafePath.Reload(Settings);
        Processor.AddFolders(new[] { added });
        return added;
    }

    public void RemoveWorkFolder(string folder)
    {
        Settings.RemoveFolder(folder);
        Settings.Save();
        SafePath.Reload(Settings);
        SynchronizeFolders();
    }

    public void SetOutputFolder(string folder)
    {
        Settings.OutputFolder = string.IsNullOrWhiteSpace(folder) ? null : Path.GetFullPath(folder);
        Directory.CreateDirectory(Settings.ResolveOutputFolder());
        Settings.Save();
        SafePath.Reload(Settings);
    }

    /// <summary>Change le dossier des descriptifs et recharge le catalogue.</summary>
    public int SetFormatsFolder(string folder)
    {
        Settings.FormatsFolder = string.IsNullOrWhiteSpace(folder) ? null : Path.GetFullPath(folder);
        Settings.Save();
        Layouts = LoadLayouts();
        return Layouts.Formats.Count();
    }

    /// <summary>Remet à zéro le traitement, sans toucher aux réglages.</summary>
    public void Reset()
    {
        Processor.Reset();
        SynchronizeFolders();
    }

    private LayoutRegistry LoadLayouts()
    {
        var registry = new LayoutRegistry();
        var folder = Settings.ResolveFormatsFolder();
        Directory.CreateDirectory(folder);
        registry.LoadDirectory(folder);
        return registry;
    }

    private void SynchronizeFolders()
    {
        Processor.ClearFolders();
        Processor.AddFolders(Settings.Folders);
    }
}
