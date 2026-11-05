using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using PreOrderStore.Domain;
using PreOrderStore.ViewModels;

namespace PreOrderStore.Views;

public partial class MainWindow : Window
{
    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["F4F5F1"] = "#171B18", ["E9EDE8"] = "#202722", ["FFFFFF"] = "#252C27",
        ["FAFBF9"] = "#202722", ["F7F9F6"] = "#303A33", ["F6F8F5"] = "#303A33",
        ["F3F5F1"] = "#252D27", ["F2F4F0"] = "#202722", ["F5F3ED"] = "#302D27",
        ["F1ECE2"] = "#373227", ["E9E4D9"] = "#4A4234", ["ECEFEA"] = "#252D27",
        ["E8EFE9"] = "#35423A", ["E4EFE7"] = "#304238", ["EDF2EC"] = "#303C33",
        ["DCE8DE"] = "#344B3A", ["D8E4DA"] = "#3A4A3E", ["DCE2DC"] = "#39413B",
        ["E0E5DF"] = "#3A433C", ["E1E6E0"] = "#3B443D", ["DDE3DD"] = "#3B453E",
        ["D9E0D9"] = "#465149", ["F2F6F2"] = "#35433A", ["202720"] = "#F0F3EF",
        ["26332A"] = "#E6ECE7", ["59655C"] = "#CAD3CB", ["4E5C51"] = "#C6D0C8",
        ["244A35"] = "#A9D0B4", ["234B37"] = "#B0D7BA", ["246247"] = "#5D9B74",
        ["1B523A"] = "#73AE86", ["173F2C"] = "#ABD7B5", ["365C43"] = "#B1D5B8",
        ["526C59"] = "#B2CBB6", ["53675A"] = "#BCC9BE", ["54715E"] = "#B9D0BE",
        ["66736A"] = "#C2CBC3", ["68746B"] = "#C3CEC5", ["695A3D"] = "#D9C79F",
        ["6D786F"] = "#BDC8BF", ["6E7B71"] = "#C0CCC2", ["6F6655"] = "#D3C7AE",
        ["6F7E72"] = "#C0CCC2", ["716958"] = "#D5C9AE", ["718076"] = "#BBC8BE",
        ["728078"] = "#BDC9C0", ["737F75"] = "#C1CBC2", ["738077"] = "#BBC8BE",
        ["748077"] = "#BBC8BE", ["758178"] = "#C1CBC2", ["778279"] = "#B8C3BA",
        ["78837A"] = "#B9C5BB", ["78847A"] = "#B8C4BA", ["7A857C"] = "#B9C5BB",
        ["859086"] = "#AAB5AC", ["879188"] = "#AAB6AC", ["9EAF9F"] = "#66796B",
        ["A7B7AA"] = "#66796B", ["9A493F"] = "#EF9A8D"
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
                ApplyTheme(viewModel.IsDarkTheme);
            }
        };
        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
                await viewModel.InitializeAsync();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme) && sender is MainViewModel viewModel)
            ApplyTheme(viewModel.IsDarkTheme);
    }

    private void ApplyTheme(bool isDark)
    {
        RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        foreach (var pair in DarkPalette)
        {
            var key = "C_" + pair.Key;
            if (Resources.ContainsKey(key))
                Resources[key] = new SolidColorBrush(Color.Parse(isDark ? pair.Value : "#" + pair.Key));
        }
    }

    private void OnSelectProductClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: Product product } && DataContext is MainViewModel vm)
            vm.SelectProductCommand.Execute(product);
    }

    private void OnAddToCartClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: Product product } && DataContext is MainViewModel vm)
            vm.AddToCartCommand.Execute(product);
    }

    private void OnIncrementCartClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: CartItem { Product: { } product } } && DataContext is MainViewModel vm)
            vm.AddToCartCommand.Execute(product);
    }

    private void OnDecrementCartClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: CartItem item } && DataContext is MainViewModel vm)
            vm.RemoveFromCartCommand.Execute(item);
    }

    private void OnCancelOrderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: PreOrder order } && DataContext is MainViewModel vm)
            vm.CancelOrderCommand.Execute(order);
    }
}
