using MCDSaveEdit.Data;
using MCDSaveEdit.ViewModels;
using System;
using System.IO;
using System.Text;

namespace MCDSaveEdit.Services
{
    public static class DiagnosticService
    {
        public static string CreateReport(MainViewModel model)
        {
            var report = new StringBuilder();
            report.AppendLine($"Application: {Constants.APPLICATION_NAME}");
            report.AppendLine($"Version: {Constants.CURRENT_VERSION}");
            report.AppendLine($"OS: {Environment.OSVersion}");
            report.AppendLine($".NET runtime: {Environment.Version}");
            report.AppendLine($"Process architecture: {(Environment.Is64BitProcess ? "x64" : "x86")}");
            report.AppendLine($"Theme: {ThemeManager.SelectedTheme} (effective {ThemeManager.EffectiveTheme})");
            report.AppendLine($"Game content loaded: {AppModel.gameContentLoaded}");
            report.AppendLine($"Game installation type: {model.detectedGameInstallationType ?? "Not detected"}");
            report.AppendLine($"Detected game version: {model.detectedGameVersion ?? "Unknown"}");
            report.AppendLine($"Pak directory: {model.selectedPakDirectory ?? "Not selected"}");
            report.AppendLine($"Cached images: {ImageResolver.instance.cachedImageCount}");
            report.AppendLine($"Last image error: {ImageResolver.instance.lastImageError ?? "None"}");
            report.AppendLine($"Current save filename: {Path.GetFileName(model.profileModel.filePath) ?? "None"}");
            report.AppendLine($"Unsaved changes: {model.isDirty}");
            report.AppendLine("Telemetry: Disabled");
            report.AppendLine("Save contents and encryption keys are not included.");
            return report.ToString();
        }
    }
}
