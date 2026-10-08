using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LastFolder.Models;

namespace LastFolder.Helpers
{
    /// <summary>
    /// Thin line-style icons from the design. Drawn in a 24x24 coordinate system.
    /// </summary>
    internal static class IconProvider
    {
        private const string FileOutline =
            "M7,3 L14,3 L19,8 L19,19.5 C19,20.33 18.33,21 17.5,21 L7,21 C6.17,21 5.5,20.33 5.5,19.5 L5.5,4.5 C5.5,3.67 6.17,3 7,3 Z M14,3 L14,8 L19,8";

        public static readonly Geometry Folder = G(
            "M3,7 C3,5.9 3.9,5 5,5 L9.6,5 L11.6,7 L19,7 C20.1,7 21,7.9 21,9 L21,17 C21,18.1 20.1,19 19,19 L5,19 C3.9,19 3,18.1 3,17 Z");

        public static readonly Geometry File = G(FileOutline);
        public static readonly Geometry FileText = G(FileOutline + " M8.5,12.5 L16,12.5 M8.5,15.5 L16,15.5 M8.5,18 L13,18");
        public static readonly Geometry FileCad = G(FileOutline + " M9,14.2 L12,12.5 L15,14.2 L15,17.6 L12,19.3 L9,17.6 Z M9,14.2 L12,15.9 L15,14.2 M12,15.9 L12,19.3");
        public static readonly Geometry FileImage = G(FileOutline + " M8.5,18.5 L11,15 L13.2,17.5 L14.6,16 L16,18.5 Z");
        public static readonly Geometry FileArchive = G(FileOutline + " M10.5,5 L10.5,6.5 M10.5,8 L10.5,9.5 M10.5,11 L10.5,12.5 M9.5,14 L11.5,14 L11.5,17 L9.5,17 Z");

        private static readonly Brush FolderBrush = B("#B4B7BE");
        private static readonly Brush DefaultBrush = B("#A3A6AD");

        private static readonly Dictionary<string, (Geometry Geometry, Brush Brush)> Map = Build();

        public static (Geometry Geometry, Brush Brush) For(ItemKind kind, string extension)
        {
            if (kind == ItemKind.Folder) return (Folder, FolderBrush);
            return Map.TryGetValue(extension, out var v) ? v : (File, DefaultBrush);
        }

        private static Dictionary<string, (Geometry, Brush)> Build()
        {
            var d = new Dictionary<string, (Geometry, Brush)>(System.StringComparer.OrdinalIgnoreCase);

            void Add(Geometry g, string color, params string[] exts)
            {
                var brush = B(color);
                foreach (var e in exts) d[e] = (g, brush);
            }

            Add(FileText, "#F05D5E", "PDF");
            Add(FileCad, "#F2A93B", "DXF", "DWG", "DWF");
            Add(FileCad, "#5AA2FF", "SLDPRT", "SLDASM", "SLDDRW", "STEP", "STP", "IGS", "IGES", "X_T", "STL", "IPT", "IAM");
            Add(FileText, "#5B8DEF", "DOC", "DOCX", "RTF", "ODT");
            Add(FileText, "#3FBF7F", "XLS", "XLSX", "XLSM", "CSV", "ODS");
            Add(FileText, "#F0883E", "PPT", "PPTX", "ODP");
            Add(FileImage, "#B07CFF", "PNG", "JPG", "JPEG", "BMP", "GIF", "SVG", "WEBP", "TIF", "TIFF", "HEIC");
            Add(FileArchive, "#D4A35A", "ZIP", "RAR", "7Z", "TAR", "GZ");
            Add(FileText, "#A3A6AD", "TXT", "MD", "LOG", "INI", "JSON", "XML", "CFG", "YAML", "YML");
            return d;
        }

        private static Geometry G(string data)
        {
            var g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        private static readonly Dictionary<string, ImageSource> _sysIcons = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? GetSystemIcon(ItemKind kind, string extension)
        {
            string key = kind == ItemKind.Folder ? "::folder" : extension;
            if (_sysIcons.TryGetValue(key, out var img)) return img;

            var shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
            uint attr = kind == ItemKind.Folder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            string path = kind == ItemKind.Folder ? "folder" : ("file." + extension);

            IntPtr res = SHGetFileInfo(path, attr, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero) return null;

            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                _sysIcons[key] = src;
                return src;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }

        private static Brush B(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}
