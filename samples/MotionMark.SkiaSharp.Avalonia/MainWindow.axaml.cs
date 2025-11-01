using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MotionMark.SkiaSharp.AvaloniaApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
