using System.ComponentModel;
using System.Windows.Media;
using LastFolder.Helpers;

namespace LastFolder.Models
{
    public enum ChipKind
    {
        Folder,
        File,
        Extension
    }

    /// <summary>Filter button below the search bar.</summary>
    public sealed class FilterChip : INotifyPropertyChanged
    {
        private bool _isSelected;

        public FilterChip(string key, string label, ChipKind kind, int count = 0)
        {
            Key = key;
            Label = label;
            Kind = kind;
            Count = count;
        }

        public string Key { get; }
        public string Label { get; }
        public ChipKind Kind { get; }
        public int Count { get; }

        public Geometry? IconGeometry => Kind switch
        {
            ChipKind.Folder when Key != "::all" => IconProvider.Folder,
            ChipKind.File => IconProvider.File,
            _ => null
        };

        public bool HasIcon => IconGeometry != null;

        public string ToolTip => Key == "::all" ? Strings.ChipAllTooltip : Kind switch
        {
            ChipKind.Folder => Strings.ChipFolderTooltip,
            ChipKind.File => Strings.ChipFileTooltip,
            _ => Strings.ChipExtensionTooltip(Label.ToLowerInvariant(), Count)
        };

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
