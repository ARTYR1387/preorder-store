using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PreOrderStore.Data;
using PreOrderStore.Domain;
using PreOrderStore.Services;

namespace PreOrderStore.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Register_StoresSaltedHashAndAllowsCaseInsensitiveSignIn()
    {
        await using var fixture = await Fixture.CreateAsync();
        var session = new UserSession();
        var auth = new AuthService(fixture.Db, session);

        var registered = await auth.RegisterAsync("Test User", "test@example.com", "correct-horse-42");

        Assert.True(registered.Succeeded);
        Assert.True(session.IsSignedIn);
        var stored = await fixture.Db.Customers.SingleAsync();
        Assert.Equal("TEST@EXAMPLE.COM", stored.EmailNormalized);
        Assert.NotEqual("correct-horse-42", stored.PasswordHash);
        Assert.NotEmpty(stored.PasswordSalt);

        session.SignOut();
        var signedIn = await auth.SignInAsync("TEST@Example.com", "correct-horse-42");
        Assert.True(signedIn.Succeeded);
        Assert.Equal(stored.Id, session.Customer!.Id);

        session.SignOut();
        var wrongPassword = await auth.SignInAsync("test@example.com", "not-the-password");
        Assert.False(wrongPassword.Succeeded);
        Assert.False(session.IsSignedIn);
    }

    [Fact]
    public async Task Register_RejectsWeakPasswordAndDuplicateEmail()
    {
        await using var fixture = await Fixture.CreateAsync();
        var auth = new AuthService(fixture.Db, new UserSession());

        var weak = await auth.RegisterAsync("Test User", "test@example.com", "short");
        Assert.False(weak.Succeeded);

        var valid = await auth.RegisterAsync("Test User", "test@example.com", "correct-horse-42");
        Assert.True(valid.Succeeded);

        var duplicate = await auth.RegisterAsync("Another Name", "TEST@EXAMPLE.COM", "correct-horse-42");
        Assert.False(duplicate.Succeeded);
        Assert.Single(await fixture.Db.Customers.ToListAsync());
    }

    [Fact]
    public async Task Register_GrantsAdminToFirstAccountOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var auth = new AuthService(fixture.Db, new UserSession());

        var first = await auth.RegisterAsync("Владелец", "owner@example.com", "first-password-42");
        var second = await auth.RegisterAsync("Покупатель", "buyer@example.com", "second-password-42");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var users = await fixture.Db.Customers.OrderBy(x => x.Id).ToListAsync();
        Assert.True(users[0].IsAdmin);
        Assert.False(users[1].IsAdmin);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public StoreDbContext Db { get; }

        private Fixture(SqliteConnection connection, StoreDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
            var db = new StoreDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
