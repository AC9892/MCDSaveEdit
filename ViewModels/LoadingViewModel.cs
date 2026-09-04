using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
#nullable enable

namespace MCDSaveEdit.ViewModels
{
    public sealed class LoadingViewModel : INotifyPropertyChanged
    {
        private string _currentStage = "Starting";
        private string _currentMessage = "Loading settings...";
        private string _settingsMarker = "●";
        private string _gameMarker = "○";
        private string _dataMarker = "○";
        private string _editorMarker = "○";

        public event PropertyChangedEventHandler? PropertyChanged;
        public string CurrentStage { get => _currentStage; private set => Set(ref _currentStage, value); }
        public string CurrentMessage { get => _currentMessage; private set => Set(ref _currentMessage, value); }
        public string SettingsMarker { get => _settingsMarker; private set => Set(ref _settingsMarker, value); }
        public string GameMarker { get => _gameMarker; private set => Set(ref _gameMarker, value); }
        public string DataMarker { get => _dataMarker; private set => Set(ref _dataMarker, value); }
        public string EditorMarker { get => _editorMarker; private set => Set(ref _editorMarker, value); }
        public bool IsIndeterminate => true;
        public bool CanContinueWithoutGameData { get; set; }
        public bool HasError { get; set; }
        public string? DetectedGamePath { get; set; }
        public string? TechnicalDetails { get; set; }

        public void AcceptDiagnostic(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string message = text.Trim();
            TechnicalDetails = (TechnicalDetails ?? string.Empty) + text;
            if (message.IndexOf("Searching for pak", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SettingsMarker = "✓"; GameMarker = "●"; CurrentStage = "Finding Minecraft Dungeons"; CurrentMessage = "Checking known game installations...";
            }
            else if (message.StartsWith("Manual:", StringComparison.OrdinalIgnoreCase)) CurrentMessage = "Checking your saved game location...";
            else if (message.StartsWith("Steam:", StringComparison.OrdinalIgnoreCase)) CurrentMessage = "Checking Steam installations...";
            else if (message.StartsWith("XboxApp:", StringComparison.OrdinalIgnoreCase)) CurrentMessage = "Checking Xbox app installations...";
            else if (message.StartsWith("MinecraftLauncher:", StringComparison.OrdinalIgnoreCase)) CurrentMessage = "Checking Minecraft Launcher...";
            else if (message.StartsWith("Pak files path:", StringComparison.OrdinalIgnoreCase))
            {
                DetectedGamePath = message.Substring(message.IndexOf(':') + 1).Trim(); GameMarker = "✓"; DataMarker = "●"; CurrentStage = "Minecraft Dungeons found"; CurrentMessage = "Reading game archives...";
            }
            else if (message.IndexOf("Loading Pak Files", StringComparison.OrdinalIgnoreCase) >= 0) { DataMarker = "●"; CurrentStage = "Loading game data"; CurrentMessage = "Reading PAK indexes..."; }
            else if (message.StartsWith("Found ", StringComparison.OrdinalIgnoreCase)) CurrentMessage = "Loading item definitions and images...";
            else if (message.IndexOf("language strings", StringComparison.OrdinalIgnoreCase) >= 0) CurrentMessage = "Loading language resources...";
            else if (message.IndexOf("Loading UI images", StringComparison.OrdinalIgnoreCase) >= 0) { DataMarker = "✓"; EditorMarker = "●"; CurrentStage = "Preparing the editor"; CurrentMessage = "Building the image cache..."; }
            else if (message.IndexOf("Loading Chest", StringComparison.OrdinalIgnoreCase) >= 0) CurrentMessage = "Preparing storage assets...";
            else if (message.IndexOf("Loading Equipment", StringComparison.OrdinalIgnoreCase) >= 0) CurrentMessage = "Preparing equipment assets...";
            else if (message.IndexOf("Loading Done", StringComparison.OrdinalIgnoreCase) >= 0) { EditorMarker = "✓"; CurrentStage = "Ready"; CurrentMessage = "Opening the editor..."; }
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
