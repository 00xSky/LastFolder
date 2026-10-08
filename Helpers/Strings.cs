using System;
using System.Globalization;
using System.Windows;
using Microsoft.Win32;

namespace LastFolder.Helpers
{
    internal enum AppLanguage
    {
        English,
        Turkish
    }

    /// <summary>UI string table (English / Turkish) and the per-user language setting.</summary>
    internal static class Strings
    {
        private const string SettingsKey = @"Software\LastFolder";
        private const string LanguageValue = "Language";

        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        public static AppLanguage Language { get; private set; } = AppLanguage.English;
        public static CultureInfo Culture => Language == AppLanguage.Turkish ? Tr : En;

        public static event Action? LanguageChanged;

        private static string L(string en, string tr) => Language == AppLanguage.Turkish ? tr : en;

        public const string EnglishName = "English";
        public const string TurkishName = "Türkçe";

        // Main window
        public static string WindowTitle => L("LastFolder — Recent Activity", "LastFolder — Son Etkinlikler");
        public static string Header => L("Recent Activity", "Son Etkinlikler");
        public static string HideTooltip => L("Hide", "Gizle");
        public static string CloseTooltip => L("Close (Esc)", "Kapat (Esc)");
        public static string SearchPlaceholder => L("Search files, folders or types...", "Dosya, klasör veya tür ara...");
        public static string RevealTooltip => L("Show in folder  (Ctrl+Enter)", "Klasörde göster  (Ctrl+Enter)");
        public static string CopyPathTooltip => L("Copy path  (Ctrl+C)", "Yolu kopyala  (Ctrl+C)");
        public static string Loading => L("Loading...", "Yükleniyor...");
        public static string NoResults => L("No results found", "Sonuç bulunamadı");

        // Toasts
        public static string ItemNotFound => L("Item not found (it may have been moved or deleted)", "Öğe bulunamadı (taşınmış veya silinmiş olabilir)");
        public static string OpenFailed => L("Couldn't open: ", "Açılamadı: ");
        public static string RevealFailed => L("Couldn't open folder: ", "Klasör açılamadı: ");
        public static string PathCopied => L("Path copied", "Yol kopyalandı");
        public static string ClipboardUnavailable => L("Clipboard is unavailable right now", "Pano şu an kullanılamıyor");

        // Filter chips
        public static string ChipAll => L("All", "Tümü");
        public static string ChipFolder => L("Folder", "Klasör");
        public static string ChipFile => L("File", "Dosya");
        public static string ChipAllTooltip => L("Show all", "Tümünü göster");
        public static string ChipFolderTooltip => L("Folders only", "Sadece klasörler");
        public static string ChipFileTooltip => L("Files only", "Sadece dosyalar");
        public static string ChipExtensionTooltip(string ext, int count) =>
            string.Format(L("Only .{0} files  ({1} in recent files)", "Sadece .{0} dosyaları  (son dosyalarda {1} adet)"), ext, count);

        // Relative times
        public static string JustNow => L("Just now", "Az önce");
        public static string MinutesAgo(int minutes) => string.Format(L("{0} min ago", "{0} dk önce"), minutes);
        public static string HoursAgo(int hours) => string.Format(L("{0} hr ago", "{0} sa önce"), hours);
        public static string Yesterday(string time) => string.Format(L("Yesterday, {0}", "Dün, {0}"), time);

        // Tray
        public static string TrayOpen => L("Open Recent Activity   (Left Ctrl+Space)", "Son Etkinlikleri Aç   (Sol Ctrl+Space)");
        public static string TrayAutoStart => L("Start with Windows", "Windows ile başlat");
        public static string TrayLanguage => L("Language", "Dil");
        public static string TrayExit => L("Exit", "Çıkış");
        public static string TrayTooltip => L("LastFolder — Left Ctrl+Space", "LastFolder — Sol Ctrl+Space");
        public static string BalloonTitle => L("LastFolder is running", "LastFolder çalışıyor");
        public static string BalloonText => L("Press Left Ctrl + Space for recently opened files and folders.",
                                              "Son açılan dosya ve klasörler için Sol Ctrl + Space tuşlarına basın.");

        // Errors
        public static string HotkeyFailed => L("Couldn't register the Left Ctrl+Space shortcut:\n", "Sol Ctrl+Space kısayolu kaydedilemedi:\n");
        public static string HookFailed => L("Couldn't install the keyboard hook.", "Klavye kancası kurulamadı.");

        /// <summary>Loads the saved language (English when none is saved) and publishes the XAML strings.</summary>
        public static void Initialize()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKey, false);
                if (key?.GetValue(LanguageValue) is string code && code == "tr") Language = AppLanguage.Turkish;
            }
            catch { /* registry unavailable — keep the default */ }

            PublishResources();
        }

        public static void SetLanguage(AppLanguage language, bool persist = true)
        {
            if (persist)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(SettingsKey, true);
                    key.SetValue(LanguageValue, language == AppLanguage.Turkish ? "tr" : "en");
                }
                catch { /* registry unavailable — apply for this session only */ }
            }

            if (Language == language) return;
            Language = language;
            PublishResources();
            LanguageChanged?.Invoke();
        }

        /// <summary>Strings used by MainWindow.xaml through DynamicResource.</summary>
        private static void PublishResources()
        {
            var res = Application.Current?.Resources;
            if (res == null) return;

            res["Str.WindowTitle"] = WindowTitle;
            res["Str.Header"] = Header;
            res["Str.HideTooltip"] = HideTooltip;
            res["Str.CloseTooltip"] = CloseTooltip;
            res["Str.SearchPlaceholder"] = SearchPlaceholder;
            res["Str.RevealTooltip"] = RevealTooltip;
            res["Str.CopyPathTooltip"] = CopyPathTooltip;
            res["Str.NoResults"] = NoResults;
        }
    }
}
