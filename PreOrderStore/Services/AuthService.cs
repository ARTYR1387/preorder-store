using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PreOrderStore.Data;
using PreOrderStore.Domain;

namespace PreOrderStore.Services;

public sealed class UserSession
{
    public StoreCustomer? Customer { get; internal set; }
    public bool IsSignedIn => Customer is not null;
    public void SignOut() => Customer = null;
}

public sealed class AuthService(StoreDbContext db, UserSession session)
{
    private const int Iterations = 310_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public UserSession Session => session;

    public async Task<RegistrationResult> RegisterAsync(string displayName, string email, string password,
        CancellationToken cancellationToken = default)
    {
        var name = displayName.Trim();
        var normalizedEmail = NormalizeEmail(email);
        if (name.Length is < 2 or > 100) return new(false, "Введите имя длиной от 2 до 100 символов.");
        if (!IsValidEmail(normalizedEmail)) return new(false, "Проверьте формат электронной почты.");
        if (password.Length < 8) return new(false, "Пароль должен содержать не менее 8 символов.");
        if (await db.Customers.AnyAsync(x => x.EmailNormalized == normalizedEmail, cancellationToken))
            return new(false, "Аккаунт с этой почтой уже зарегистрирован.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        var isFirstAdmin = !await db.Customers.AnyAsync(x => x.IsAdmin, cancellationToken);
        var customer = new StoreCustomer
        {
            DisplayName = name,
            EmailNormalized = normalizedEmail,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash),
            IsAdmin = isFirstAdmin,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        session.Customer = customer;
        return new(true, isFirstAdmin
            ? "Создан первый аккаунт с правами администратора этого устройства."
            : "Аккаунт покупателя создан. Добро пожаловать в магазин.");
    }

    public async Task<RegistrationResult> SignInAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.EmailNormalized == normalizedEmail, cancellationToken);
        if (customer is null || !VerifyPassword(password, customer.PasswordSalt, customer.PasswordHash))
            return new(false, "Неверная почта или пароль.");
        session.Customer = customer;
        return new(true, "Вход выполнен.");
    }

    private static bool VerifyPassword(string password, string saltText, string expectedHashText)
    {
        try
        {
            var salt = Convert.FromBase64String(saltText);
            var expected = Convert.FromBase64String(expectedHashText);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeEmail(string? email) => email?.Trim().ToUpperInvariant() ?? string.Empty;

    private static bool IsValidEmail(string normalized)
    {
        try
        {
            return new MailAddress(normalized).Address == normalized;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
