using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using LastFolder.Models;
using LastFolder.Native;

namespace LastFolder.Services
{
    internal static class RecentItemsService
    {
        /// <summary>Extensions that are never shown in the list.</summary>
        private static readonly HashSet<string> ExcludedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".exe", ".lnk" };

        /// <summary>Number of recent files inspected for the dynamic filters.</summary>
        public const int ExtensionSampleSize = 20;

        /// <summary>Maximum number of extension buttons shown besides Folder/File.</summary>
        public const int MaxExtensionFilters = 4;

        /// <summary>
        /// Reads the shortcuts in %APPDATA%\Microsoft\Windows\Recent, resolves their targets
        /// and returns a list sorted newest to oldest. (An STA thread is used for IShellLink)
        /// </summary>
        public static Task<List<RecentItem>> LoadAsync(int maxItems = 50)
        {
            var tcs = new TaskCompletionSource<List<RecentItem>>();
            var t = new System.Threading.Thread(() =>
            {
                try { tcs.SetResult(LoadInternal(maxItems)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            return tcs.Task;
        }

        private static List<RecentItem> LoadInternal(int maxItems)
        {
            var result = new List<RecentItem>();
            string recentDir = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            var dir = new DirectoryInfo(recentDir);
            if (!dir.Exists) return result;

            IEnumerable<FileInfo> links;
            try
            {
                links = dir.EnumerateFiles("*.lnk")
                           .OrderByDescending(f => f.LastWriteTimeUtc)
                           .ToList();
            }
            catch
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var lnk in links)
            {
                if (result.Count >= maxItems) break;

                string? target = ShellLinkResolver.GetTarget(lnk.FullName);
                if (target is null || !seen.Add(target)) continue;
                if (ExcludedExtensions.Contains(Path.GetExtension(target))) continue;

                var item = CreateItem(target, lnk.LastWriteTime);
                if (item != null) result.Add(item);
            }

            return result;
        }

        private static RecentItem? CreateItem(string target, DateTime lastOpened)
        {
            try
            {
                // No existence check on network paths, so an unreachable server doesn't freeze the app.
                if (target.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    bool looksLikeFile = Path.HasExtension(target);
                    return new RecentItem(target, looksLikeFile ? ItemKind.File : ItemKind.Folder, lastOpened, null);
                }

                if (Directory.Exists(target))
                    return new RecentItem(target, ItemKind.Folder, lastOpened, null);

                var file = new FileInfo(target);
                if (file.Exists)
                    return new RecentItem(target, ItemKind.File, lastOpened, file.Length);
            }
            catch
            {
                // Access error etc. — the item is skipped
            }

            return null; // deleted / moved
        }

        /// <summary>
        /// Counts the extensions of the last <see cref="ExtensionSampleSize"/> files and
        /// returns the most frequent ones (on a tie, the more recent one comes first).
        /// </summary>
        public static List<(string Extension, int Count)> TopExtensions(IEnumerable<RecentItem> items)
        {
            return items.Where(i => i.Kind == ItemKind.File && i.Extension.Length > 0)
                        .Take(ExtensionSampleSize)
                        .GroupBy(i => i.Extension)
                        .Select(g => (Extension: g.Key, Count: g.Count()))
                        .OrderByDescending(x => x.Count) // stable sort: on a tie, the first one seen comes first
                        .Take(MaxExtensionFilters)
                        .ToList();
        }
    }
}
