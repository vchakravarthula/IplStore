using System.Net.Http.Headers;
using System.Net.Http.Json;
using IplStore.Application.Common.Models;
using IplStore.Application.Products.Queries;

namespace IplStore.IntegrationTests.Support;

public static class ApiClientExtensions
{
    public sealed record TokenResponse(string AccessToken);

    /// <summary>Gets a dev token for the seeded "customer" or "admin" user and sets it on the client.</summary>
    public static async Task<HttpClient> LoginAsync(this HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/token", new { username });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public static async Task<ProductDto> FirstProductAsync(this HttpClient client, string? search = null)
    {
        var url = search is null ? "/api/products?pageSize=1" : $"/api/products?pageSize=1&search={Uri.EscapeDataString(search)}";
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>(url);
        return page!.Items.First();
    }

    public static Task<HttpResponseMessage> CheckoutAsync(this HttpClient client, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/checkout");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }
}
