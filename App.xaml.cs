using System;
using System.Linq;
using System.Threading;
using System.Windows;
using LastFolder.Helpers;
using LastFolder.Native;

namespace LastFolder
{
    public partial class App : Application
    {
        private const string MutexName = @"Local\LastFolder.SingleInstance";
        private const string ShowEventName = @"Local\LastFolder.Show";

        private Mutex? _mutex;
        private EventWaitHandle? _showEvent;
        private RegisteredWaitHandle? _showWait;
        private KeyboardHook? _hook;
        private TrayIcon? _tray;
        private MainWindow? _window;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool isFirstInstance);
            if (!isFirstInstance)
            {
                // Already running: signal the existing instance to show its window, then exit.
                try
                {
                    NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                    using var signal = EventWaitHandle.OpenExisting(ShowEventName);
                    signal.Set();
                }
                catch
                {
                    // signal could not be sent — exit silently
                }
                _mutex.Dispose();
                _mutex = null;
                Shutdown();
                return;
            }

            base.OnStartup(e);

            bool silent = e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase);
            bool showNow = e.Args.Contains("--show", StringComparer.OrdinalIgnoreCase);
            bool demo = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);

            Strings.Initialize();
            if (demo) Strings.SetLanguage(AppLanguage.English, persist: false);

            _window = new MainWindow { DemoData = demo };

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
                (_, _) => Dispatcher.InvokeAsync(() => _window.ShowLauncher()), null, Timeout.Infinite, false);

            try
            {
                _hook = new KeyboardHook();
                _hook.HotkeyPressed += () => Dispatcher.InvokeAsync(() => _window.Toggle());
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.HotkeyFailed + ex.Message, "LastFolder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _tray = new TrayIcon(onOpen: () => _window.ShowLauncher(), onExit: ExitApp);

            // Preload the list in the background so the first open doesn't wait.
            _window.Preload();

            int snapshotIndex = Array.FindIndex(e.Args, a => a.Equals("--snapshot", StringComparison.OrdinalIgnoreCase));
            if (snapshotIndex >= 0 && snapshotIndex + 1 < e.Args.Length)
            {
                string path = e.Args[snapshotIndex + 1];
                Dispatcher.InvokeAsync(async () =>
                {
                    try { await _window.SaveSnapshotAsync(path); }
                    finally { ExitApp(); }
                });
            }
            else if (showNow)
                _window.ShowLauncher();
            else if (!silent)
                _tray.ShowBalloon(Strings.BalloonTitle, Strings.BalloonText);
        }

        private void ExitApp()
        {
            _window?.AllowClose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hook?.Dispose();
            _tray?.Dispose();
            _showWait?.Unregister(null);
            _showEvent?.Dispose();
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
