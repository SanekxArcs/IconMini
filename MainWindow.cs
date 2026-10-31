using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;

namespace IconMini;

public sealed class MainWindow : Window
{
    private IconRenderer? renderer;
    private readonly Image preview = new() { Width = 176, Height = 176, Stretch = Stretch.Uniform };
    private readonly Image smallPreview = new() { Width = 16, Height = 16 };
    private readonly FontIcon empty = new() { Glyph = "\uE91B", FontSize = 48, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 108, 115, 135)) };
    private readonly TextBlock filename = new() { Text = "Перетягніть зображення сюди", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 460 };
    private readonly TextBlock metadata = new() { Text = "PNG · JPG · WebP · SVG · GIF · BMP · ICO", Opacity = .6, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ComboBox mode = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox fit = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox background = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Slider padding = new() { Minimum = 0, Maximum = 30, StepFrequency = 1, Value = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock paddingLabel = new() { Text = "Відступ: 8%", FontSize = 12, Opacity = .75 };
    private readonly TextBlock sizes = new() { Text = "16, 32, 48 px · один файл .ico", FontSize = 12, Opacity = .65 };
    private readonly Button export = new() { Content = "Зберегти favicon.ico", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch, Height = 42 };
    private readonly Button open = new() { Content = "Вибрати зображення", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button reveal = new() { Content = "Показати файл", Visibility = Visibility.Collapsed };
    private readonly InfoBar notice = new() { IsOpen = false, IsClosable = true };
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private bool busy;
    private int previewVersion;
    private string? lastExport;
    private readonly StackPanel root;

    public MainWindow()
    {
        Title = "Icon Mini";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(620, 840));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.PreferredMinimumWidth = 500;
            presenter.PreferredMinimumHeight = 700;
        }
        SystemBackdrop = new MicaBackdrop();
        root = new StackPanel { Spacing = 16, Margin = new Thickness(28, 22, 28, 24), RequestedTheme = ElementTheme.Dark, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 24, 26, 32)) };
        Content = new ScrollViewer { Content = root, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 24, 26, 32)), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 3 };
        titles.Children.Add(new TextBlock { Text = "Icon Mini", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        titles.Children.Add(new TextBlock { Text = "Ваше зображення. Ваш маленький значок.", Opacity = .65, FontSize = 13 });
        heading.Children.Add(titles);
        var badge = new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(28, 104, 112, 240)), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 5, 10, 5), VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = ".ICO", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } };
        Grid.SetColumn(badge, 1); heading.Children.Add(badge); root.Children.Add(heading);

        var drop = new Border { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 57, 60, 72)), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 36, 44)), Padding = new Thickness(18), AllowDrop = true };
        var dropContent = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        var previewPanel = new Grid { Width = 192, Height = 192, HorizontalAlignment = HorizontalAlignment.Center };
        // A checkerboard makes transparency visible in both Windows themes.
        for (int row = 0; row < 12; row++)
            for (int col = 0; col < 12; col++)
                previewPanel.Children.Add(new Border { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(col * 16, row * 16, 0, 0), Background = new SolidColorBrush((row + col) % 2 == 0 ? Windows.UI.Color.FromArgb(255, 218, 220, 226) : Windows.UI.Color.FromArgb(255, 242, 243, 246)) });
        previewPanel.Children.Add(preview);
        previewPanel.Children.Add(empty);
        dropContent.Children.Add(previewPanel); dropContent.Children.Add(filename); dropContent.Children.Add(metadata); dropContent.Children.Add(open);
        drop.Child = dropContent; root.Children.Add(drop);
        drop.DragOver += (_, e) => { e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems) && !busy ? DataPackageOperation.Copy : DataPackageOperation.None; e.DragUIOverride.Caption = "Створити значок"; };
        drop.Drop += async (_, e) =>
        {
            if (busy || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var deferral = e.GetDeferral();
            try
            {
                var files = (await e.DataView.GetStorageItemsAsync()).OfType<StorageFile>().ToArray();
                if (files.Length != 1) { ShowError("Перетягніть одне зображення."); return; }
                await LoadImage(files[0].Path);
                empty.Visibility = renderer is null ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally { deferral.Complete(); }
        };
        open.Click += async (_, _) =>
        {
            try
            {
                var picker = new FileOpenPicker(); InitializePicker(picker);
                picker.FileTypeFilter.Add("*");
                var file = await picker.PickSingleFileAsync();
                if (file is not null) await LoadImage(file.Path);
                empty.Visibility = renderer is null ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { ShowError(ex.Message); }
        };

        mode.Items.Add("Favicon для сайту"); mode.Items.Add("Значок папки Windows"); mode.SelectedIndex = 0;
        fit.Items.Add("Вписати повністю"); fit.Items.Add("Обрізати по центру"); fit.SelectedIndex = 0;
        background.Items.Add("Прозоре"); background.Items.Add("Біле"); background.Items.Add("Темне"); background.SelectedIndex = 0;
        var options = new Grid { ColumnSpacing = 12 };
        options.ColumnDefinitions.Add(new ColumnDefinition()); options.ColumnDefinitions.Add(new ColumnDefinition());
        options.Children.Add(Field("Призначення", mode));
        var fitField = Field("Композиція", fit); Grid.SetColumn(fitField, 1); options.Children.Add(fitField); root.Children.Add(options);
        var adjustments = new Grid { ColumnSpacing = 16 };
        adjustments.ColumnDefinitions.Add(new ColumnDefinition()); adjustments.ColumnDefinitions.Add(new ColumnDefinition());
        adjustments.Children.Add(Field("Тло", background));
        var paddingField = new StackPanel { Spacing = 0 }; paddingField.Children.Add(paddingLabel); paddingField.Children.Add(padding);
        Grid.SetColumn(paddingField, 1); adjustments.Children.Add(paddingField); root.Children.Add(adjustments);
        var actual = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actual.Children.Add(smallPreview); actual.Children.Add(new TextBlock { Text = "Реальний розмір 16 px", FontSize = 12, Opacity = .65 }); root.Children.Add(actual);
        root.Children.Add(sizes); root.Children.Add(export); root.Children.Add(notice); root.Children.Add(reveal);
        root.Children.Add(new TextBlock { Text = "Локально на вашому ПК · без завантаження в інтернет", FontSize = 11, Opacity = .5, HorizontalAlignment = HorizontalAlignment.Center });

        export.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        export.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 112, 99, 235));
        export.Foreground = new SolidColorBrush(Colors.White);
        foreach (var combo in new[] { mode, fit, background })
        {
            combo.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 44, 47, 57));
            combo.Foreground = new SolidColorBrush(Colors.White);
        }
        open.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 48, 51, 62));
        open.Foreground = new SolidColorBrush(Colors.White);
        mode.SelectionChanged += (_, _) =>
        {
            bool folder = mode.SelectedIndex == 1;
            export.Content = folder ? "Зберегти folder.ico" : "Зберегти favicon.ico";
            sizes.Text = folder ? "16, 24, 32, 48, 64, 128, 256 px · один файл .ico" : "16, 32, 48 px · один файл .ico";
        };
        fit.SelectionChanged += (_, _) => QueuePreview(); background.SelectionChanged += (_, _) => QueuePreview();
        padding.ValueChanged += (_, _) => { paddingLabel.Text = $"Відступ: {(int)padding.Value}%"; QueuePreview(); };
        debounce.Tick += async (_, _) => { debounce.Stop(); await UpdatePreview(); };
        export.Click += async (_, _) => await Export();
        reveal.Click += async (_, _) =>
        {
            try
            {
                if (lastExport is null) return;
                var file = await StorageFile.GetFileFromPathAsync(lastExport);
                var folder = await file.GetParentAsync();
                var options = new FolderLauncherOptions(); options.ItemsToSelect.Add(file);
                await Launcher.LaunchFolderAsync(folder, options);
            }
            catch (Exception ex) { ShowError(ex.Message); }
        };
        Closed += (_, _) => { debounce.Stop(); previewVersion++; renderer?.Dispose(); renderer = null; };
    }

    internal void StartSmokeTest()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "verification"));
            Directory.CreateDirectory(output);
            try
            {
                string source = Path.Combine(output, "sample.svg");
                await File.WriteAllTextAsync(source, "<svg xmlns='http://www.w3.org/2000/svg' width='256' height='256'><rect x='8' y='8' width='240' height='240' rx='64' fill='#685ce8'/><path d='M72 82h112v28h-42v67h42v28H72v-28h42v-67H72z' fill='white'/><circle cx='194' cy='62' r='25' fill='#b9f488'/></svg>");
                await LoadImage(source);
                if (renderer is null || preview.Source is null || !export.IsEnabled) throw new Exception("Image import/preview failed");
                mode.SelectedIndex = 1;
                await File.WriteAllBytesAsync(Path.Combine(output, "folder.ico"), renderer.CreateIco(true, false, 8, SKColors.Transparent));
                mode.SelectedIndex = 0;
                await File.WriteAllBytesAsync(Path.Combine(output, "favicon.ico"), renderer.CreateIco(false, false, 8, SKColors.Transparent));
                root.UpdateLayout();
                await Task.Delay(250); // Let enabled control templates finish their next render pass.
                var capture = new RenderTargetBitmap();
                await capture.RenderAsync(root);
                var pixels = await capture.GetPixelsAsync();
                byte[] data = new byte[pixels.Length];
                using (var reader = DataReader.FromBuffer(pixels)) reader.ReadBytes(data);
                var file = await StorageFile.GetFileFromPathAsync(source);
                var target = await (await file.GetParentAsync()).CreateFileAsync("preview.png", CreationCollisionOption.ReplaceExisting);
                using (var stream = await target.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                        (uint)capture.PixelWidth, (uint)capture.PixelHeight, 96, 96, data);
                    await encoder.FlushAsync();
                }
                await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS: window activated, SVG imported, large/16px previews loaded, both export modes rendered, UI captured. Native file pickers and drag/drop not exercised.");
            }
            catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "FAIL: " + ex); }
            finally { Close(); }
        };
        timer.Start();
    }

    private static StackPanel Field(string label, UIElement control)
    {
        var field = new StackPanel { Spacing = 6 };
        field.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = .75 }); field.Children.Add(control); return field;
    }
    private void InitializePicker(object picker) => WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
    private SKColor BackgroundColor => background.SelectedIndex switch { 1 => SKColors.White, 2 => new SKColor(24, 26, 32), _ => SKColors.Transparent };
    private void ShowError(string message) { notice.Severity = InfoBarSeverity.Error; notice.Title = "Не вдалося"; notice.Message = message; notice.IsOpen = true; }
    private void SetBusy(bool value) { busy = value; export.IsEnabled = !value && renderer is not null; open.IsEnabled = !value; mode.IsEnabled = !value; fit.IsEnabled = !value; background.IsEnabled = !value; padding.IsEnabled = !value; }
    private async Task LoadImage(string path)
    {
        SetBusy(true);
        try
        {
            var next = await Task.Run(() => new IconRenderer(path));
            renderer?.Dispose(); renderer = next;
            empty.Visibility = Visibility.Collapsed;
            filename.Text = Path.GetFileName(path); metadata.Text = $"{renderer.Width:0} × {renderer.Height:0} px · {Path.GetExtension(path).TrimStart('.').ToUpperInvariant()}";
            notice.IsOpen = false;
            await UpdatePreview();
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }
    private void QueuePreview() { debounce.Stop(); debounce.Start(); }
    private async Task UpdatePreview()
    {
        if (renderer is null) return;
        int version = ++previewVersion;
        try
        {
            byte[] large = renderer.RenderPng(256, fit.SelectedIndex == 1, (int)padding.Value, BackgroundColor);
            byte[] small = renderer.RenderPng(16, fit.SelectedIndex == 1, (int)padding.Value, BackgroundColor);
            var largeImage = await Bitmap(large); var smallImage = await Bitmap(small);
            if (version != previewVersion) return;
            preview.Source = largeImage; smallPreview.Source = smallImage;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }
    private static async Task<BitmapImage> Bitmap(byte[] data)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(data); await writer.StoreAsync(); }
        stream.Seek(0);
        var image = new BitmapImage(); await image.SetSourceAsync(stream); return image;
    }
    private async Task Export()
    {
        if (renderer is null || busy) return;
        SetBusy(true);
        try
        {
            bool folder = mode.SelectedIndex == 1;
            var picker = new FileSavePicker { SuggestedFileName = folder ? "folder" : "favicon" };
            InitializePicker(picker); picker.FileTypeChoices.Add("Значок Windows", new List<string> { ".ico" });
            var file = await picker.PickSaveFileAsync(); if (file is null) return;
            byte[] data = renderer.CreateIco(folder, fit.SelectedIndex == 1, (int)padding.Value, BackgroundColor);
            await FileIO.WriteBytesAsync(file, data); lastExport = file.Path;
            notice.Severity = InfoBarSeverity.Success; notice.Title = "Значок збережено";
            notice.Message = folder ? "Щоб застосувати: властивості папки → Налаштування → Змінити значок → Огляд." : "Додайте favicon.ico до свого сайту.";
            notice.IsOpen = true; reveal.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }
}
