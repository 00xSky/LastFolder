using System;
using System.Windows.Media;
using LastFolder.Helpers;

namespace LastFolder.Models
{
    public enum ItemKind
    {
        Folder,
        File
    }

    public sealed class RecentItem
    {
        public RecentItem(string path, ItemKind kind, DateTime lastOpened, long? size)
        {
            Path = path;
            Kind = kind;
            LastOpened = lastOpened;
            Size = size;

            string name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            Name = string.IsNullOrEmpty(name) ? path : name;

            Extension = kind == ItemKind.File
                ? System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant()
                : string.Empty;

            // Real Windows icon
            IconImage = IconProvider.GetSystemIcon(kind, Extension);
        }

        public string Path { get; }
        public string Name { get; }
        public ItemKind Kind { get; }

        /// <summary>Upper-case extension without the dot (e.g. "DXF"). Empty for folders.</summary>
        public string Extension { get; }

        public DateTime LastOpened { get; }
        public long? Size { get; }

        public bool IsFolder => Kind == ItemKind.Folder;
        public ImageSource? IconImage { get; }

        public string TimeText => Formatters.RelativeTime(LastOpened);
        public string SizeText => IsFolder ? "—" : Formatters.FileSize(Size);
    }
}
