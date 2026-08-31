using System;
using Avalonia.Controls;

namespace MegaDatos.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Console.WriteLine("MainWindow: Constructor started");
        InitializeComponent();
        Console.WriteLine("MainWindow: InitializeComponent completed");
    }
}