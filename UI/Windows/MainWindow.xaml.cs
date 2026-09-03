using MCDSaveEdit.Data;
using MCDSaveEdit.Logic;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Logic.Validation;
using MCDSaveEdit.Services;
using MCDSaveEdit.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Interop;
#nullable enable

namespace MCDSaveEdit.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Action? onRelaunch;
        public Action<string?, ProfileSaveFile?>? onReload;

        private readonly MainViewModel _model;

        private Window? _busyWindow = null;
        private bool _allowClose;
        private bool _closeConfirmationInProgress;
        private DispatcherTimer? _toastTimer;
        private SettingsWindow? _embeddedSettings;
        private string? _embeddedSettingsSavePath;
        private TowerLiveMonitorWindow? _towerLiveMonitorWindow;
        private TowerLiveSnapshot? _towerEditorSnapshot;
        private string? _towerEditorFilePath;
        private string? _selectedCampMerchantKey;
        private readonly List<CampSlotEditor> _campSlotEditors = new List<CampSlotEditor>();
        private Dictionary<string, int> _towerOfferedRewardPool = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private WrapPanel? _towerRewardPoolItemsPanel;
        private ContentControl? _towerRewardPoolVisualHost;
        private HwndSource? _hotkeySource;
        private const int LiveMonitorHotkeyId = 0x4D43;
        private const int WmHotkey = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private sealed class CampSlotEditor
        {
            public MerchantSlot Slot { get; set; } = null!;
            public TextBox PriceMultiplier { get; set; } = null!;
            public TextBox RebateFraction { get; set; } = null!;
            public CheckBox Reserved { get; set; } = null!;
        }

        public MainWindow(MainViewModel model)
        {
            _model = model;
            _model.dirtyStateChanged = _ => Dispatcher.Invoke(updateTitleUI);
            InitializeComponent();
            SourceInitialized += mainWindow_SourceInitialized;
            updateMerchantSectionLabels();
            loadEmbeddedSettings();
            translateStaticStrings();

            _model.showError = showError;
            gameFilesLocationMenuItem.Header = ImageResolver.instance.path ?? R.GAME_FILES_WINDOW_NO_CONTENT_BUTTON;
            var detectedGameVersion = _model.detectedGameVersion;
            if (detectedGameVersion == null)
            {
                gameFilesVersionMenuItem.Header = R.NO_GAME_VERSION_DETECTED;
            }
            else
            {
                gameFilesVersionMenuItem.Header = R.formatMCD_VERSION(detectedGameVersion);
            }

            refreshRecentFilesList();

            createLangMenuItems();


#if HIDE_CHEST_TAB
            chestTab.Visibility = Visibility.Collapsed;
#else
            chestTab.model = _model.profileModel;
#endif

            inventoryTab.model = _model.profileModel;
            statsTab.model = _model.profileModel;
            _model.profileModel.profile.subscribe(_ => this.updateUI());

            //Clear out design/testing values
            updateUI();

            checkForNewVersionAsync();
        }

        #region UI

        private void updateMerchantSectionLabels()
        {
            campMerchantsTabItem.Header = "MERCHANTS & SERVICES";
            foreach (TextBlock textBlock in logicalChildren<TextBlock>(campMerchantsTabItem))
            {
                if (textBlock.Text == "Camp merchants") textBlock.Text = "Merchants & services";
                else if (textBlock.Text == "Edit interaction, restock, pricing, reservation, and stocked-item data.")
                    textBlock.Text = "Edit every stored merchant and service definition, including camp shops, Tower services, storage, mission, and event records.";
            }
        }

        private static IEnumerable<T> logicalChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is T match) yield return match;
                if (child is DependencyObject dependencyObject)
                {
                    foreach (T descendant in logicalChildren<T>(dependencyObject)) yield return descendant;
                }
            }
        }

        public void updateUI()
        {
            updateTitleUI();
            statsTab.updateUI();
            inventoryTab.updateUI();
            chestTab.updateUI();
            updateCharacterOverviewFields();
            refreshCampMerchantList();
            if (!string.Equals(_towerEditorFilePath, _model.profileModel.filePath, StringComparison.OrdinalIgnoreCase))
                resetTowerEditor();
            _towerLiveMonitorWindow?.AttachSaveFile(_model.profileModel.filePath);
            closeBusyIndicator();
        }

        private void updateCharacterOverviewFields()
        {
            var profile = _model.profileModel.profile.value;
            bool loaded = profile != null;
            characterLevelTextBlock.Text = loaded ? profile!.level().ToString("N0") : "—";
            characterPowerOverviewTextBlock.Text = loaded ? profile!.TotalGearPower.ToString("N0") : "—";
            characterPortalCheckBox.IsEnabled = loaded && _model.profileModel.unlockPortal.value.HasValue;
            characterPortalCheckBox.IsChecked = _model.profileModel.unlockPortal.value == true;
        }

        private void characterPortalCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_model.profileModel.profile.value == null) return;
            _model.profileModel.unlockPortal.setValue = characterPortalCheckBox.IsChecked == true;
        }

        private void updateTitleUI()
        {
            if (_model.profileModel.filePath != null)
            {
                var dirtyMarker = _model.isDirty ? " *" : string.Empty;
                Title = string.Format("{0} - {1}{2}", R.APPLICATION_TITLE, Path.GetFileName(_model.profileModel.filePath), dirtyMarker);
                saveMenuItem.IsEnabled = saveAsMenuItem.IsEnabled = true;
                quickSaveButton.IsEnabled = true;
                restoreBackupMenuItem.IsEnabled = true;
                var profile = _model.profileModel.profile.value;
                string characterName = string.IsNullOrWhiteSpace(profile?.Name) ? "Unnamed character" : profile!.Name;
                characterNameTextBlock.Text = characterName;
                fileNameTextBlock.Text = Path.GetFileName(_model.profileModel.filePath);
                powerTextBlock.Text = $"Power {profile?.TotalGearPower ?? 0}";
                homeCharacterText.Text = characterName;
                homePowerText.Text = (profile?.TotalGearPower ?? 0).ToString("N0");
                homeFileText.Text = Path.GetFileName(_model.profileModel.filePath);
                homeModifiedText.Text = File.Exists(_model.profileModel.filePath)
                    ? File.GetLastWriteTime(_model.profileModel.filePath).ToString("g")
                    : "Not written yet";
                string backupFolder = Path.Combine(Path.GetDirectoryName(_model.profileModel.filePath)!, BackupService.BackupDirectoryName);
                homeBackupText.Text = Directory.Exists(backupFolder) && Directory.EnumerateFiles(backupFolder).Any()
                    ? "Available" : "None yet";
                homeValidationText.Text = _model.isDirty ? "Changes pending" : "Ready";
                emptyStatePanel.Visibility = Visibility.Collapsed;
                loadedHomePanel.Visibility = Visibility.Visible;
            }
            else
            {
                Title = R.APPLICATION_TITLE;
                saveMenuItem.IsEnabled = saveAsMenuItem.IsEnabled = false;
                quickSaveButton.IsEnabled = false;
                restoreBackupMenuItem.IsEnabled = false;
                characterNameTextBlock.Text = "No character loaded";
                fileNameTextBlock.Text = "Open a character save to begin";
                powerTextBlock.Text = "Power —";
                homeFileText.Text = homeModifiedText.Text = homeBackupText.Text = "—";
                homeValidationText.Text = "Open a save";
                emptyStatePanel.Visibility = Visibility.Visible;
                loadedHomePanel.Visibility = Visibility.Collapsed;
            }
            unsavedBadge.Visibility = _model.isDirty ? Visibility.Visible : Visibility.Collapsed;
            homeGameContentText.Text = AppModel.gameContentLoaded
                ? (_model.detectedGameVersion == null ? "Loaded" : $"MCD {_model.detectedGameVersion}")
                : "Limited mode";
            gameAssetStatusRun.Text = AppModel.gameContentLoaded ? "loaded" : "limited mode";
        }

        private void navigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (mainTabControl == null) return;
            if (sender is RadioButton button && int.TryParse(button.CommandParameter?.ToString(), out int pageIndex))
            {
                mainTabControl.SelectedIndex = pageIndex;
                statusTextBlock.Text = $"{button.Content} page";
                if (pageIndex == 6) loadEmbeddedSettings();
            }
        }

        private void loadEmbeddedSettings(bool force = false)
        {
            string? currentPath = _model.profileModel.filePath;
            if (!force && _embeddedSettings != null && string.Equals(_embeddedSettingsSavePath, currentPath, StringComparison.OrdinalIgnoreCase)) return;
            settingsHost.Content = null;
            _embeddedSettings?.Close();
            _embeddedSettings = new SettingsWindow(currentPath);
            _embeddedSettingsSavePath = currentPath;
            _embeddedSettings.SettingsSaved = () =>
            {
                registerLiveMonitorHotkey();
                statusTextBlock.Text = "Settings applied";
                showToast("Settings applied");
            };
            _embeddedSettings.SettingsReset = () => loadEmbeddedSettings(true);
            settingsHost.Content = _embeddedSettings.DetachContentForEmbedding();
        }

        private void openInventoryPageButton_Click(object sender, RoutedEventArgs e)
        {
            mainTabControl.SelectedIndex = 2;
            inventoryNavigationButton.IsChecked = true;
        }

        private void openTowerLiveMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = _model.profileModel.filePath;
            if (_towerLiveMonitorWindow != null && _towerLiveMonitorWindow.IsLoaded)
            {
                _towerLiveMonitorWindow.AttachSaveFile(filePath);
                if (_towerLiveMonitorWindow.WindowState == WindowState.Minimized)
                    _towerLiveMonitorWindow.WindowState = WindowState.Normal;
                if (!_towerLiveMonitorWindow.IsVisible) _towerLiveMonitorWindow.Show();
                _towerLiveMonitorWindow.ShowCompactOverlay();
                return;
            }

            _towerLiveMonitorWindow = new TowerLiveMonitorWindow(filePath);
            _towerLiveMonitorWindow.Closed += (_, __) => _towerLiveMonitorWindow = null;
            _towerLiveMonitorWindow.Show();
            statusTextBlock.Text = "Tower live overlay opened";
        }

        private void mainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hotkeySource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hotkeySource?.AddHook(windowMessageHook);
            registerLiveMonitorHotkey();
        }

        private void registerLiveMonitorHotkey()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            UnregisterHotKey(handle, LiveMonitorHotkeyId);
            if (!LiveMonitorHotkey.TryParse(MCDSaveEdit.Properties.Settings.Default.LiveMonitorHotkey,
                out uint modifiers, out uint virtualKey, out _)) return;
            if (!RegisterHotKey(handle, LiveMonitorHotkeyId, modifiers | LiveMonitorHotkey.NoRepeat, virtualKey))
                statusTextBlock.Text = "Live monitor hotkey is already used by another app";
        }

        private IntPtr windowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmHotkey && wParam.ToInt32() == LiveMonitorHotkeyId)
            {
                toggleLiveMonitorOverlay();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void toggleLiveMonitorOverlay()
        {
            if (_towerLiveMonitorWindow != null && _towerLiveMonitorWindow.IsLoaded)
            {
                if (_towerLiveMonitorWindow.IsVisible) _towerLiveMonitorWindow.Hide();
                else _towerLiveMonitorWindow.ShowCompactOverlay();
                return;
            }
            openTowerLiveMonitorButton_Click(this, new RoutedEventArgs());
        }

        private void resetTowerEditor()
        {
            _towerEditorSnapshot = null;
            _towerEditorFilePath = _model.profileModel.filePath;
            if (towerEditorPanel == null) return;
            towerEditorPanel.IsEnabled = false;
            saveTowerChangesButton.IsEnabled = false;
            towerEditorStatusText.Text = _model.profileModel.profile.value == null
                ? "Open a character save first."
                : "Load the Tower data from the current character.";
            towerEditorIdText.Text = towerEditorDifficultyText.Text = towerEditorSeedText.Text = towerEditorItemsText.Text = "—";
            towerFloorTextBox.Text = towerLivesTextBox.Text = towerBossesTextBox.Text = towerArrowsTextBox.Text = towerPointsTextBox.Text = string.Empty;
            towerFloorCompletedCheckBox.IsChecked = false;
            towerFloorRangeText.Text = "Valid range will appear after loading.";
        }

        private async void reloadTowerEditorButton_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = _model.profileModel.filePath;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ModernDialog.Show(this, "Open a character first", "Tower data can only be loaded from an existing character save.",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            towerEditorStatusText.Text = "Reading Tower data…";
            towerEditorPanel.IsEnabled = false;
            saveTowerChangesButton.IsEnabled = false;
            try
            {
                TowerLiveSnapshot snapshot = await TowerLiveSaveReader.ReadAsync(filePath!);
                if (!snapshot.HasTowerData)
                {
                    towerEditorStatusText.Text = "No active Tower run was found in this character.";
                    return;
                }

                _towerEditorSnapshot = snapshot;
                _towerEditorFilePath = filePath;
                towerFloorTextBox.Text = snapshot.CurrentFloor.ToString(CultureInfo.InvariantCulture);
                towerLivesTextBox.Text = snapshot.LivesLost.ToString(CultureInfo.InvariantCulture);
                towerBossesTextBox.Text = snapshot.BossesKilled.ToString(CultureInfo.InvariantCulture);
                towerArrowsTextBox.Text = snapshot.Arrows.ToString(CultureInfo.InvariantCulture);
                towerPointsTextBox.Text = snapshot.EnchantmentPoints.ToString(CultureInfo.InvariantCulture);
                towerFloorCompletedCheckBox.IsChecked = snapshot.CurrentFloorCompleted;
                towerFloorRangeText.Text = $"Valid range: 0–{Math.Max(0, snapshot.TotalFloors - 1)}";
                towerEditorIdText.Text = snapshot.TowerId;
                towerEditorDifficultyText.Text = snapshot.Difficulty.Replace("Difficulty_", "Difficulty ")
                    + " • " + snapshot.Threat.Replace("Threat_", "Threat ");
                towerEditorSeedText.Text = snapshot.TowerSeed.ToString(CultureInfo.InvariantCulture);
                towerEditorItemsText.Text = snapshot.PlayerItemsSummary;
                towerEditorStatusText.Text = $"Active run loaded • floor index {snapshot.CurrentFloor} • {snapshot.TotalFloors} configured floors";
                towerEditorPanel.IsEnabled = true;
                saveTowerChangesButton.IsEnabled = true;
            }
            catch (Exception exception)
            {
                EventLogger.logError("Tower editor load: " + exception);
                towerEditorStatusText.Text = "Tower data could not be loaded.";
                showError("The Tower data could not be read.\n\n" + exception.Message);
            }
        }

        private async void saveTowerChangesButton_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = _model.profileModel.filePath;
            if (_towerEditorSnapshot == null || string.IsNullOrWhiteSpace(filePath)) return;
            if (_model.isDirty)
            {
                ModernDialog.Show(this, "Save editor changes first",
                    "The character has unsaved changes in the main editor.", MessageBoxButton.OK, MessageBoxImage.Warning,
                    "Save or discard those changes before writing Tower data so neither set of changes is lost.");
                return;
            }
            if (isMinecraftDungeonsRunning())
            {
                ModernDialog.Show(this, "Close Minecraft Dungeons",
                    "Tower modification is blocked while the game is running.", MessageBoxButton.OK, MessageBoxImage.Warning,
                    "Close the game completely, then reload the Tower data before saving.");
                return;
            }

            if (!tryReadTowerInteger(towerFloorTextBox.Text, "Floor index", out int floor)
                || !tryReadTowerInteger(towerLivesTextBox.Text, "Lives lost", out int lives)
                || !tryReadTowerInteger(towerBossesTextBox.Text, "Bosses killed", out int bosses)
                || !tryReadTowerInteger(towerArrowsTextBox.Text, "Arrows", out int arrows)
                || !tryReadTowerInteger(towerPointsTextBox.Text, "Tower enchantment points", out int points)) return;

            var values = new TowerEditValues
            {
                CurrentFloor = floor,
                LivesLost = lives,
                BossesKilled = bosses,
                Arrows = arrows,
                EnchantmentPoints = points,
                CurrentFloorCompleted = towerFloorCompletedCheckBox.IsChecked == true
            };
            var confirmation = ModernDialog.Show(this, "Save Tower changes",
                $"Write the modified Tower run to {Path.GetFileName(filePath)}?",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning,
                "A timestamped backup will be created before the character save is replaced.");
            if (confirmation != MessageBoxResult.OK) return;

            saveTowerChangesButton.IsEnabled = false;
            towerEditorStatusText.Text = "Validating and writing Tower data…";
            try
            {
                string? backupPath = await TowerLiveSaveReader.SaveChangesAsync(filePath!, values);
                await _model.handleFileOpenAsync(filePath!);
                await reloadTowerEditorAsync(filePath!);
                statusTextBlock.Text = "Tower changes saved • backup created";
                showToast("Tower changes saved — backup created");
                EventLogger.logEvent("towerSaveModified", new Dictionary<string, object>
                {
                    { "backupCreated", !string.IsNullOrWhiteSpace(backupPath) }, { "floor", floor }
                });
            }
            catch (Exception exception)
            {
                EventLogger.logError("Tower editor save: " + exception);
                towerEditorStatusText.Text = "Tower changes were not saved.";
                showError("The Tower changes could not be saved safely.\n\n" + exception.Message);
                saveTowerChangesButton.IsEnabled = true;
            }
        }

        private async Task reloadTowerEditorAsync(string filePath)
        {
            TowerLiveSnapshot snapshot = await TowerLiveSaveReader.ReadAsync(filePath);
            _towerEditorSnapshot = snapshot;
            _towerEditorFilePath = filePath;
            towerFloorTextBox.Text = snapshot.CurrentFloor.ToString(CultureInfo.InvariantCulture);
            towerLivesTextBox.Text = snapshot.LivesLost.ToString(CultureInfo.InvariantCulture);
            towerBossesTextBox.Text = snapshot.BossesKilled.ToString(CultureInfo.InvariantCulture);
            towerArrowsTextBox.Text = snapshot.Arrows.ToString(CultureInfo.InvariantCulture);
            towerPointsTextBox.Text = snapshot.EnchantmentPoints.ToString(CultureInfo.InvariantCulture);
            towerFloorCompletedCheckBox.IsChecked = snapshot.CurrentFloorCompleted;
            towerEditorStatusText.Text = "Tower changes verified from disk.";
            towerEditorPanel.IsEnabled = saveTowerChangesButton.IsEnabled = true;
        }

        private bool tryReadTowerInteger(string text, string label, out int value)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0) return true;
            ModernDialog.Show(this, "Invalid Tower value", label + " must be a whole number of zero or greater.",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static bool isMinecraftDungeonsRunning()
        {
            return Process.GetProcesses().Any(process =>
                process.ProcessName.IndexOf("Dungeons", StringComparison.OrdinalIgnoreCase) >= 0
                && process.ProcessName.IndexOf("MCDSaveEdit", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private async void reloadTowerMerchantButton_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = _model.profileModel.filePath;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ModernDialog.Show(this, "Open a character first", "Load a character with an active Tower run first.",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_model.isDirty)
            {
                ModernDialog.Show(this, "Save editor changes first",
                    "Save or discard pending editor changes before reloading the latest Tower loot from disk.",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                // Tower services such as Tower Complete live in merchantData, not
                // in the active-run snapshot. Refresh the clean profile model too,
                // otherwise these cards show the stock from when the file was opened.
                await _model.handleFileOpenAsync(filePath!);
                await reloadTowerMerchantAsync(filePath!);
            }
            catch (Exception exception)
            {
                EventLogger.logError("Tower merchant load: " + exception);
                showError("Tower merchant data could not be loaded.\n\n" + exception.Message);
            }
        }

        private async Task reloadTowerMerchantAsync(string filePath)
        {
            TowerLiveSnapshot snapshot = await TowerLiveSaveReader.ReadAsync(filePath);
            _towerEditorSnapshot = snapshot;
            towerMerchantInteractionsTextBox.Text = snapshot.MerchantInteractionsJson;
            towerMerchantRewardsTextBox.Text = snapshot.OfferedRewardsJson;
            _towerOfferedRewardPool = new Dictionary<string, int>(snapshot.OfferedRewardPool, StringComparer.OrdinalIgnoreCase);
            ensureTowerRewardPoolRightPanel();
            if (_towerRewardPoolVisualHost != null)
                _towerRewardPoolVisualHost.Content = createTowerRewardPoolEditor();
            towerMerchantItemsPanel.Children.Clear();
            var towerServices = (_model.profileModel.profile.value?.MerchantData
                ?? new Dictionary<string, MerchantDef>())
                .Where(entry => entry.Key.IndexOf("Tower", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(entry => merchantDisplayName(entry.Key))
                .ToList();
            towerMerchantItemsPanel.Children.Add(createTowerMerchantSectionLabel("TOWER SERVICES"));
            foreach (var service in towerServices)
                towerMerchantItemsPanel.Children.Add(createTowerServiceCard(service.Key, service.Value));
            if (towerServices.Count == 0)
                towerMerchantItemsPanel.Children.Add(new TextBlock { Text = "No Tower service definitions were found.", TextWrapping = TextWrapping.Wrap });

            towerMerchantItemsPanel.Children.Add(createTowerMerchantSectionLabel("CURRENT TOWER FINAL REWARDS"));
            foreach (Item reward in snapshot.FinalRewardItems)
                towerMerchantItemsPanel.Children.Add(createTowerFinalRewardCard(reward));
            if (snapshot.FinalRewardItems.Count == 0)
                towerMerchantItemsPanel.Children.Add(new TextBlock
                {
                    Text = snapshot.RunFinished
                        ? "The game has not written final reward items for this run yet."
                        : "Final reward choices are generated when the active Tower run is finished.",
                    Width = 340,
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (System.Windows.Media.Brush)FindResource("AppMutedForegroundBrush")
                });
            towerMerchantItemsPanel.Children.Add(createTowerMerchantSectionLabel("CURRENT TOWER LOADOUT • CLICK TO EDIT"));
            for (int index = 0; index < snapshot.PlayerItems.Count; index++)
                towerMerchantItemsPanel.Children.Add(createTowerLoadoutItemCard(snapshot.PlayerItems[index], index));
            if (snapshot.PlayerItems.Count == 0)
                towerMerchantItemsPanel.Children.Add(new TextBlock
                {
                    Text = "No Tower loadout items are recorded yet.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (System.Windows.Media.Brush)FindResource("AppSecondaryForegroundBrush")
                });
            string source = snapshot.MerchantItemTypes.Count > 0 ? "offered item" : "current loadout item";
            towerMerchantStatusText.Text = snapshot.MerchantItemTypes.Count > 0
                ? $"Loaded {towerServices.Count} Tower services and {snapshot.MerchantItemTypes.Count} {source}{(snapshot.MerchantItemTypes.Count == 1 ? "" : "s")}."
                : $"Loaded {towerServices.Count} Tower services. No run offers are stored yet; showing the current loadout when available.";
            saveTowerMerchantButton.IsEnabled = snapshot.HasTowerData;
        }

        private Border createTowerLoadoutItemCard(Item item, int itemIndex)
        {
            var itemControl = new ItemControl { item = item, Width = 86, Height = 86 };
            var button = new Button { Content = itemControl, Padding = new Thickness(2), ToolTip = "Edit " + R.itemName(item.Type) };
            button.Click += async (_, __) =>
            {
                string? filePath = _model.profileModel.filePath;
                if (string.IsNullOrWhiteSpace(filePath)) return;
                if (_model.isDirty)
                {
                    ModernDialog.Show(this, "Save editor changes first", "Save or discard pending editor changes before modifying the encrypted Tower loadout.", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (isMinecraftDungeonsRunning())
                {
                    ModernDialog.Show(this, "Close Minecraft Dungeons", "Tower loadout modification is blocked while the game is running.", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var editor = new MerchantItemEditorWindow(item, "Edit Tower loadout • " + R.itemName(item.Type), false) { Owner = this };
                if (editor.ShowDialog() != true) return;
                try
                {
                    await TowerLiveSaveReader.SaveLoadoutItemAsync(filePath!, itemIndex, editor.EditedItem);
                    await _model.handleFileOpenAsync(filePath!);
                    await reloadTowerMerchantAsync(filePath!);
                    statusTextBlock.Text = "Tower loadout item saved • backup created";
                    showToast("Tower loadout item saved — backup created");
                }
                catch (Exception exception)
                {
                    EventLogger.logError("Tower loadout item save: " + exception);
                    showError("The Tower loadout item could not be saved safely.\n\n" + exception.Message);
                }
            };
            return new Border
            {
                Child = button, Width = 98, Height = 98,
                Margin = new Thickness(0, 0, 8, 8),
                Background = (System.Windows.Media.Brush)FindResource("AppElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AppBorderBrush"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6)
            };
        }

        private TextBlock createTowerMerchantSectionLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Width = 340,
                Margin = new Thickness(0, 4, 0, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("AppSecondaryForegroundBrush"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
        }

        private Border createTowerFinalRewardCard(Item item)
        {
            var itemControl = new ItemControl { item = item, Width = 86, Height = 86 };
            return new Border
            {
                Child = itemControl,
                Width = 98,
                Height = 98,
                ToolTip = "Current final reward • " + R.itemName(item.Type),
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(5),
                Background = (System.Windows.Media.Brush)FindResource("AppElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AppBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };
        }

        private FrameworkElement createTowerRewardPoolEditor()
        {
            var root = new StackPanel { Margin = new Thickness(8) };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock
            {
                Text = "CURRENT RUN OFFERED-REWARD POOL",
                Foreground = (System.Windows.Media.Brush)FindResource("AppSecondaryForegroundBrush"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            var headerButtons = new StackPanel { Orientation = Orientation.Horizontal };
            var fullTowerButton = new Button { Content = "Edit All Reward Floors…", Padding = new Thickness(9, 5, 9, 5), MinHeight = 28, Margin = new Thickness(0, 0, 6, 0) };
            fullTowerButton.Click += editFullTowerRewardsButton_Click;
            var addButton = new Button { Content = "+ Add Item", Padding = new Thickness(9, 5, 9, 5), MinHeight = 28 };
            addButton.Click += (_, __) => chooseTowerRewardPoolItem(null);
            headerButtons.Children.Add(fullTowerButton);
            headerButtons.Children.Add(addButton);
            Grid.SetColumn(headerButtons, 1);
            header.Children.Add(headerButtons);
            root.Children.Add(header);
            root.Children.Add(new TextBlock
            {
                Text = "These entries contain item types and offer-state values only. Power, rarity, enchantments, and gilding are not generated until the game creates concrete reward items.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10,
                Margin = new Thickness(0, 5, 0, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("AppMutedForegroundBrush")
            });
            _towerRewardPoolItemsPanel = new WrapPanel();
            root.Children.Add(_towerRewardPoolItemsPanel);
            refreshTowerRewardPoolEditor();
            return root;
        }

        private async void editFullTowerRewardsButton_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = _model.profileModel.filePath;
            if (_towerEditorSnapshot == null || string.IsNullOrWhiteSpace(filePath)
                || _towerEditorSnapshot.FloorRewardConfiguration.Count == 0)
            {
                ModernDialog.Show(this, "No Tower configuration",
                    "Reload Tower merchant data after starting a Tower run.",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_model.isDirty)
            {
                ModernDialog.Show(this, "Save editor changes first",
                    "Save or discard pending editor changes before modifying the Tower floor configuration.",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (isMinecraftDungeonsRunning())
            {
                ModernDialog.Show(this, "Close Minecraft Dungeons",
                    "Tower floor reward modification is blocked while the game is running.",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var editor = new TowerFloorRewardsWindow(_towerEditorSnapshot.FloorRewardConfiguration) { Owner = this };
            if (editor.ShowDialog() != true) return;
            if (ModernDialog.Show(this, "Save Tower reward configuration",
                $"Replace the reward-category slots for floors 1–{editor.EditedFloors.Count}?",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning,
                "Floor 0 will remain untouched. The Tower structure will be checked again and a timestamped backup will be created.") != MessageBoxResult.OK) return;
            try
            {
                await TowerLiveSaveReader.SaveFloorRewardConfigurationAsync(filePath!, editor.EditedFloors);
                await _model.handleFileOpenAsync(filePath!);
                await reloadTowerMerchantAsync(filePath!);
                statusTextBlock.Text = "Tower floor reward configuration saved • backup created";
                showToast("Tower floor rewards saved — backup created");
            }
            catch (Exception exception)
            {
                EventLogger.logError("Tower floor reward save: " + exception);
                showError("The Tower floor reward configuration could not be saved safely.\n\n" + exception.Message);
            }
        }

        private void ensureTowerRewardPoolRightPanel()
        {
            if (_towerRewardPoolVisualHost != null) return;
            if (!(towerMerchantRewardsTextBox.Parent is Grid rewardGrid)) return;

            rewardGrid.Children.Remove(towerMerchantRewardsTextBox);
            var tabs = new TabControl { Margin = new Thickness(0, 6, 0, 0) };
            _towerRewardPoolVisualHost = new ContentControl();
            var visualScroll = new ScrollViewer
            {
                Content = _towerRewardPoolVisualHost,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            tabs.Items.Add(new TabItem { Header = "VISUAL POOL", Content = visualScroll });
            tabs.Items.Add(new TabItem { Header = "RAW JSON", Content = towerMerchantRewardsTextBox });
            Grid.SetRow(tabs, 1);
            rewardGrid.Children.Add(tabs);
        }

        private void refreshTowerRewardPoolEditor()
        {
            if (_towerRewardPoolItemsPanel == null) return;
            _towerRewardPoolItemsPanel.Children.Clear();
            foreach (var reward in _towerOfferedRewardPool.OrderBy(entry => R.itemName(entry.Key)))
                _towerRewardPoolItemsPanel.Children.Add(createTowerRewardPoolCard(reward.Key, reward.Value));
            if (_towerOfferedRewardPool.Count == 0)
                _towerRewardPoolItemsPanel.Children.Add(new TextBlock
                {
                    Text = "No item types are currently recorded in the active reward pool.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (System.Windows.Media.Brush)FindResource("AppMutedForegroundBrush")
                });
        }

        private Border createTowerRewardPoolCard(string itemType, int offerState)
        {
            var root = new StackPanel();
            var imageButton = new Button
            {
                Content = new Image { Source = ImageResolver.instance.imageSourceForItem(itemType), Width = 54, Height = 54, Stretch = System.Windows.Media.Stretch.Uniform },
                Width = 74,
                Height = 68,
                Padding = new Thickness(3),
                ToolTip = "Change " + R.itemName(itemType)
            };
            imageButton.Click += (_, __) => chooseTowerRewardPoolItem(itemType);
            root.Children.Add(imageButton);
            root.Children.Add(new TextBlock
            {
                Text = R.itemName(itemType),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 10,
                Width = 92,
                ToolTip = R.itemName(itemType),
                Margin = new Thickness(0, 3, 0, 3)
            });
            root.Children.Add(new TextBlock
            {
                Text = "Gilding: not generated",
                TextAlignment = TextAlignment.Center,
                FontSize = 9,
                Foreground = (System.Windows.Media.Brush)FindResource("AppMutedForegroundBrush"),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var controls = new Grid();
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            var state = new TextBox { Text = offerState.ToString(CultureInfo.InvariantCulture), ToolTip = "Game offer-state value", Padding = new Thickness(4, 2, 4, 2), MinHeight = 26 };
            state.TextChanged += (_, __) =>
            {
                if (int.TryParse(state.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    _towerOfferedRewardPool[itemType] = value;
                    syncTowerRewardPoolJson();
                }
            };
            controls.Children.Add(state);
            var remove = new Button { Content = "×", FontSize = 16, Padding = new Thickness(0), MinWidth = 26, MinHeight = 26, Margin = new Thickness(3, 0, 0, 0), ToolTip = "Remove from pool" };
            remove.Click += (_, __) =>
            {
                _towerOfferedRewardPool.Remove(itemType);
                syncTowerRewardPoolJson();
                refreshTowerRewardPoolEditor();
            };
            Grid.SetColumn(remove, 1);
            controls.Children.Add(remove);
            root.Children.Add(controls);
            return new Border
            {
                Child = root,
                Width = 104,
                Margin = new Thickness(0, 0, 7, 7),
                Padding = new Thickness(5),
                Background = (System.Windows.Media.Brush)FindResource("AppElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AppBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };
        }

        private void chooseTowerRewardPoolItem(string? oldItemType)
        {
            var selection = WindowFactory.createSelectionWindow();
            selection.loadItems(oldItemType);
            selection.onSelection = selectedType =>
            {
                if (string.IsNullOrWhiteSpace(selectedType)) return;
                int offerState = 0;
                if (oldItemType != null && _towerOfferedRewardPool.TryGetValue(oldItemType, out int previousState))
                {
                    offerState = previousState;
                    _towerOfferedRewardPool.Remove(oldItemType);
                }
                else if (_towerOfferedRewardPool.Count > 0)
                    offerState = _towerOfferedRewardPool.Values.First();
                _towerOfferedRewardPool[selectedType!] = offerState;
                syncTowerRewardPoolJson();
                refreshTowerRewardPoolEditor();
            };
            if (selection is Window window) window.Owner = this;
            selection.Show();
        }

        private void syncTowerRewardPoolJson()
        {
            towerMerchantRewardsTextBox.Text = JsonSerializer.Serialize(
                _towerOfferedRewardPool.OrderBy(entry => entry.Key).ToDictionary(entry => entry.Key, entry => entry.Value),
                new JsonSerializerOptions { WriteIndented = true });
        }

        private Border createTowerServiceCard(string key, MerchantDef merchant)
        {
            var root = new StackPanel { Width = 320 };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bool isCompletionHistory = key.IndexOf("TowerComplete", StringComparison.OrdinalIgnoreCase) >= 0;
            header.Children.Add(new TextBlock
            {
                Text = isCompletionHistory ? "Previous Tower completion loot" : merchantDisplayName(key),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            var interacted = new CheckBox { Content = "Unlocked", IsChecked = merchant.EverInteracted, Style = (Style)FindResource("ToggleSwitch") };
            interacted.IsEnabled = !isCompletionHistory;
            Grid.SetColumn(interacted, 1); header.Children.Add(interacted);
            root.Children.Add(header);

            var restockRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            restockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            restockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            restockRow.Children.Add(new TextBlock { Text = "Times restocked", Foreground = (System.Windows.Media.Brush)FindResource("AppSecondaryForegroundBrush"), VerticalAlignment = VerticalAlignment.Center });
            var restocks = new TextBox { Text = (merchant.Pricing?.TimesRestocked ?? 0).ToString(CultureInfo.InvariantCulture) };
            restocks.IsReadOnly = isCompletionHistory;
            Grid.SetColumn(restocks, 1); restockRow.Children.Add(restocks);
            root.Children.Add(restockRow);

            var stockPanel = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var slotEntry in (merchant.Slots ?? new Dictionary<string, MerchantSlot>()).OrderBy(entry => entry.Key))
            {
                MerchantSlot slot = slotEntry.Value;
                var itemControl = new ItemControl { Width = 54, Height = 54, item = slot.Item };
                var button = new Button { Content = itemControl, Width = 64, Height = 64, Padding = new Thickness(3), Margin = new Thickness(0, 0, 6, 6), ToolTip = "Change " + slotEntry.Key };
                button.IsEnabled = !isCompletionHistory;
                button.Click += (_, __) =>
                {
                    if (slot.Item == null) return;
                    var editor = new MerchantItemEditorWindow(slot.Item,
                        "Edit " + merchantDisplayName(key) + " • " + slotEntry.Key, true) { Owner = this };
                    if (editor.ShowDialog() == true)
                    {
                        slot.Item = editor.EditedItem;
                        itemControl.item = slot.Item;
                        _model.profileModel.notifyChanged();
                        statusTextBlock.Text = merchantDisplayName(key) + " item changed • apply Tower service";
                    }
                };
                stockPanel.Children.Add(button);
            }
            if (stockPanel.Children.Count == 0)
                stockPanel.Children.Add(new TextBlock { Text = "No stocked item slots", Foreground = (System.Windows.Media.Brush)FindResource("AppMutedForegroundBrush") });
            root.Children.Add(stockPanel);
            if (isCompletionHistory)
                root.Children.Add(new TextBlock
                {
                    Text = "Stored by the game from the last completed run; this is not the active run's final reward list.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0),
                    Foreground = (System.Windows.Media.Brush)FindResource("AppMutedForegroundBrush"),
                    FontSize = 10
                });

            var apply = new Button { Content = "Apply Tower Service", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            apply.Click += (_, __) =>
            {
                if (!long.TryParse(restocks.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long count) || count < 0)
                {
                    ModernDialog.Show(this, "Invalid restock count", "Times restocked must be a whole number of zero or greater.", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                merchant.Pricing = merchant.Pricing ?? new MerchantPricing();
                merchant.Pricing.TimesRestocked = count;
                merchant.EverInteracted = interacted.IsChecked == true;
                _model.profileModel.notifyChanged();
                statusTextBlock.Text = merchantDisplayName(key) + " changes pending save";
                showToast("Tower service changes applied — press Save");
            };
            if (!isCompletionHistory) root.Children.Add(apply);

            return new Border
            {
                Child = root,
                Background = (System.Windows.Media.Brush)FindResource("AppElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AppBorderBrush"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7),
                Padding = new Thickness(11), Margin = new Thickness(0, 0, 0, 10)
            };
        }

        private Border createMerchantItemCard(string type)
        {
            var image = new Image
            {
                Width = 62,
                Height = 62,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Source = ImageResolver.instance.imageSourceForItem(type)
            };
            var panel = new StackPanel { Width = 92 };
            panel.Children.Add(image);
            panel.Children.Add(new TextBlock
            {
                Text = R.itemName(type),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0)
            });
            return new Border
            {
                Child = panel,
                Background = (System.Windows.Media.Brush)FindResource("AppElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AppBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 8, 8)
            };
        }

        private void resetTowerMerchantButton_Click(object sender, RoutedEventArgs e)
        {
            if (_towerEditorSnapshot == null) return;
            towerMerchantInteractionsTextBox.Text = _towerEditorSnapshot.MerchantInteractionsJson;
            towerMerchantRewardsTextBox.Text = _towerEditorSnapshot.OfferedRewardsJson;
            _towerOfferedRewardPool = new Dictionary<string, int>(_towerEditorSnapshot.OfferedRewardPool, StringComparer.OrdinalIgnoreCase);
            refreshTowerRewardPoolEditor();
            towerMerchantStatusText.Text = "Merchant fields reset to the last loaded save data.";
        }

        private async void saveTowerMerchantButton_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = _model.profileModel.filePath;
            if (string.IsNullOrWhiteSpace(filePath) || _towerEditorSnapshot == null) return;
            if (_model.isDirty)
            {
                ModernDialog.Show(this, "Save editor changes first", "Save or discard the main editor's pending changes before writing Tower merchant data.",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (isMinecraftDungeonsRunning())
            {
                ModernDialog.Show(this, "Close Minecraft Dungeons", "Tower merchant modification is blocked while the game is running.",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ModernDialog.Show(this, "Save Tower merchant changes",
                "Replace the recorded Tower merchant interactions and offered rewards?",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning,
                "Both JSON structures will be validated first, and a timestamped backup will be created.") != MessageBoxResult.OK) return;

            saveTowerMerchantButton.IsEnabled = false;
            try
            {
                await TowerLiveSaveReader.SaveMerchantChangesAsync(filePath!,
                    towerMerchantInteractionsTextBox.Text, towerMerchantRewardsTextBox.Text);
                await _model.handleFileOpenAsync(filePath!);
                await reloadTowerMerchantAsync(filePath!);
                statusTextBlock.Text = "Tower merchant changes saved • backup created";
                showToast("Tower merchant changes saved — backup created");
            }
            catch (Exception exception)
            {
                EventLogger.logError("Tower merchant save: " + exception);
                showError("Tower merchant changes could not be saved safely.\n\n" + exception.Message);
                saveTowerMerchantButton.IsEnabled = true;
            }
        }

        private void campMerchantsTabItem_Selected(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, sender)) refreshCampMerchantList();
        }

        private void refreshCampMerchantList()
        {
            if (campMerchantList == null) return;
            string? previous = _selectedCampMerchantKey;
            campMerchantList.Items.Clear();
            var merchants = _model.profileModel.profile.value?.MerchantData;
            if (merchants == null) return;
            foreach (var entry in merchants
                .Where(entry => isCharacterMerchant(entry.Key))
                .OrderBy(entry => merchantDisplayName(entry.Key)))
            {
                var item = new ListBoxItem { Content = merchantDisplayName(entry.Key), Tag = entry.Key };
                campMerchantList.Items.Add(item);
                if (string.Equals(previous, entry.Key, StringComparison.OrdinalIgnoreCase)) item.IsSelected = true;
            }
            if (campMerchantList.SelectedItem == null && campMerchantList.Items.Count > 0)
                ((ListBoxItem)campMerchantList.Items[0]).IsSelected = true;
        }

        private static string merchantDisplayName(string key)
        {
            string name = key.Replace("Default__", string.Empty).Replace("MerchantDef", string.Empty).Replace('_', ' ');
            return string.Concat(name.Select((character, index) => index > 0 && char.IsUpper(character) && !char.IsUpper(name[index - 1]) ? " " + character : character.ToString()));
        }

        private static bool isCharacterMerchant(string key)
        {
            string displayName = merchantDisplayName(key);
            return displayName.IndexOf("Tower", StringComparison.OrdinalIgnoreCase) < 0
                && !string.Equals(displayName.Replace(" ", string.Empty), "StorageChest", StringComparison.OrdinalIgnoreCase);
        }

        private void campMerchantList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(campMerchantList.SelectedItem is ListBoxItem selected) || !(selected.Tag is string key)) return;
            var merchants = _model.profileModel.profile.value?.MerchantData;
            if (merchants == null || !merchants.TryGetValue(key, out MerchantDef merchant)) return;
            _selectedCampMerchantKey = key;
            campMerchantTitleText.Text = merchantDisplayName(key);
            campMerchantInteractedCheckBox.IsChecked = merchant.EverInteracted;
            campMerchantRestocksTextBox.Text = (merchant.Pricing?.TimesRestocked ?? 0).ToString(CultureInfo.InvariantCulture);
            campMerchantSlotsPanel.Children.Clear();
            _campSlotEditors.Clear();
            foreach (var slotEntry in (merchant.Slots ?? new Dictionary<string, MerchantSlot>()).OrderBy(entry => entry.Key))
                addCampMerchantSlot(slotEntry.Key, slotEntry.Value);
            if (_campSlotEditors.Count == 0)
                campMerchantSlotsPanel.Children.Add(new TextBlock { Text = "This merchant has no stored slots.", Style = (Style)FindResource("BodyText") });
            applyCampMerchantButton.IsEnabled = true;
        }

        private void addCampMerchantSlot(string slotName, MerchantSlot slot)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var itemControl = new ItemControl { Width = 72, Height = 72, item = slot.Item };
            var changeItemButton = new Button { Content = itemControl, Padding = new Thickness(2), ToolTip = "Change stocked item" };
            changeItemButton.Click += (_, __) =>
            {
                if (slot.Item == null) return;
                var editor = new MerchantItemEditorWindow(slot.Item, "Edit " + slotName + " stock", true) { Owner = this };
                if (editor.ShowDialog() == true)
                {
                    slot.Item = editor.EditedItem;
                    itemControl.item = slot.Item;
                    _model.profileModel.notifyChanged();
                    statusTextBlock.Text = "Camp merchant item changed • apply merchant changes";
                }
            };
            row.Children.Add(changeItemButton);
            var details = new Grid { Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(details, 1);
            details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.Children.Add(new TextBlock { Text = slotName, FontWeight = FontWeights.SemiBold });
            var reserved = new CheckBox { Content = "Reserved", IsChecked = slot.Reserved, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(reserved, 2); details.Children.Add(reserved);
            var price = new TextBox { Text = slot.PriceMultiplier.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 7, 0, 0), ToolTip = "Price multiplier" };
            Grid.SetRow(price, 1); details.Children.Add(price);
            var rebate = new TextBox { Text = slot.RebateFraction.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 7, 0, 0), ToolTip = "Rebate fraction (0 to 1)" };
            Grid.SetRow(rebate, 1); Grid.SetColumn(rebate, 2); details.Children.Add(rebate);
            row.Children.Add(details);
            campMerchantSlotsPanel.Children.Add(new Border
            {
                Child = row, Background = (System.Windows.Media.Brush)FindResource("AppElevatedBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AppBorderBrush"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(9)
            });
            _campSlotEditors.Add(new CampSlotEditor { Slot = slot, PriceMultiplier = price, RebateFraction = rebate, Reserved = reserved });
        }

        private void applyCampMerchantButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedCampMerchantKey)) return;
            var merchants = _model.profileModel.profile.value?.MerchantData;
            if (merchants == null || !merchants.TryGetValue(_selectedCampMerchantKey!, out MerchantDef merchant)) return;
            if (!long.TryParse(campMerchantRestocksTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long restocks) || restocks < 0)
            {
                ModernDialog.Show(this, "Invalid restock count", "Times restocked must be a whole number of zero or greater.", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            foreach (CampSlotEditor editor in _campSlotEditors)
            {
                if (!double.TryParse(editor.PriceMultiplier.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double price) || price < 0
                    || !double.TryParse(editor.RebateFraction.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double rebate) || rebate < 0 || rebate > 1)
                {
                    ModernDialog.Show(this, "Invalid merchant pricing", "Price multipliers must be zero or greater, and rebate fractions must be between 0 and 1.", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                editor.Slot.PriceMultiplier = price;
                editor.Slot.RebateFraction = rebate;
                editor.Slot.Reserved = editor.Reserved.IsChecked == true;
            }
            merchant.Pricing = merchant.Pricing ?? new MerchantPricing();
            merchant.Pricing.TimesRestocked = restocks;
            merchant.EverInteracted = campMerchantInteractedCheckBox.IsChecked == true;
            _model.profileModel.notifyChanged();
            statusTextBlock.Text = "Camp merchant changes pending save";
            showToast("Camp merchant changes applied — press Save");
        }

        private void moreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void saveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ApplicationCommands.SaveAs.Execute(null, this);
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current?.Shutdown();
        }

        private void OnTabSelected(object sender, RoutedEventArgs e)
        {
            var tab = sender as TabItem;
            if (tab != null)
            {
                // this tab is selected!
                this._model.profileModel.mainEquipmentModel.updateEnchantmentPoints();
                this._model.profileModel.storageChestEquipmentModel.updateEnchantmentPoints();
            }
        }

        #endregion

        #region Setup

        private void refreshRecentFilesList()
        {
            recentFilesMenuItem.Items.Clear();
            foreach(var menuItem in _model.recentFilesInfos.Select(createRecentFileMenuItem))
            {
                recentFilesMenuItem.Items.Add(menuItem);
            }
            recentFilesMenuItem.IsEnabled = recentFilesMenuItem.Items.Count > 0;
        }

        private MenuItem createRecentFileMenuItem(FileInfo fileInfo)
        {
            var menuItem = new MenuItem();
            menuItem.Header = fileInfo.Name;
            menuItem.CommandParameter = fileInfo;
            menuItem.Command = new RelayCommand<FileInfo>(openRecentFileCommandBinding_Executed);
            return menuItem;
        }

        private void translateStaticStrings()
        {
            inventoryTabItem.Header = R.getString("Quickaction_inventory") ?? R.INVENTORY;
            statsTabItem.Header = R.STATS_COUNTERS;
            chestTabItem.Header = R.getString("StorageChest") ?? R.CHEST;
        }

        private void createLangMenuItems()
        {
            langMenuItem.Items.Clear();
            var noneMenuItem = createLangMenuItem(R.getString("rebind_none") ?? R.NONE);
            langMenuItem.Items.Add(noneMenuItem);
            langMenuItem.Items.Add(new Separator());
            foreach(var menuItem in LanguageResolver.instance.localizationOptions.Select(createLangMenuItem))
            {
                langMenuItem.Items.Add(menuItem);
            }
        }

        private MenuItem createLangMenuItem(string lang)
        {
            var specificLangMenuItem = new MenuItem();
            string header;
            try
            {
                header = CultureInfo.GetCultureInfo(lang).NativeName;
            }
            catch
            {
                header = lang;
            }
            specificLangMenuItem.Header = header;
            specificLangMenuItem.IsChecked = AppModel.currentLangSpecifier == lang;
            specificLangMenuItem.CommandParameter = lang;
            specificLangMenuItem.Command = new RelayCommand<string>(languageSelectedMenuItem_Click);
            return specificLangMenuItem;
        }

#endregion

#region Version Check

        private async void checkForNewVersionAsync()
        {
            await Config.instance.downloadAsync();
            if (Config.instance.isNewBetaVersionAvailable())
            {
                updateMenuItem.Header = R.BETA_UPDATE_MENU_ITEM_HEADER;
                updateMenuItem.Visibility = Visibility.Visible;
            }
            else if (Config.instance.isNewStableVersionAvailable())
            {
                updateMenuItem.Header = R.STABLE_UPDATE_MENU_ITEM_HEADER;
                updateMenuItem.Visibility = Visibility.Visible;
            }
            else
            {
                updateMenuItem.Visibility = Visibility.Collapsed;
            }
        }

#endregion

#region User Input Methods

#region Keyboard Captures

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            //Capture the delete key
            if (e.Key == Key.Delete)
            {
                if (inventoryTab.IsVisible && !(Keyboard.FocusedElement is TextBox))
                {
                    inventoryTab.deleteCurrentSelectedItem();
                }
                else if (chestTab.IsVisible && !(Keyboard.FocusedElement is TextBox))
                {
                    chestTab.deleteCurrentSelectedItem();
                }
            }
        }

#endregion

#region Menu Items

        private void exitCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            EventLogger.logEvent("exitCommandBinding_Executed");
            Application.Current?.Shutdown();
        }

        private void relaunchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EventLogger.logEvent("relaunchMenuItem_Click");
            onRelaunch?.Invoke();
        }

        private async void openCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            EventLogger.logEvent("openCommandBinding_Executed");
            var openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.Filter = constructOpenFileDialogFilterString(ProfileViewModel.supportedFileTypesDict);
            openFileDialog.FilterIndex = 0;
            if(!string.IsNullOrWhiteSpace(_model.profileModel.filePath))
            {
                var directory = Path.GetDirectoryName(_model.profileModel.filePath!);
                openFileDialog.InitialDirectory = directory;
            }
            else
            {
                openFileDialog.InitialDirectory = Constants.FILE_DIALOG_INITIAL_DIRECTORY;
            }
            if (openFileDialog.ShowDialog() == true)
            {
                if (await handleUnsavedChangesAsync())
                    await handleFileOpenAsync(openFileDialog.FileName);
            }
        }

        private async void saveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            EventLogger.logEvent("saveAsCommandBinding_Executed");
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = constructOpenFileDialogFilterString(ProfileViewModel.supportedFileTypesDict);
            saveFileDialog.FilterIndex = 0;
            saveFileDialog.InitialDirectory = Path.GetDirectoryName(_model.profileModel.filePath!); //Constants.FILE_DIALOG_INITIAL_DIRECTORY;
            if (saveFileDialog.ShowDialog() == true)
            {
                await handleFileSaveAsync(saveFileDialog.FileName);
            }
        }

        private async void saveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            EventLogger.logEvent("saveCommandBinding_Executed");
            await handleFileSaveAsync(_model.profileModel.filePath);
        }

        private async void restoreBackupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var targetPath = _model.profileModel.filePath;
            if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
            {
                showError("Open a character save before restoring a backup.");
                return;
            }
            if (!await handleUnsavedChangesAsync()) return;

            var browser = WindowFactory.createBackupBrowserWindow(targetPath!);
            if (browser.ShowDialog() != true || string.IsNullOrWhiteSpace(browser.SelectedBackupPath)) return;

            var backup = new FileInfo(browser.SelectedBackupPath);
            if (!backup.Exists)
            {
                showError("The selected backup no longer exists. Refresh the backup browser and try again.");
                return;
            }
            var confirmation = ModernDialog.Show(this, "Restore backup",
                $"Restore {backup.Name}? The current save will be backed up first.",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                $"Backup size: {backup.Length:N0} bytes\nCreated: {backup.LastWriteTime:g}\nPath: {backup.FullName}");
            if (confirmation != MessageBoxResult.OK) return;

            showBusyIndicator();
            try
            {
                await BackupService.RestoreBackupAsync(backup.FullName, targetPath!,
                    Math.Max(1, MCDSaveEdit.Properties.Settings.Default.BackupRetentionCount));
                await _model.handleFileOpenAsync(targetPath!);
                updateTitleUI();
                refreshRecentFilesList();
                showToast("Backup restored safely");
            }
            catch (Exception exception)
            {
                EventLogger.logError($"Backup restore failed: {exception.GetType().Name}: {exception.Message}");
                showError("The backup could not be restored safely.\n\n" + exception.Message);
            }
            finally
            {
                closeBusyIndicator();
            }
        }

        private async void openRecentFileCommandBinding_Executed(FileInfo fileInfo)
        {
            if (await handleUnsavedChangesAsync())
                await handleFileOpenAsync(fileInfo.FullName);
        }

        private void languageSelectedMenuItem_Click(string langSpecifier)
        {
            EventLogger.logEvent("languageSelectedMenuItem_Click", new Dictionary<string, object> { { "langSpecifier", langSpecifier } });
            AppModel.loadLanguageStrings(langSpecifier);
            onReload?.Invoke(_model.profileModel.filePath, _model.profileModel.profile.value);
        }

        private void aboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EventLogger.logEvent("aboutMenuItem_Click");
            var aboutWindow = WindowFactory.createAboutWindow();
            aboutWindow.ShowDialog();
        }

        private void settingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EventLogger.logEvent("settingsMenuItem_Click");
            mainTabControl.SelectedIndex = 6;
            settingsNavigationButton.IsChecked = true;
            loadEmbeddedSettings();
            statusTextBlock.Text = "Settings page";
        }

        private void openLogsFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(EventLogger.LogsFolder);
            Process.Start("explorer.exe", $"\"{EventLogger.LogsFolder}\"");
        }

        private void copyDiagnosticInformationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(DiagnosticService.CreateReport(_model));
            showToast("Diagnostics copied — save contents and keys excluded");
        }

        private void validateCurrentSaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var profile = _model.profileModel.profile.value;
            if (profile == null)
            {
                showError("Open a character save before running validation.");
                return;
            }

            var settings = MCDSaveEdit.Properties.Settings.Default;
            if (!Enum.TryParse(settings.SaveValidationStrictness, out SaveValidationStrictness strictness))
                strictness = SaveValidationStrictness.Standard;
            var report = SaveValidator.Validate(profile, strictness);
            homeValidationText.Text = !report.HasMessages
                ? "Valid"
                : $"{report.Issues.Count} issue(s)";
            if (!report.HasMessages)
            {
                showToast("Validation complete — no issues found");
                return;
            }
            WindowFactory.createValidationWindow(report, false).ShowDialog();
        }

        private void updateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EventLogger.logEvent("updateMenuItem_Click");
            Process.Start(Config.instance.newVersionDownloadURL());
        }

#endregion

#region File Drop Capture

        private async void window_File_Drop(object sender, DragEventArgs e)
        {
            EventLogger.logEvent("window_File_Drop");
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                if (await handleUnsavedChangesAsync())
                    await handleFileOpenAsync(files[0]);
            }
            else
            {
                showError(R.FILE_DROP_ERROR_MESSAGE);
            }
        }

#endregion

#endregion

#region Helper Functions

        private string constructOpenFileDialogFilterString(Dictionary<string, string> dict)
        {
            return string.Join("|", dict.Select(x => string.Join("|", string.Format("{0} ({1})", x.Value, x.Key), x.Key)));
        }

        public async Task handleFileOpenAsync(string? fileName)
        {
            if(string.IsNullOrWhiteSpace(fileName)) { return; }
            if (!File.Exists(fileName))
            {
                showError(R.FILE_DOESNT_EXIST_ERROR_MESSAGE);
                return;
            }
            showBusyIndicator();
            string extension = Path.GetExtension(fileName!);
            EventLogger.logEvent("handleFileOpenAsync", new Dictionary<string, object>() { { "extension", extension } });
                await _model.handleFileOpenAsync(fileName!);
                updateTitleUI();
                refreshRecentFilesList();
                statusTextBlock.Text = $"Loaded {Path.GetFileName(fileName)}";
                if (_model.profileModel.profile.value != null) showToast("Save loaded successfully");
                closeBusyIndicator();
        }

        private async Task<bool> handleFileSaveAsync(string? fileName)
        {
            if (_model.profileModel.profile.value == null || string.IsNullOrWhiteSpace(fileName)) { return false; }

            var profile = _model.profileModel.profile.value;
            var settings = MCDSaveEdit.Properties.Settings.Default;
            if (settings.SaveValidationEnabled)
            {
                if (!Enum.TryParse(settings.SaveValidationStrictness, out SaveValidationStrictness strictness))
                    strictness = SaveValidationStrictness.Standard;
                var report = SaveValidator.Validate(profile, strictness);
                if ((report.HasErrors || report.HasWarnings) && WindowFactory.createValidationWindow(report).ShowDialog() != true)
                    return false;
            }

            if (File.Exists(fileName) && settings.ConfirmBeforeOverwrite)
            {
                var backupNotice = settings.AutomaticBackupsEnabled
                    ? "A timestamped backup will be created first."
                    : "Automatic backups are disabled in Settings.";
                var confirmation = ModernDialog.Show(this, "Confirm save",
                    $"Overwrite {Path.GetFileName(fileName)}?",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning,
                    backupNotice);
                if (confirmation != MessageBoxResult.OK) return false;
            }

            showBusyIndicator();
            try
            {
                string extension = Path.GetExtension(fileName!);
                EventLogger.logEvent("handleFileSaveAsync", new Dictionary<string, object>() { { "extension", extension } });
                await _model.handleFileSaveAsync(fileName!, profile!);
                updateTitleUI();
                refreshRecentFilesList();
                statusTextBlock.Text = MCDSaveEdit.Properties.Settings.Default.AutomaticBackupsEnabled
                    ? "Saved successfully • backup created"
                    : "Saved successfully";
                showToast(MCDSaveEdit.Properties.Settings.Default.AutomaticBackupsEnabled ? "Saved successfully — backup created" : "Saved successfully");
                return true;
            }
            catch (Exception exception)
            {
                EventLogger.logError($"Save failed: {exception.GetType().Name}: {exception.Message}");
                showError("The save could not be completed safely. If atomic replacement already completed, the previous file is available in the Backups folder.\n\n" + exception.Message);
                return false;
            }
            finally
            {
                closeBusyIndicator();
            }
        }

        private async Task<bool> handleUnsavedChangesAsync()
        {
            if (!_model.isDirty) return true;

            var result = ModernDialog.Show(this, "Unsaved changes",
                "The current character has changes that have not been saved.",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                "Save writes the current changes. Discard continues without saving. Cancel keeps the current character open.");
            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.No) return true;
            return await handleFileSaveAsync(_model.profileModel.filePath);
        }

        private async void mainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_allowClose || !_model.isDirty) return;

            e.Cancel = true;
            if (_closeConfirmationInProgress) return;

            _closeConfirmationInProgress = true;
            try
            {
                if (!await handleUnsavedChangesAsync()) return;

                _allowClose = true;

                // The Discard path completes synchronously, so calling Close() here
                // would re-enter WPF while the original Closing event is still active.
                // Queue the approved close so it runs after that event has unwound.
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (IsLoaded) Close();
                }), DispatcherPriority.Normal);
            }
            finally
            {
                if (!_allowClose) _closeConfirmationInProgress = false;
            }
        }

        private void mainWindow_Closed(object? sender, EventArgs e)
        {
            _toastTimer?.Stop();
            _toastTimer = null;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero) UnregisterHotKey(handle, LiveMonitorHotkeyId);
            _hotkeySource?.RemoveHook(windowMessageHook);
            _hotkeySource = null;
            if (_towerLiveMonitorWindow != null)
            {
                _towerLiveMonitorWindow.Close();
                _towerLiveMonitorWindow = null;
            }
            settingsHost.Content = null;
            _embeddedSettings?.Close();
            _embeddedSettings = null;
            closeBusyIndicator();
        }
        
        private void showBusyIndicator()
        {
            closeBusyIndicator();

            _busyWindow = WindowFactory.createBusyWindow();
            _busyWindow.Owner = this;
            _busyWindow.Show();
        }

        private void closeBusyIndicator()
        {
            if (_busyWindow != null)
            {
                _busyWindow!.Close();
                _busyWindow = null;
            }
        }

        private void showError(string message)
        {
            EventLogger.logEvent("showError", new Dictionary<string, object>() { { "message", message } });
            string[] sections = message.Split(new[] { "\r\n\r\n", "\n\n" }, 2, StringSplitOptions.None);
            ModernDialog.Show(this, "Something went wrong", sections[0], MessageBoxButton.OK, MessageBoxImage.Error,
                sections.Length > 1 ? sections[1] : null);
            closeBusyIndicator();
        }

        private void showToast(string message)
        {
            toastTextBlock.Text = message;
            toastBorder.Visibility = Visibility.Visible;
            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _toastTimer.Tick += (_, __) => { _toastTimer?.Stop(); toastBorder.Visibility = Visibility.Collapsed; };
            _toastTimer.Start();
        }

#endregion
    }
}
