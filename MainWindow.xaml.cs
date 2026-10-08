using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LastFolder.Helpers;
using LastFolder.Models;
using LastFolder.Native;
using LastFolder.Services;

namespace LastFolder
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<RecentItem> _items = new();
        private readonly ObservableCollection<FilterChip> _chips = new();
        private readonly ICollectionView _view;
        private readonly CompareInfo _compare = CultureInfo.CurrentCulture.CompareInfo;

        private FilterChip? _activeChip;
        private string[] _queryTokens = Array.Empty<string>();
        private DateTime _shownAtUtc;
        private bool _isLoading;
        private bool _allowClose;

        public MainWindow()
        {
            InitializeComponent();

            _view = CollectionViewSource.GetDefaultView(_items);
            _view.Filter = FilterPredicate;
            ItemsList.ItemsSource = _view;
            FilterChips.ItemsSource = _chips;

            new WindowInteropHelper(this).EnsureHandle();
            RebuildChips(Array.Empty<RecentItem>());
            Strings.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>Dev only: shows sample items instead of the Recent folder (--demo).</summary>
        public bool DemoData { get; set; }

        private RecentItem? SelectedItem => ItemsList.SelectedItem as RecentItem;

        // ───────────────────────── Show / Hide ─────────────────────────

        public void Preload() => _ = ReloadAsync();

        /// <summary>Dev only: saves the window as a PNG (--snapshot path).</summary>
        public async Task SaveSnapshotAsync(string path)
        {
            ShowLauncher();
            while (_isLoading) await Task.Delay(50);
            await Task.Delay(400); // let the animation finish
            UpdateLayout();

            var dpi = VisualTreeHelper.GetDpi(this);
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY),
                dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bmp.Render(this);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        public void Toggle()
        {
            if (IsVisible && IsActive) HideLauncher();
            else ShowLauncher();
        }

        public void ShowLauncher()
        {
            if (!IsVisible)
            {
                SearchBox.Text = string.Empty;
                SetActiveChip(null);
                PositionOnActiveScreen();
                Show();
                PlayShowAnimation();
                _ = ReloadAsync();
            }

            _shownAtUtc = DateTime.UtcNow;
            Activate();
            NativeMethods.ForceForeground(new WindowInteropHelper(this).Handle);
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }

        public void HideLauncher() => Hide();

        public void AllowClose() => _allowClose = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            // Alt+F4 must not close the app, only hide it. Exit is in the tray menu.
            if (!_allowClose)
            {
                e.Cancel = true;
                HideLauncher();
            }
            base.OnClosing(e);
        }

        private void PositionOnActiveScreen()
        {
            NativeMethods.GetCursorPos(out var cursor);
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
            var area = screen.WorkingArea; // physical pixels
            double scale = NativeMethods.GetScaleAt(cursor);

            double areaLeft = area.Left / scale, areaTop = area.Top / scale;
            double areaWidth = area.Width / scale, areaHeight = area.Height / scale;

            Left = areaLeft + Math.Max(0, (areaWidth - Width) / 2);
            Top = areaTop + Math.Max(0, (areaHeight - Height) / 2);
        }

        private void PlayShowAnimation()
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(150);

            RootGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, 1, duration) { EasingFunction = ease });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, 1, duration) { EasingFunction = ease });
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            // Ignore momentary deactivations that happen while taking focus.
            if ((DateTime.UtcNow - _shownAtUtc).TotalMilliseconds < 300) return;
            HideLauncher();
        }

        // ───────────────────────── Data ─────────────────────────

        private async Task ReloadAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            UpdateEmptyState();
            try
            {
                var list = DemoData ? DemoItems.Create() : await RecentItemsService.LoadAsync();
                ApplyItems(list);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                _isLoading = false;
                UpdateEmptyState();
            }
        }

        private void ApplyItems(List<RecentItem> list)
        {
            string? previousPath = ItemsList.SelectedIndex > 0 ? SelectedItem?.Path : null;

            // The collection can't be modified while DeferRefresh is active (throws on the first Add), so it isn't used.
            _items.Clear();
            foreach (var item in list) _items.Add(item);
            RebuildChips(list);
            _view.Refresh();

            var match = previousPath == null
                ? null
                : ItemsList.Items.Cast<RecentItem>().FirstOrDefault(i => i.Path == previousPath);

            if (match != null)
            {
                ItemsList.SelectedItem = match;
                ItemsList.ScrollIntoView(match);
            }
            else
            {
                SelectFirst();
            }
        }

        /// <summary>All, Folder, File + the most frequent extensions among recent files.</summary>
        private void RebuildChips(IReadOnlyCollection<RecentItem> items)
        {
            string? activeKey = _activeChip?.Key;

            _chips.Clear();
            _chips.Add(new FilterChip("::all", Strings.ChipAll, ChipKind.Folder)); // no special icon, or folder; actually All
            _chips.Add(new FilterChip("::folder", Strings.ChipFolder, ChipKind.Folder));
            _chips.Add(new FilterChip("::file", Strings.ChipFile, ChipKind.File));
            foreach (var (ext, count) in RecentItemsService.TopExtensions(items))
                _chips.Add(new FilterChip(ext, ext, ChipKind.Extension, count));

            _activeChip = _chips.FirstOrDefault(c => c.Key == activeKey) ?? _chips[0];
            _activeChip.IsSelected = true;
        }

        // ───────────────────────── Filtering ─────────────────────────

        private bool FilterPredicate(object obj)
        {
            if (obj is not RecentItem item) return false;

            if (_activeChip != null && _activeChip.Key != "::all")
            {
                switch (_activeChip.Kind)
                {
                    case ChipKind.Folder when !item.IsFolder:
                    case ChipKind.File when item.IsFolder:
                        return false;
                    case ChipKind.Extension when !string.Equals(item.Extension, _activeChip.Key, StringComparison.OrdinalIgnoreCase):
                        return false;
                }
            }

            // Every word must appear in the name. A word starting with a dot, like ".pdf", becomes an extension filter.
            foreach (var token in _queryTokens)
            {
                if (token.Length > 1 && token[0] == '.')
                {
                    if (!item.Extension.StartsWith(token.Substring(1), StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (_compare.IndexOf(item.Name, token, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Placeholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _queryTokens = SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            RefreshView();
        }

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is FilterChip chip)
                SetActiveChip(ReferenceEquals(_activeChip, chip) ? null : chip);
        }

        private void SetActiveChip(FilterChip? chip)
        {
            foreach (var c in _chips) c.IsSelected = ReferenceEquals(c, chip);
            _activeChip = chip;
            RefreshView();
        }

        private void CycleChip(int direction)
        {
            if (_chips.Count == 0) return;
            int index = _activeChip == null ? -1 : _chips.IndexOf(_activeChip);
            int next = index + direction;
            if (next >= _chips.Count) next = -1;          // all
            else if (next < -1) next = _chips.Count - 1;
            SetActiveChip(next < 0 ? null : _chips[next]);
        }

        private void RefreshView()
        {
            _view.Refresh();
            SelectFirst();
            UpdateEmptyState();
        }

        private void OnLanguageChanged()
        {
            RebuildChips(_items.ToList());
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            bool empty = ItemsList.Items.Count == 0;
            EmptyText.Text = _isLoading && _items.Count == 0 ? Strings.Loading : Strings.NoResults;
            EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        }

        // ───────────────────────── Selection / keyboard ─────────────────────────

        private void SelectFirst()
        {
            if (ItemsList.Items.Count == 0) return;
            ItemsList.SelectedIndex = 0;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }

        private void MoveSelection(int delta)
        {
            int count = ItemsList.Items.Count;
            if (count == 0) return;
            int index = Math.Clamp(ItemsList.SelectedIndex + delta, 0, count - 1);
            ItemsList.SelectedIndex = index;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;

            switch (e.Key)
            {
                case Key.Escape:
                    HideLauncher();
                    e.Handled = true;
                    break;
                case Key.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    MoveSelection(8);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    MoveSelection(-8);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (SelectedItem is { } item)
                    {
                        if (mods.HasFlag(ModifierKeys.Control)) RevealItem(item);
                        else OpenItem(item);
                    }
                    e.Handled = true;
                    break;
                case Key.Tab:
                    CycleChip(mods.HasFlag(ModifierKeys.Shift) ? -1 : 1);
                    e.Handled = true;
                    break;
                case Key.C when mods == ModifierKeys.Control && SearchBox.SelectionLength == 0:
                    if (SelectedItem is { } toCopy) CopyPath(toCopy);
                    e.Handled = true;
                    break;
            }
        }

        private void ItemsList_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Keep focus in the search box so typing can continue after a click.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => SearchBox.Focus()));
        }

        private void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            var source = e.OriginalSource as DependencyObject;
            if (FindAncestor<ButtonBase>(source) != null) return;           // row action button
            if (FindAncestor<ListBoxItem>(source)?.DataContext is RecentItem item)
                OpenItem(item);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void HideButton_Click(object sender, RoutedEventArgs e) => HideLauncher();

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is RecentItem item) RevealItem(item);
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is RecentItem item) CopyPath(item);
        }

        // ───────────────────────── Actions ─────────────────────────

        private void OpenItem(RecentItem item)
        {
            if (!item.Path.StartsWith(@"\\", StringComparison.Ordinal) && !File.Exists(item.Path) && !Directory.Exists(item.Path))
            {
                ShowToast(Strings.ItemNotFound);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
                HideLauncher();
            }
            catch (Exception ex)
            {
                ShowToast(Strings.OpenFailed + ex.Message);
            }
        }

        private void RevealItem(RecentItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
                HideLauncher();
            }
            catch (Exception ex)
            {
                ShowToast(Strings.RevealFailed + ex.Message);
            }
        }

        private void CopyPath(RecentItem item)
        {
            try
            {
                Clipboard.SetText(item.Path);
                ShowToast(Strings.PathCopied);
            }
            catch
            {
                ShowToast(Strings.ClipboardUnavailable);
            }
        }

        private void ShowToast(string message)
        {
            ToastText.Text = message;
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1500))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));
            Toast.BeginAnimation(OpacityProperty, anim);
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = current is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
