using System.Net;
using System.Net.Http.Json;
using IplStore.Application.Cart;
using IplStore.Application.Common.Models;
using IplStore.Application.Orders.Commands;
using IplStore.Application.Orders.Queries;
using IplStore.Application.Products.Queries;
using IplStore.IntegrationTests.Support;

namespace IplStore.IntegrationTests;

/// <summary>Whole-pipeline tests through HTTP: routing, auth, validation, handlers, EF, exception mapping.</summary>
public sealed class ApiEndToEndTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiEndToEndTests(ApiFactory factory) => _factory = factory;

    private HttpClient Anonymous() => _factory.CreateClient();

    private async Task<HttpClient> LoginAsync(string user) => await _factory.CreateClient().LoginAsync(user);

    // ---------- catalog ----------

    [Fact]
    public async Task Catalog_is_public_and_searchable()
    {
        var client = Anonymous();

        var all = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?pageSize=50");
        var csk = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?search=csk");

        Assert.Equal(40, all!.TotalCount);              // 10 franchises x 4 product types
        Assert.Equal(4, csk!.TotalCount);
        Assert.All(csk.Items, p => Assert.Equal("CSK", p.FranchiseCode));
    }

    [Fact]
    public async Task Product_details_include_franchise_and_category()
    {
        var client = Anonymous();
        var product = await client.FirstProductAsync("mumbai");

        var detail = await client.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}");

        Assert.Equal("MI", detail!.FranchiseCode);
        Assert.False(string.IsNullOrEmpty(detail.CategoryName));
    }

    [Fact]
    public async Task Unknown_product_is_a_404_problem_details_response()
    {
        var response = await Anonymous().GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    // ---------- security ----------

    [Theory]
    [InlineData("/api/cart")]
    [InlineData("/api/orders")]
    public async Task Protected_endpoints_reject_anonymous_callers(string url)
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync(url)).StatusCode);

    [Fact]
    public async Task A_token_signed_with_the_wrong_key_is_rejected()
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJoYWNrZXIifQ.invalidsignature");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);
    }

    [Fact]
    public async Task The_token_endpoint_only_issues_tokens_for_seeded_users_and_never_lets_the_caller_pick_a_role()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/token", new { username = "mallory", role = "Admin" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoints_need_the_admin_role()
    {
        var product = await Anonymous().FirstProductAsync("Gujarat Titans Flag");
        var customer = await LoginAsync("customer");
        var admin = await LoginAsync("admin");

        var asCustomer = await customer.PostAsJsonAsync($"/api/admin/products/{product.Id}/restock", new { quantity = 5 });
        var asAdmin = await admin.PostAsJsonAsync($"/api/admin/products/{product.Id}/restock", new { quantity = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, asAdmin.StatusCode);
    }

    // ---------- cart ----------

    [Fact]
    public async Task Cart_ignores_any_client_supplied_price_and_prices_from_the_catalog()
    {
        var customer = await LoginAsync("customer");
        var product = await Anonymous().FirstProductAsync("jersey");

        var response = await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 1, unitPrice = 0.01m });
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();

        Assert.Equal(product.Price, cart!.Items.Single().UnitPrice);
        await customer.DeleteAsync("/api/cart");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(999)]
    public async Task Invalid_quantities_are_a_400(int quantity)
    {
        var customer = await LoginAsync("customer");
        var product = await Anonymous().FirstProductAsync();

        var response = await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Two_users_never_see_each_others_cart()
    {
        var customer = await LoginAsync("customer");
        var admin = await LoginAsync("admin");
        var product = await Anonymous().FirstProductAsync();
        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 1 });

        var adminCart = await admin.GetFromJsonAsync<CartDto>("/api/cart");

        Assert.Empty(adminCart!.Items);
        await customer.DeleteAsync("/api/cart");
    }

    // ---------- checkout + payment + history ----------

    [Fact]
    public async Task Full_purchase_journey_browse_cart_checkout_pay_history()
    {
        var customer = await LoginAsync("customer");
        await customer.DeleteAsync("/api/cart");   // tests share one in-memory cart store: start clean
        var product = await Anonymous().FirstProductAsync("flag");

        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 2 });
        var checkoutResponse = await customer.CheckoutAsync("journey-1");
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>();

        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);
        Assert.Equal("PendingPayment", checkout!.Status);
        Assert.Equal(product.Price * 2, checkout.TotalAmount);
        Assert.Single((await customer.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);   // cart kept until payment succeeds

        var pay = await customer.PostAsJsonAsync($"/api/orders/{checkout.OrderId}/confirm-payment",
            new { gatewayOrderId = checkout.GatewayOrderId, gatewayPaymentId = "pay_it_1", signature = FakePaymentGateway.ValidSignature });
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        Assert.Empty((await customer.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);   // cleared only after the payment is confirmed

        var history = await customer.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/api/orders");
        var order = history!.Items.Single(o => o.Id == checkout.OrderId);
        Assert.Equal("Paid", order.Status);

        var detail = await customer.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{checkout.OrderId}");
        Assert.Equal(2, detail!.Items.Single().Quantity);
    }

    [Fact]
    public async Task A_failed_or_abandoned_payment_leaves_the_cart_intact_and_a_returning_customer_gets_the_same_open_order()
    {
        var customer = await LoginAsync("customer");
        await customer.DeleteAsync("/api/cart");
        var product = await Anonymous().FirstProductAsync("Delhi Capitals Cap");
        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 2 });

        var first = await (await customer.CheckoutAsync("abandon-1")).Content.ReadFromJsonAsync<CheckoutResponse>();
        var forged = await customer.PostAsJsonAsync($"/api/orders/{first!.OrderId}/confirm-payment",
            new { gatewayOrderId = first.GatewayOrderId, gatewayPaymentId = "pay_fail", signature = "forged" });

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal(2, (await customer.GetFromJsonAsync<CartDto>("/api/cart"))!.Items.Single().Quantity);   // cart still there

        // The customer goes back to the cart and presses "Place order" again (a new key): same open order, no second reservation.
        var again = await (await customer.CheckoutAsync("abandon-2")).Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.Equal(first.OrderId, again!.OrderId);
        Assert.Equal(first.GatewayOrderId, again.GatewayOrderId);
    }

    [Fact]
    public async Task Checkout_without_an_idempotency_key_is_a_400()
    {
        var customer = await LoginAsync("customer");
        var product = await Anonymous().FirstProductAsync("cap");
        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 1 });

        var response = await customer.PostAsync("/api/orders/checkout", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await customer.DeleteAsync("/api/cart");
    }

    [Fact]
    public async Task Retrying_checkout_with_the_same_key_returns_the_same_order()
    {
        var customer = await LoginAsync("customer");
        await customer.DeleteAsync("/api/cart");
        var product = await Anonymous().FirstProductAsync("cap");
        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 1 });

        var first = await (await customer.CheckoutAsync("retry-1")).Content.ReadFromJsonAsync<CheckoutResponse>();
        var second = await (await customer.CheckoutAsync("retry-1")).Content.ReadFromJsonAsync<CheckoutResponse>();

        Assert.Equal(first!.OrderId, second!.OrderId);
    }

    [Fact]
    public async Task A_forged_payment_signature_is_a_400_and_the_order_stays_pending()
    {
        var customer = await LoginAsync("customer");
        await customer.DeleteAsync("/api/cart");
        var product = await Anonymous().FirstProductAsync("cap");
        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 1 });
        var checkout = await (await customer.CheckoutAsync("forged-1")).Content.ReadFromJsonAsync<CheckoutResponse>();

        var pay = await customer.PostAsJsonAsync($"/api/orders/{checkout!.OrderId}/confirm-payment",
            new { gatewayOrderId = checkout.GatewayOrderId, gatewayPaymentId = "pay_x", signature = "forged" });

        Assert.Equal(HttpStatusCode.BadRequest, pay.StatusCode);
        var detail = await customer.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{checkout.OrderId}");
        Assert.Equal("PendingPayment", detail!.Status);
    }

    [Fact]
    public async Task One_user_cannot_read_or_pay_another_users_order()
    {
        var customer = await LoginAsync("customer");
        await customer.DeleteAsync("/api/cart");
        var admin = await LoginAsync("admin");
        var product = await Anonymous().FirstProductAsync("cap");
        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 1 });
        var checkout = await (await customer.CheckoutAsync("owner-1")).Content.ReadFromJsonAsync<CheckoutResponse>();

        var read = await admin.GetAsync($"/api/orders/{checkout!.OrderId}");
        var pay = await admin.PostAsJsonAsync($"/api/orders/{checkout.OrderId}/confirm-payment",
            new { gatewayOrderId = checkout.GatewayOrderId, gatewayPaymentId = "pay_y", signature = FakePaymentGateway.ValidSignature });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pay.StatusCode);
    }

    [Fact]
    public async Task Buying_more_than_is_in_stock_is_a_409_and_stock_is_not_taken()
    {
        var customer = await LoginAsync("customer");
        var photo = await Anonymous().FirstProductAsync("Lucknow Super Giants Autographed Photo");   // seeded with only 5 units, untouched by other tests

        var tooMany = await customer.PostAsJsonAsync("/api/cart/items", new { productId = photo.Id, quantity = 6 });

        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
    }

    // ---------- payment helpers used by the UI ----------

    [Fact]
    public async Task Payment_config_requires_login_and_exposes_only_the_public_key_id()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().GetAsync("/api/payments/config")).StatusCode);

        var customer = await LoginAsync("customer");
        var body = await customer.GetStringAsync("/api/payments/config");

        Assert.Contains("rzp_test_integration", body);
        Assert.DoesNotContain(FakePaymentGateway.Secret, body);   // the secret never leaves the server
    }

    [Fact]
    public async Task Simulated_payment_pays_the_callers_own_order_through_the_normal_confirmation_path()
    {
        var customer = await LoginAsync("customer");
        await customer.DeleteAsync("/api/cart");
        var admin = await LoginAsync("admin");
        var product = await Anonymous().FirstProductAsync("Rajasthan Royals Flag");
        await customer.PostAsJsonAsync("/api/cart/items", new { productId = product.Id, quantity = 1 });
        var checkout = await (await customer.CheckoutAsync("sim-1")).Content.ReadFromJsonAsync<CheckoutResponse>();

        var someoneElse = await admin.PostAsync($"/api/dev/orders/{checkout!.OrderId}/simulate-payment", content: null);
        var own = await customer.PostAsync($"/api/dev/orders/{checkout.OrderId}/simulate-payment", content: null);

        Assert.Equal(HttpStatusCode.NotFound, someoneElse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var detail = await customer.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{checkout.OrderId}");
        Assert.Equal("Paid", detail!.Status);
    }

    // ---------- operations ----------

    [Fact]
    public async Task Health_endpoints_are_anonymous()
    {
        var client = Anonymous();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
