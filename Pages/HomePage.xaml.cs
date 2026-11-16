namespace MauiApp.Pages;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        SizeChanged += (_, _) => ToolsLayout.Span = Math.Max(2, (int)(Width / 190));
    }

    private void OnMenuClicked(object? sender, EventArgs e) => Shell.Current.FlyoutIsPresented = true;

    private void OnSwipedRight(object? sender, SwipedEventArgs e) => Shell.Current.FlyoutIsPresented = true;
}
