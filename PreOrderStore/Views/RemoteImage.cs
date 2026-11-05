using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;

namespace PreOrderStore.Views;

public sealed class RemoteImage : Image
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly ConcurrentDictionary<string, Task<byte[]>> Cache = new(StringComparer.Ordinal);
    private int _loadVersion;

    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<RemoteImage, string?>(nameof(Url));

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    static RemoteImage()
    {
        UrlProperty.Changed.AddClassHandler<RemoteImage>((control, args) =>
        {
            _ = control.LoadAsync();
        });
    }

    private async Task LoadAsync()
    {
        var version = Interlocked.Increment(ref _loadVersion);
        var url = Url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            Source = null;
            return;
        }

        try
        {
            var task = Cache.GetOrAdd(url, key => Client.GetByteArrayAsync(key));
            var bytes = await task;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != _loadVersion) return;
                using var stream = new MemoryStream(bytes, writable: false);
                Source = new Bitmap(stream);
            });
        }
        catch (HttpRequestException) { Cache.TryRemove(url, out _); Source = null; }
        catch (TaskCanceledException) { Cache.TryRemove(url, out _); Source = null; }
        catch (IOException) { Source = null; }
        catch (ArgumentException) { Source = null; }
    }
}
