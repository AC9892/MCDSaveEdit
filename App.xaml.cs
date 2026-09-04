using MCDSaveEdit.Data;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Services;
using MCDSaveEdit.UI;
using MCDSaveEdit.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
#nullable enable

namespace MCDSaveEdit
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private readonly AppModel _model = new AppModel();
        private readonly MultiTextWriter _outputWriter = new MultiTextWriter();
        private readonly LoadingViewModel _loadingModel = new LoadingViewModel();

        private ControlWriter? _controlWriter = null;
        private SplashWindow? _splashWindow = null;
        private Window? _busyWindow = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.Initialize();
            _outputWriter.addWriter(Console.Out);
            Console.SetOut(_outputWriter);

            EventLogger.init();
            DispatcherUnhandledException += onDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += onUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += onUnhandledException;

            showSplashWindowReplacingOldWindow();

            var args = e.Args;
            _ = Application.Current?.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate {
                startAsync(args);
            }));
        }

        private async void startAsync(string[] args)
        {
            MainThreadConsoleWriteLine($"{Constants.APPLICATION_NAME} {Constants.CURRENT_VERSION}");
            
            string? fileName = args.LastOrDefault();
            if(!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
            {
                string extension = Path.GetExtension(fileName!);
                EventLogger.logEvent("handleFileOpenAsync", new Dictionary<string, object>() { { "extension", extension } });
                await _model.mainModel.handleFileOpenAsync(fileName!);
            }

            bool skipGameContent = args.Contains("SKIP_GAME_CONTENT");
            if (skipGameContent)
            {
                showMainWindow();
            }
            else
            {
                bool askForGameContentLocation = args.Contains("ASK_FOR_GAME_CONTENT_LOCATION");
                await loadAsync(askForGameContentLocation);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DispatcherUnhandledException -= onDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException -= onUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= onUnhandledException;
            EventLogger.dispose();
            ThemeManager.Shutdown();
            _outputWriter.Dispose();
            base.OnExit(e);
        }

        private async Task loadAsync(bool askForGameContentLocation)
        {
            MainThreadConsoleWriteLine("Searching for pak files...");
            bool canContinue = true;
            //check default install locations
            string? paksFolderPath = _model.usableGameContentIfExists();
            if (askForGameContentLocation || string.IsNullOrWhiteSpace(paksFolderPath))
            {
                //show dialog asking for install location
                canContinue = showGameFilesWindow(ref paksFolderPath);
            }

            if (!string.IsNullOrWhiteSpace(paksFolderPath))
            {
                MainThreadConsoleWriteLine($"Pak files path: {paksFolderPath}");
                try
                {
                    await loadGameContentAsync(paksFolderPath!);
                }
                catch (Exception e)
                {
                    EventLogger.logException("Failed to load game content", e);
                    //Clear the path saved in the registry because it might be the cause of the exception
                    _model.unloadGameContent();

                    var title = "Game content could not be loaded";
                    var result = ModernDialog.Show(_splashWindow, title,
                        "MCDSaveEdit can continue in limited mode, but item images and game-data names may be unavailable.",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning,
                        $"{e}\n\n{R.PLEASE_HAVE_LATEST_VERSION}",
                        "Continue without game files", "Exit");
                    canContinue = result == MessageBoxResult.Yes || result == MessageBoxResult.OK;
                }
            }
            else
            {
                //Clear the path saved in the registry
                _model.unloadGameContent();
            }

            if (canContinue == false)
            {
                //User opted to Exit
                _splashWindow?.Close();
                closeBusyIndicator();
                this.MainWindow?.Close();
                this.Shutdown();
                return;
            }

            showMainWindow();
        }

        private void MainThreadConsoleWriteLine(string str)
        {
            this.ExecuteOnMainThread(delegate {
                Console.WriteLine(str);
            });
        }

        private bool showGameFilesWindow(ref string? selectedPath)
        {
            EventLogger.logEvent("showGameFilesWindow");
            var gameFilesWindow = WindowFactory.createGameFilesWindow(selectedPath, allowNoContent: true);
            gameFilesWindow.ShowDialog();
            var gameFilesWindowResult = gameFilesWindow.result;
            switch (gameFilesWindowResult)
            {
                case GameFilesWindow.GameFilesWindowResult.exit:
                    selectedPath = null;
                    return false;
                case GameFilesWindow.GameFilesWindowResult.useSelectedPath:
                    selectedPath = gameFilesWindow.selectedPath!;
                    return true;
                case GameFilesWindow.GameFilesWindowResult.noContent:
                    selectedPath = null;
                    return true;
            }
            throw new NotImplementedException();
        }

        private async Task<bool> loadGameContentAsync(string paksFolderPath)
        {
            showBusyIndicator();
            _model.initPakReader();
            await _model.loadGameContentAsync(paksFolderPath);
            await preloadImages();
            return true;
        }

        private async Task<bool> preloadImages()
        {
            await Task.Run(() => {
                MainThreadConsoleWriteLine("Loading UI images...");
                InventoryTab.preload();
                EquipmentScreen.preload();
                ItemListScreen.preload();
                ItemControl.preload();
#if !HIDE_CHEST_TAB
                MainThreadConsoleWriteLine("Loading Chest images...");
                ChestTab.preload();
#endif

                MainThreadConsoleWriteLine("Loading Equipment images...");
                BaseSelectionWindow.preload();
            });
            return true;
        }

        private void onDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            EventLogger.logException("Unhandled UI exception", e.Exception);
            closeBusyIndicator();
            ModernDialog.Show(MainWindow, "Something went wrong",
                "An unexpected interface error occurred. Your source save was not changed unless a save had already completed.",
                MessageBoxButton.OK, MessageBoxImage.Error,
                $"{e.Exception}\n\nUse More > Open Logs Folder for the local diagnostic log.");
            e.Handled = true;
        }

        private void onUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            EventLogger.logException("Unobserved background exception", e.Exception);
            e.SetObserved();
        }

        private void onUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
                EventLogger.logException("Fatal application exception", exception);
            else
                EventLogger.logError("Fatal application exception with no Exception object");
        }

        private void showMainWindow()
        {
            EventLogger.logEvent("showMainWindow", new Dictionary<string, object>() { { "gameContentLoaded", AppModel.gameContentLoaded.ToString() } });
            MainThreadConsoleWriteLine("Loading Done");
            var mainWindow = WindowFactory.createMainWindow(_model.mainModel);
            mainWindow.onRelaunch = onRelaunch;
            mainWindow.onReload = onReload;
            this.MainWindow = mainWindow;

            _splashWindow?.Close();
            closeBusyIndicator();

            this.MainWindow.Show();
        }

        private void onRelaunch()
        {
            showSplashWindowReplacingOldWindow();
            _ = loadAsync(askForGameContentLocation: true);
        }

        private async void onReload(string? autoReloadFilename, ProfileSaveFile? profile)
        {
            var oldMainWindow = this.MainWindow;
            var mainWindow = WindowFactory.createMainWindow(_model.mainModel);
            mainWindow.onRelaunch = onRelaunch;
            mainWindow.onReload = onReload;
            this.MainWindow = mainWindow;
            oldMainWindow?.Close();
            this.MainWindow.Show();

            if (!string.IsNullOrWhiteSpace(autoReloadFilename))
            {
                if(profile != null)
                {
                    _model.mainModel.setProfile(autoReloadFilename!, profile);
                }
                else
                {
                    await mainWindow.handleFileOpenAsync(autoReloadFilename!);
                }
            }
        }

        private void showSplashWindowReplacingOldWindow()
        {
            if(_controlWriter != null)
            {
                _outputWriter.removeWriter(_controlWriter);
                _controlWriter.Close();
            }

            var oldMainWindow = this.MainWindow;
            _splashWindow = WindowFactory.createSplashWindow(_loadingModel);
            _controlWriter = new ControlWriter(_splashWindow.textbox, _loadingModel.AcceptDiagnostic);
            this.ExecuteOnMainThreadWithNonnullThis(nonnullThis => {
                if(nonnullThis._controlWriter != null)
                    nonnullThis._outputWriter.addWriter(nonnullThis._controlWriter);
            });

            MainWindow = _splashWindow;
            oldMainWindow?.Close();
            this.MainWindow.Show();
        }

        private void showBusyIndicator()
        {
            closeBusyIndicator();

            _busyWindow = WindowFactory.createBusyWindow();
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

#region Dispose
        //Implemented as described here: https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose

        private bool _disposed = false;

        ~App()
        {
            Dispose(true);
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // dispose managed state (managed objects).
                _outputWriter.Dispose();
                _controlWriter?.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override a finalizer below.
            // set large fields to null.

            _disposed = true;
        }

#endregion
    }
}
