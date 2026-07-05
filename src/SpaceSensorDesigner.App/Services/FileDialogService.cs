using Microsoft.Win32;
using SpaceSensorDesigner.Core.Serialization;

namespace SpaceSensorDesigner.App.Services;

/// <summary>Thin wrapper over the Win32 open/save dialogs for <c>.spacedesign</c> files.</summary>
public sealed class FileDialogService
{
    private const string Filter = "SpaceSensor Design (*.spacedesign)|*.spacedesign|All files (*.*)|*.*";

    public string? AskOpenPath()
    {
        var dlg = new OpenFileDialog
        {
            Filter = Filter,
            DefaultExt = FloorPlanSerializer.FileExtension,
            Title = "Open Floor Plan"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? AskOpenImagePath()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            Title = "Import Floor Plan Image"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? AskOpenDxfPath()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "AutoCAD DXF (*.dxf)|*.dxf|All files (*.*)|*.*",
            Title = "Import CAD Floor Plan (DXF)"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? AskOpenPdfPath()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PDF floor plan (*.pdf)|*.pdf|All files (*.*)|*.*",
            Title = "Import Floor Plan (PDF)"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? AskSavePath(string suggestedName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = Filter,
            DefaultExt = FloorPlanSerializer.FileExtension,
            FileName = suggestedName,
            Title = "Save Floor Plan"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    /// <summary>Generic save dialog for an export (report / CSV / PNG).</summary>
    public string? AskSaveExport(string suggestedName, string filter, string defaultExt)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = suggestedName,
            Title = "Export"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
