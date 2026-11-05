using Avalonia.Controls;
using Avalonia.Interactivity;
using PreOrderStore.Domain;
using PreOrderStore.ViewModels;

namespace PreOrderStore.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
                await viewModel.InitializeAsync();
        };
    }

    private void OnSelectProductClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: Product product } && DataContext is MainViewModel viewModel)
            viewModel.SelectProductCommand.Execute(product);
    }

    private void OnCancelOrderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: PreOrder order } && DataContext is MainViewModel viewModel)
            viewModel.CancelOrderCommand.Execute(order);
    }
}
