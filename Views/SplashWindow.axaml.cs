using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MegaDatos.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public void UpdateStatus(string status)
    {
        var statusText = this.FindControl<TextBlock>("StatusText");
        if (statusText != null)
        {
            statusText.Text = status;
        }
    }
}
