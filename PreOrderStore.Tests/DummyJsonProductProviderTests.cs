using System.Net;
using System.Text;
using PreOrderStore.Infrastructure;

namespace PreOrderStore.Tests;

public sealed class DummyJsonProductProviderTests
{
    [Fact]
    public async Task GetProducts_MapsDummyJsonAndAppliesOnlyIllustrativePriceMultiplier()
    {
        const string payload = """
            {"products":[{"id":7,"title":"Sample phone","description":"Placeholder","category":"smartphones","price":9.99,"thumbnail":"https://cdn.example.test/phone.webp","stock":4}],"total":1,"skip":0,"limit":0}
            """;
        using var client = new HttpClient(new StubHandler(payload))
        {
            BaseAddress = new Uri("https://dummyjson.test/")
        };
        var provider = new DummyJsonProductProvider(client);

        var product = Assert.Single(await provider.GetProductsAsync());

        Assert.Equal("7", product.ExternalId);
        Assert.Equal("Sample phone", product.Name);
        Assert.Equal("smartphones", product.Category);
        Assert.Equal(899.10m, product.Price);
        Assert.Equal("https://cdn.example.test/phone.webp", product.ImageAssetPath);
        Assert.Equal(4, product.AvailableQuantity);
        Assert.True(product.IsAvailable);
        Assert.Null(product.ExpectedArrivalDate);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://dummyjson.test/products?limit=0&select=id,title,description,category,price,thumbnail,stock", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
