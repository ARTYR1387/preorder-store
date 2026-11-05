using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PreOrderStore.Services;

public sealed record DemoBankPaymentResult(bool Approved);

internal sealed record DemoBankPipeResponse(Guid PaymentId, string Token, long AmountMinor, bool Approved);

public sealed class DemoBankPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<DemoBankPaymentResult> PayAsync(
        Guid paymentId,
        long amountMinor,
        string orderReference,
        string itemSummary,
        CancellationToken cancellationToken = default)
    {
        if (paymentId == Guid.Empty) throw new ArgumentException("Неверный идентификатор операции.", nameof(paymentId));
        if (amountMinor <= 0) throw new ArgumentOutOfRangeException(nameof(amountMinor));

        var projectPath = ResolveProjectPath();
        if (projectPath is null)
            throw new InvalidOperationException("Не найдено приложение Demo Bank. Разместите репозитории рядом или задайте PREORDER_DEMO_BANK_PROJECT.");

        var pipeName = "preorder_demo_bank_" + Guid.NewGuid().ToString("N");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(projectPath)!
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--");
        AddArgument(startInfo, "--pipe", pipeName);
        AddArgument(startInfo, "--token", token);
        AddArgument(startInfo, "--payment-id", paymentId.ToString("D"));
        AddArgument(startInfo, "--amount-minor", amountMinor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--order-reference", orderReference);
        AddArgument(startInfo, "--item-summary", itemSummary);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Не удалось запустить Demo Bank через dotnet.");

        try
        {
            var connectionTask = server.WaitForConnectionAsync(linked.Token);
            var exitTask = process.WaitForExitAsync(linked.Token);
            var firstCompleted = await Task.WhenAny(connectionTask, exitTask);
            if (firstCompleted == exitTask)
            {
                await exitTask;
                if (!server.IsConnected)
                    throw new InvalidOperationException("Demo Bank закрылся, не отправив ответ. Корзина не изменена.");
            }
            await connectionTask;

            using var reader = new StreamReader(server, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var line = await reader.ReadLineAsync(linked.Token);
            if (string.IsNullOrWhiteSpace(line))
                throw new InvalidOperationException("Demo Bank не передал подтверждение. Корзина не изменена.");

            var response = JsonSerializer.Deserialize<DemoBankPipeResponse>(line, JsonOptions)
                ?? throw new InvalidOperationException("Ответ Demo Bank имеет неверный формат.");
            if (response.PaymentId != paymentId || response.AmountMinor != amountMinor || !TokenMatches(token, response.Token))
                throw new InvalidOperationException("Не удалось проверить ответ Demo Bank. Заказ не создан.");

            return new DemoBankPaymentResult(response.Approved);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Demo Bank не ответил за 3 минуты. Корзина сохранена.");
        }
        finally
        {
            linked.Cancel();
            if (!process.HasExited)
            {
                try
                {
                    process.CloseMainWindow();
                    using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(closeTimeout.Token);
                }
                catch
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
            }
        }
    }

    private static string? ResolveProjectPath()
    {
        var configured = Environment.GetEnvironmentVariable("PREORDER_DEMO_BANK_PROJECT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Path.GetFullPath(configured);
            if (Directory.Exists(path)) path = Path.Combine(path, "DemoBank.csproj");
            if (File.Exists(path)) return path;
            throw new FileNotFoundException("Файл DemoBank.csproj из PREORDER_DEMO_BANK_PROJECT не найден.", path);
        }

        var starts = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory };
        var checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in starts)
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (checkedDirectories.Add(directory.FullName))
                {
                    var candidates = new[]
                    {
                        Path.Combine(directory.FullName, "demo-bank", "DemoBank.csproj"),
                        Path.Combine(directory.FullName, "Demo Bank", "DemoBank.csproj"),
                        Path.Combine(directory.FullName, "DemoBank", "DemoBank.csproj")
                    };
                    var found = candidates.FirstOrDefault(File.Exists);
                    if (found is not null) return found;
                }
                directory = directory.Parent;
            }
        }
        return null;
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static bool TokenMatches(string expectedHex, string actualHex)
    {
        try
        {
            var expected = Convert.FromHexString(expectedHex);
            var actual = Convert.FromHexString(actualHex);
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
