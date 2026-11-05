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
    private static readonly SemaphoreSlim DownloadSlots = new(4, 4);
    private CancellationTokenSource? _loadCancellation;

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    static RemoteImage()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("PreOrderStore/1.0");
        UrlProperty.Changed.AddClassHandler<RemoteImage>((control, _) => control.StartLoading());
    }

    private void StartLoading()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        Source = null;
        Opacity = 0;
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
                await DownloadSlots.WaitAsync().ConfigureAwait(false);
                try
                {
                    var bytes = await Client.GetByteArrayAsync(address).ConfigureAwait(false);
                    return await Task.Run(() =>
                    {
                        using var stream = new MemoryStream(bytes);
                        return new Bitmap(stream);
                    }).ConfigureAwait(false);
                }
                catch
                {
                    return null;
                }
                finally
                {
                    DownloadSlots.Release();
                }
            });
            var bitmap = await task;
            cancellationToken.ThrowIfCancellationRequested();
            if (bitmap is null)
            {
                Cache.TryRemove(key, out _);
                return;
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested) return;
                Source = bitmap;
                Opacity = 1;
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
