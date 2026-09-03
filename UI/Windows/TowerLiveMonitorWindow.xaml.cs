using MCDSaveEdit.Services;
using MCDSaveEdit.Data;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
#nullable enable

namespace MCDSaveEdit.UI
{
    public partial class TowerLiveMonitorWindow : Window
    {
        private string? _filePath;
        private readonly DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _pollTimer;
        private FileSystemWatcher? _watcher;
        private TowerLiveSnapshot? _snapshot;
        private bool _reading;
        private bool _readAgain;
        private int _changeCount;
        private DateTime _lastSeenWriteUtc;
        private long _lastSeenLength = -1;
        private int? _gameProcessId;
        private TimeSpan _lastProcessCpu;
        private DateTime _lastProcessSampleUtc;
        private DateTime _lastProcessLogUtc;
        private readonly string _sessionDirectory;
        private readonly string _sessionLogPath;
        private StreamWriter? _sessionLogWriter;
        private bool _overlayMode = true;
        private readonly Queue<string> _liveLogLines = new Queue<string>();
        private string _lastProcessState = string.Empty;
        private bool _autoDiscoverSave;
        private DateTime _lastAutoDiscoveryUtc;

        public TowerLiveMonitorWindow(string? filePath = null)
        {
            _autoDiscoverSave = string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath);
            _filePath = _autoDiscoverSave ? findLatestCharacterSave() : filePath;
            InitializeComponent();
            updateAttachedFileLabel();
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _debounceTimer.Tick += async (_, __) =>
            {
                _debounceTimer.Stop();
                await RefreshAsync(true);
            };
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += pollTimer_Tick;

            string safeName = _filePath == null ? "ProcessOnly" : Path.GetFileNameWithoutExtension(_filePath);
            _sessionDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                $"LiveMonitor-{DateTime.Now:yyyyMMdd-HHmmss}-{safeName}");
            _sessionLogPath = Path.Combine(_sessionDirectory, "session.jsonl");
            try
            {
                Directory.CreateDirectory(_sessionDirectory);
                Directory.CreateDirectory(Path.Combine(_sessionDirectory, "snapshots"));
                _sessionLogWriter = new StreamWriter(_sessionLogPath, false, new UTF8Encoding(false)) { AutoFlush = true };
                writeSessionEvent(new { eventType = "session_started", at = DateTimeOffset.Now, saveFile = _filePath == null ? null : Path.GetFileName(_filePath) });
            }
            catch (Exception exception)
            {
                EventLogger.logError("Live monitor export could not start: " + exception.Message);
            }
            sessionLogPathText.Text = _sessionLogWriter == null ? "Log export unavailable" : _sessionLogPath;
            exportFolderPathText.Text = _sessionLogWriter == null ? "Unavailable" : _sessionDirectory;
            exportFolderPathText.ToolTip = exportFolderPathText.Text;
            addLiveLog("MONITOR", "Session started. Export folder: " + _sessionDirectory);
            if (_autoDiscoverSave)
                addLiveLog("DISCOVERY", _filePath == null
                    ? "Watching the Minecraft Dungeons save folders for an active character."
                    : "Automatically attached the most recently updated character: " + _filePath);
            updateTabVisibility();
        }

        private async void window_Loaded(object sender, RoutedEventArgs e)
        {
            StartWatcher();
            _pollTimer.Start();
            sampleGameProcess();
            await RefreshAsync(false);
        }

        private void StartWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath)) return;
            string? directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory)) return;
            _watcher = new FileSystemWatcher(directory, Path.GetFileName(_filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _watcher.Changed += saveFile_Changed;
            _watcher.Created += saveFile_Changed;
            _watcher.Renamed += saveFile_Changed;
        }

        private void saveFile_Changed(object sender, FileSystemEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(scheduleRefresh));
        }

        private void scheduleRefresh()
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void pollTimer_Tick(object? sender, EventArgs e)
        {
            sampleGameProcess();
            if (_autoDiscoverSave && (DateTime.UtcNow - _lastAutoDiscoveryUtc).TotalSeconds >= 2)
            {
                _lastAutoDiscoveryUtc = DateTime.UtcNow;
                string? discovered = findLatestCharacterSave();
                if (!string.Equals(discovered, _filePath, StringComparison.OrdinalIgnoreCase))
                    setAttachedSave(discovered, true);
            }
            if (string.IsNullOrWhiteSpace(_filePath)) return;
            try
            {
                var info = new FileInfo(_filePath);
                if (info.Exists && (info.LastWriteTimeUtc != _lastSeenWriteUtc || info.Length != _lastSeenLength))
                    scheduleRefresh();
            }
            catch { }
        }

        private void sampleGameProcess()
        {
            Process? game = null;
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    if (process.ProcessName.IndexOf("Dungeons", StringComparison.OrdinalIgnoreCase) >= 0
                        && process.ProcessName.IndexOf("MCDSaveEdit", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        long candidateMemory;
                        long selectedMemory;
                        try { candidateMemory = process.WorkingSet64; }
                        catch { candidateMemory = 0; }
                        try { selectedMemory = game?.WorkingSet64 ?? -1; }
                        catch { selectedMemory = -1; }
                        if (game == null || candidateMemory > selectedMemory)
                        {
                            game?.Dispose();
                            game = process;
                        }
                        else process.Dispose();
                        continue;
                    }
                    process.Dispose();
                }
                if (game == null)
                {
                    _gameProcessId = null;
                    processStateText.Text = "Not running";
                    processCpuText.Text = "0%";
                    processMemoryText.Text = "—";
                    processThreadsText.Text = "—";
                    processDetailsText.Text = "Waiting for Minecraft Dungeons…";
                    if (_lastProcessState != "stopped")
                    {
                        _lastProcessState = "stopped";
                        addLiveLog("PROCESS", "Minecraft Dungeons is not running.");
                    }
                    return;
                }

                DateTime now = DateTime.UtcNow;
                TimeSpan cpu = game.TotalProcessorTime;
                double cpuPercent = 0;
                if (_gameProcessId == game.Id && _lastProcessSampleUtc != default)
                {
                    double elapsedMs = (now - _lastProcessSampleUtc).TotalMilliseconds;
                    if (elapsedMs > 0)
                        cpuPercent = Math.Max(0, Math.Min(100,
                            (cpu - _lastProcessCpu).TotalMilliseconds / elapsedMs / Environment.ProcessorCount * 100));
                }
                _gameProcessId = game.Id;
                _lastProcessCpu = cpu;
                _lastProcessSampleUtc = now;
                processStateText.Text = "Running";
                processStateText.Foreground = (Brush)FindResource("AppSuccessBrush");
                processCpuText.Text = cpuPercent.ToString("N1") + "%";
                processMemoryText.Text = (game.WorkingSet64 / 1048576.0).ToString("N0") + " MB";
                processThreadsText.Text = game.Threads.Count.ToString("N0");
                string path;
                try { path = game.MainModule?.FileName ?? "Path unavailable"; }
                catch { path = "Path unavailable"; }
                TimeSpan uptime;
                try { uptime = DateTime.Now - game.StartTime; }
                catch { uptime = TimeSpan.Zero; }
                processDetailsText.Text = $"PID {game.Id}  •  uptime {uptime:dd\\.hh\\:mm\\:ss}\n{path}";

                if (_lastProcessState != "running:" + game.Id)
                {
                    _lastProcessState = "running:" + game.Id;
                    addLiveLog("PROCESS", $"Minecraft Dungeons detected (PID {game.Id}).");
                }

                if ((now - _lastProcessLogUtc).TotalSeconds >= 5)
                {
                    _lastProcessLogUtc = now;
                    writeSessionEvent(new
                    {
                        eventType = "process_sample",
                        at = DateTimeOffset.Now,
                        pid = game.Id,
                        cpuPercent,
                        workingSetBytes = game.WorkingSet64,
                        threads = game.Threads.Count,
                        uptimeSeconds = uptime.TotalSeconds
                    });
                    addLiveLog("SAMPLE", $"CPU {cpuPercent:N1}% • memory {game.WorkingSet64 / 1048576.0:N0} MB • {game.Threads.Count:N0} threads");
                }
            }
            catch (Exception exception)
            {
                processStateText.Text = "Unavailable";
                processDetailsText.Text = exception.Message;
            }
            finally { game?.Dispose(); }
        }

        private async Task RefreshAsync(bool triggeredByChange)
        {
            if (_reading)
            {
                _readAgain = true;
                return;
            }
            _reading = true;
            statusText.Text = "Reading latest save write…";
            statusDot.Fill = (Brush)FindResource("AppWarningBrush");
            try
            {
                string attachedPath = _filePath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(attachedPath) || !File.Exists(attachedPath))
                {
                    statusText.Text = "Live game process monitor";
                    writeInfoText.Text = "Open a character at any time to attach save-data monitoring";
                    statusDot.Fill = (Brush)FindResource(_gameProcessId.HasValue ? "AppSuccessBrush" : "AppWarningBrush");
                    return;
                }
                TowerLiveSnapshot next = await TowerLiveSaveReader.ReadAsync(attachedPath);
                bool contentChanged = _snapshot == null || !string.Equals(_snapshot.FullJson, next.FullJson, StringComparison.Ordinal);
                _snapshot = next;
                var info = new FileInfo(_filePath);
                _lastSeenWriteUtc = info.LastWriteTimeUtc;
                _lastSeenLength = info.Length;

                characterNameMetricText.Text = next.CharacterName;
                gearPowerMetricText.Text = next.TotalGearPower.ToString("N0");
                experienceMetricText.Text = next.Experience.ToString("N0");
                saveSizeMetricText.Text = (next.FileSize / 1024.0).ToString("N1") + " KB";
                inventoryCountMetricText.Text = next.InventoryItemCount.ToString("N0");
                storageCountMetricText.Text = next.StorageItemCount.ToString("N0");
                merchantCountMetricText.Text = next.MerchantCount.ToString("N0");
                mobKillCountMetricText.Text = next.MobKillTypeCount.ToString("N0");
                currencyMetricText.Text = next.CurrencySummary;
                floorMetricText.Text = next.CurrentFloor + " / " + Math.Max(0, next.TotalFloors - 1);
                livesMetricText.Text = next.LivesLost.ToString();
                bossesMetricText.Text = next.BossesKilled + " / 3";
                seedMetricText.Text = next.TowerSeed.ToString();
                difficultyMetricText.Text = next.Difficulty.Replace("Difficulty_", "Difficulty ") + "  •  " + next.Threat.Replace("Threat_", "Threat ");
                arrowsMetricText.Text = next.Arrows.ToString("N0");
                pointsMetricText.Text = next.EnchantmentPoints.ToString("N0");
                itemsMetricText.Text = next.PlayerItemsSummary;
                towerIdText.Text = "TOWER ID  " + next.TowerId + (next.RunFinished ? "  •  FINISHED" : next.CurrentFloorCompleted ? "  •  FLOOR COMPLETE" : "  •  ACTIVE");
                renderSelectedJson();

                if (triggeredByChange && contentChanged) _changeCount++;
                changeCountText.Text = _changeCount + (_changeCount == 1 ? " update" : " updates");
                statusText.Text = "Live game and character data connected";
                writeInfoText.Text = "Save write " + next.FileModifiedAt.ToString("T") + "  •  read " + next.ReadAt.ToString("T") + "  •  polling every 0.5 s";
                statusDot.Fill = (Brush)FindResource("AppSuccessBrush");
                if (contentChanged) exportSaveSnapshot(next);
                if (contentChanged)
                {
                    string character = string.IsNullOrWhiteSpace(next.CharacterName) ? "Unnamed character" : next.CharacterName;
                    string tower = next.HasTowerData
                        ? $" Tower floor {next.CurrentFloor}/{Math.Max(0, next.TotalFloors - 1)}, {next.Difficulty.Replace("Difficulty_", "Difficulty ")}."
                        : " No active Tower run detected.";
                    addLiveLog("SAVE", $"Snapshot captured: {character}, {next.FileSize / 1024.0:N1} KB, inventory {next.InventoryItemCount}, storage {next.StorageItemCount}.{tower}");
                }
            }
            catch (Exception exception)
            {
                statusText.Text = "Waiting for a readable save";
                writeInfoText.Text = exception.Message;
                statusDot.Fill = (Brush)FindResource("AppDangerBrush");
                EventLogger.logError("Live monitor: " + exception);
                writeSessionEvent(new { eventType = "read_error", at = DateTimeOffset.Now, error = exception.Message });
                addLiveLog("ERROR", exception.Message);
            }
            finally
            {
                _reading = false;
                if (_readAgain)
                {
                    _readAgain = false;
                    scheduleRefresh();
                }
            }
        }

        private void exportSaveSnapshot(TowerLiveSnapshot snapshot)
        {
            if (_sessionLogWriter == null) return;
            try
            {
                string fileName = $"save-{_changeCount:D5}-{snapshot.ReadAt:HHmmss-fff}.json";
                string relativePath = Path.Combine("snapshots", fileName);
                File.WriteAllText(Path.Combine(_sessionDirectory, relativePath), snapshot.FullJson, new UTF8Encoding(false));
                writeSessionEvent(new
                {
                    eventType = "save_snapshot",
                    at = DateTimeOffset.Now,
                    fileModifiedAt = snapshot.FileModifiedAt,
                    sizeBytes = snapshot.FileSize,
                    snapshot = relativePath,
                    character = snapshot.CharacterName,
                    inventoryItems = snapshot.InventoryItemCount,
                    storageItems = snapshot.StorageItemCount,
                    towerFloor = snapshot.CurrentFloor
                });
            }
            catch (Exception exception) { EventLogger.logError("Live snapshot export: " + exception.Message); }
        }

        private void writeSessionEvent(object value)
        {
            try { _sessionLogWriter?.WriteLine(JsonSerializer.Serialize(value)); }
            catch { }
        }

        private void addLiveLog(string category, string message)
        {
            _liveLogLines.Enqueue($"{DateTime.Now:HH:mm:ss.fff}  [{category}]  {message}");
            while (_liveLogLines.Count > 300) _liveLogLines.Dequeue();
            liveLogText.Text = string.Join(Environment.NewLine, _liveLogLines);
            liveLogText.ScrollToEnd();
            liveLogText.ScrollToHorizontalOffset(0);
        }

        public void AttachSaveFile(string? filePath)
        {
            string? usablePath = !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) ? filePath : null;
            _autoDiscoverSave = usablePath == null;
            if (_autoDiscoverSave) usablePath = findLatestCharacterSave();
            setAttachedSave(usablePath, _autoDiscoverSave);
        }

        private void setAttachedSave(string? usablePath, bool automatic)
        {
            if (string.Equals(_filePath, usablePath, StringComparison.OrdinalIgnoreCase)) return;
            _filePath = usablePath;
            _lastSeenWriteUtc = default;
            _lastSeenLength = -1;
            updateAttachedFileLabel();
            addLiveLog(automatic ? "DISCOVERY" : "ATTACH", _filePath == null
                ? "No character save found yet; continuing in process-only mode."
                : (automatic ? "Active character changed: " : "Monitoring save: ") + _filePath);
            updateTabVisibility();
            StartWatcher();
            writeSessionEvent(new { eventType = "save_attachment_changed", at = DateTimeOffset.Now, saveFile = _filePath == null ? null : Path.GetFileName(_filePath) });
            scheduleRefresh();
        }

        private void updateAttachedFileLabel()
        {
            fileNameText.Text = _filePath == null
                ? "AUTO DISCOVERY • WAITING FOR CHARACTER SAVE"
                : (_autoDiscoverSave ? "AUTO • " : string.Empty) + Path.GetFileName(_filePath);
        }

        private static string? findLatestCharacterSave()
        {
            try
            {
                string root = Constants.FILE_DIALOG_INITIAL_DIRECTORY;
                if (!Directory.Exists(root)) return null;
                return Directory.EnumerateFiles(root, "*.dat", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .Where(file => file.Exists && file.Length > 0
                        && string.Equals(file.Directory?.Name, "Characters", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Select(file => file.FullName)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        private void renderSelectedJson()
        {
            if (_snapshot == null) return;
            if (monitorTabs.SelectedItem == characterJsonTab) characterJsonText.SetJson(_snapshot.CharacterJson);
            else if (monitorTabs.SelectedItem == towerJsonTab) towerJsonText.SetJson(_snapshot.TowerJson);
            else if (monitorTabs.SelectedItem == inventoryJsonTab) inventoryJsonText.SetJson(_snapshot.InventoryJson);
            else if (monitorTabs.SelectedItem == storageJsonTab) storageJsonText.SetJson(_snapshot.StorageJson);
            else if (monitorTabs.SelectedItem == merchantsJsonTab) merchantsJsonText.SetJson(_snapshot.MerchantsJson);
            else if (monitorTabs.SelectedItem == progressJsonTab) progressJsonText.SetJson(_snapshot.ProgressJson);
            else if (monitorTabs.SelectedItem == rawJsonTab) rawJsonText.SetJson(_snapshot.FullJson);
        }

        private async void refreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync(false);
        private void monitorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) => renderSelectedJson();

        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            string text;
            if (monitorTabs.SelectedItem == liveLogTab) text = liveLogText.Text;
            else if (monitorTabs.SelectedItem == gameProcessTab) text = processDetailsText.Text;
            else if (_snapshot == null) return;
            else if (monitorTabs.SelectedItem == towerSummaryTab || monitorTabs.SelectedItem == towerJsonTab) text = _snapshot.TowerJson;
            else if (monitorTabs.SelectedItem == inventoryJsonTab) text = _snapshot.InventoryJson;
            else if (monitorTabs.SelectedItem == storageJsonTab) text = _snapshot.StorageJson;
            else if (monitorTabs.SelectedItem == merchantsJsonTab) text = _snapshot.MerchantsJson;
            else if (monitorTabs.SelectedItem == progressJsonTab) text = _snapshot.ProgressJson;
            else if (monitorTabs.SelectedItem == rawJsonTab) text = _snapshot.FullJson;
            else text = _snapshot.CharacterJson;
            Clipboard.SetText(text);
            statusText.Text = "Current tab copied";
        }

        private void viewLiveLogButton_Click(object sender, RoutedEventArgs e) => monitorTabs.SelectedItem = liveLogTab;

        private void clearLiveLogButton_Click(object sender, RoutedEventArgs e)
        {
            _liveLogLines.Clear();
            liveLogText.Clear();
            addLiveLog("MONITOR", "Live view cleared. Exported logs were not deleted.");
        }

        private void openLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_sessionDirectory}\"") { UseShellExecute = true }); }
            catch (Exception exception) { statusText.Text = "Could not open log folder: " + exception.Message; }
        }

        private void pinButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            pinButton.Content = Topmost ? "PINNED" : "UNPINNED";
        }

        public void ShowCompactOverlay()
        {
            setOverlayMode(true);
            if (!IsVisible) Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void overlayModeButton_Click(object sender, RoutedEventArgs e) => setOverlayMode(!_overlayMode);

        private void opacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (opacityValueText == null) return;
            Opacity = Math.Max(0.35, Math.Min(1, e.NewValue / 100.0));
            opacityValueText.Text = Math.Round(e.NewValue).ToString("N0") + "%";
        }

        private void setOverlayMode(bool overlay)
        {
            _overlayMode = overlay;
            if (overlay)
            {
                ResizeMode = ResizeMode.NoResize;
                MinWidth = 520;
                MinHeight = 360;
                Width = 620;
                Height = 460;
                Topmost = true;
                overlayModeButton.Content = "EXPAND";
                expandedActionButtons.Visibility = Visibility.Collapsed;
                updateTabVisibility();
            }
            else
            {
                ResizeMode = ResizeMode.NoResize;
                MinWidth = 760;
                MinHeight = 500;
                Width = Math.Max(1100, ActualWidth);
                Height = Math.Max(720, ActualHeight);
                overlayModeButton.Content = "OVERLAY";
                expandedActionButtons.Visibility = Visibility.Visible;
                updateTabVisibility();
            }
            pinButton.Content = Topmost ? "PINNED" : "UNPINNED";
        }

        private void updateTabVisibility()
        {
            bool hasSave = !string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath);
            overviewTab.Visibility = hasSave ? Visibility.Visible : Visibility.Collapsed;
            towerSummaryTab.Visibility = Visibility.Visible;
            if (!hasSave)
            {
                floorMetricText.Text = livesMetricText.Text = bossesMetricText.Text = seedMetricText.Text = "—";
                difficultyMetricText.Text = arrowsMetricText.Text = pointsMetricText.Text = "—";
                itemsMetricText.Text = "Waiting for Minecraft Dungeons to create or update a character save.";
                towerIdText.Text = "Tower data will appear automatically when an active character is detected.";
            }
            Visibility jsonVisibility = hasSave && !_overlayMode ? Visibility.Visible : Visibility.Collapsed;
            characterJsonTab.Visibility = towerJsonTab.Visibility = inventoryJsonTab.Visibility =
                storageJsonTab.Visibility = merchantsJsonTab.Visibility = progressJsonTab.Visibility = rawJsonTab.Visibility = jsonVisibility;

            if (!hasSave && (monitorTabs.SelectedItem == null || monitorTabs.SelectedItem == overviewTab))
                monitorTabs.SelectedItem = gameProcessTab;
            else if (monitorTabs.SelectedItem == null || monitorTabs.SelectedItem == gameProcessTab)
                monitorTabs.SelectedItem = overviewTab;
        }

        private void titleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else DragMove();
        }

        private void minimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void closeButton_Click(object sender, RoutedEventArgs e) => Close();

        private void window_Closed(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            _pollTimer.Stop();
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            writeSessionEvent(new { eventType = "session_ended", at = DateTimeOffset.Now, updates = _changeCount });
            _sessionLogWriter?.Dispose();
            _sessionLogWriter = null;
        }
    }
}
