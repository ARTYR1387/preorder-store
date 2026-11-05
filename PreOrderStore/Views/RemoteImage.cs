using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace PreOrderStore.Views;

public sealed class RemoteImage : Image
{
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<RemoteImage, string?>(nameof(Url));

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Cache = new();
    private CancellationTokenSource? _loadCancellation;

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    static RemoteImage()
    {
        UrlProperty.Changed.AddClassHandler<RemoteImage>((control, _) => control.StartLoading());
    }

    private void StartLoading()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        Source = null;
        var url = Url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
        _ = LoadAsync(url!, _loadCancellation.Token);
    }

    private async Task LoadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var task = Cache.GetOrAdd(key, static async address =>
            {
                try
                {
                    var bytes = await Client.GetByteArrayAsync(address);
                    using var stream = new MemoryStream(bytes);
                    return new Bitmap(stream);
                }
                catch
                {
                    return null;
                }
            });
            var bitmap = await task;
            cancellationToken.ThrowIfCancellationRequested();
            if (bitmap is not null)
                Dispatcher.UIThread.Post(() =>
                {
                    if (!cancellationToken.IsCancellationRequested) Source = bitmap;
                }, DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
            // The control was rebound to another catalogue item.
        }
        catch
        {
            // Keep the neutral container visible on offline/error responses.
        }
    }
}
