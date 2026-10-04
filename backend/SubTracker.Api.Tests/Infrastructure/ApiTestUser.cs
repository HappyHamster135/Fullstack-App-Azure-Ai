using System.Net.Http.Headers;
using System.Net.Http.Json;
using SubTracker.Api.Dtos.Auth;
using SubTracker.Api.Dtos.Categories;
using SubTracker.Api.Dtos.Subscriptions;
using SubTracker.Api.Entities;

namespace SubTracker.Api.Tests.Infrastructure;

/// <summary>
/// En nyregistrerad användare med egen inloggad HTTP-klient. Varje test skapar sin egen användare
/// med unik e-post, så att testerna inte påverkar varandra även om de delar databas.
/// </summary>
public sealed class ApiTestUser
{
    private const string Password = "Lösenord123";

    private ApiTestUser(HttpClient client, UserResponse user, List<CategoryResponse> categories)
    {
        Client = client;
        User = user;
        Categories = categories;
    }

    public HttpClient Client { get; }

    public UserResponse User { get; }

    /// <summary>Standardkategorierna som skapas vid registrering.</summary>
    public IReadOnlyList<CategoryResponse> Categories { get; }

    public static async Task<ApiTestUser> RegisterAsync(ApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password,
        });
        registerResponse.EnsureSuccessStatusCode();

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Registreringen returnerade inget svar.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories")
            ?? throw new InvalidOperationException("Kategorierna kunde inte läsas.");

        return new ApiTestUser(client, auth.User, categories);
    }

    public async Task<SubscriptionResponse> AddSubscriptionAsync(
        string name,
        decimal price,
        BillingInterval interval,
        DateOnly nextPaymentDate,
        bool isActive = true,
        int? categoryId = null)
    {
        var request = new SubscriptionRequest
        {
            Name = name,
            Price = price,
            BillingInterval = interval,
            StartDate = nextPaymentDate.AddYears(-1),
            NextPaymentDate = nextPaymentDate,
            IsActive = isActive,
            CategoryId = categoryId ?? Categories[0].Id,
        };

        var response = await Client.PostAsJsonAsync("/api/subscriptions", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SubscriptionResponse>()
            ?? throw new InvalidOperationException("Prenumerationen returnerade inget svar.");
    }
}
