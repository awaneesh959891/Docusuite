using MauiApp.Services;

namespace MauiApp.Pages;

public partial class PdfPreviewPage : ContentPage
{
    private readonly string _path;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Image> _images = [];
    private double _zoom = 1;
    private const double BaseWidth = 620;

    public string Heading { get; }
    public string Info { get; private set; } = "Loading…";

    public PdfPreviewPage(string path, string? title = null)
    {
        InitializeComponent();
        _path = path;
        var name = Path.GetFileName(path);
        Heading = title ?? (name.Length > 33 && name[32] == '_' ? name[33..] : name);
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts.Cancel();
    }

    private void SetInfo(string text)
    {
        Info = text;
        OnPropertyChanged(nameof(Info));
    }

    private async Task LoadAsync()
    {
        if (_images.Count > 0)
            return;
        try
        {
            var count = await Task.Run(() => PdfPageRenderService.GetPageCount(_path));

            var size = Formatting.Bytes(new FileInfo(_path).Length);
            SetInfo($"{count} page{(count == 1 ? "" : "s")}  •  {size}");

            for (var i = 0; i < count; i++)
            {
                var image = new Image { Aspect = Aspect.AspectFit, WidthRequest = BaseWidth, HeightRequest = BaseWidth * 1.3 };
                var frame = new Border
                {
                    Padding = 0,
                    StrokeThickness = 0,
                    BackgroundColor = Colors.White,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    Shadow = new Shadow { Opacity = 0.25f, Radius = 8, Offset = new Point(0, 3) },
                    Content = new VerticalStackLayout
                    {
                        Children =
                        {
                            image,
                            new Label { Text = $"Page {i + 1} of {count}", FontSize = 11, HorizontalOptions = LayoutOptions.Center, TextColor = Colors.Gray, Margin = new Thickness(0, 0, 0, 4) }
                        }
                    }
                };
                _images.Add(image);
                PagesHost.Add(frame);
            }

            for (var i = 0; i < count && !_cts.IsCancellationRequested; i++)
            {
                var index = i;
                var png = await Task.Run(() => Render(index));
                if (_cts.IsCancellationRequested)
                    return;
                var img = _images[index];
                img.Source = ImageSource.FromStream(() => new MemoryStream(png));
                if (img.Parent is VerticalStackLayout)
                    img.HeightRequest = -1;
            }
        }
        catch (Exception ex)
        {
            SetInfo(ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                ? "This PDF is password protected, so it can't be previewed."
                : $"Preview failed: {ex.Message}");
        }
    }

    private byte[] Render(int index) => PdfPageRenderService.RenderPage(_path, index, 1000);

    private void ApplyZoom()
    {
        _zoom = Math.Clamp(_zoom, 0.5, 2.5);
        foreach (var image in _images)
            image.WidthRequest = BaseWidth * _zoom;
    }

    private void OnZoomIn(object? sender, EventArgs e) { _zoom += 0.25; ApplyZoom(); }
    private void OnZoomOut(object? sender, EventArgs e) { _zoom -= 0.25; ApplyZoom(); }
    private async void OnClose(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private async void OnOpen(object? sender, EventArgs e)
    {
        if (await FileActions.OpenAsync(_path) is { } error)
            await DisplayAlertAsync("Open", error, "OK");
    }

    private async void OnShare(object? sender, EventArgs e)
    {
        if (await FileActions.ShareAsync(_path) is { } error)
            await DisplayAlertAsync("Share", error, "OK");
    }

    private async void OnSave(object? sender, EventArgs e) =>
        await DisplayAlertAsync("Save", await FileActions.SaveAsync(_path), "OK");
}
