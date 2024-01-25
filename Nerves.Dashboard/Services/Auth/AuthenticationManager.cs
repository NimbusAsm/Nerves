using System.Text.Json;
using Nerves.Shared.Models;

namespace Nerves.Dashboard.Services.Auth;

public class AuthenticationManager
{
    private HttpClient? HttpClient { get; set; }

    private readonly string baseUrl = "http://localhost:5252/";

    private readonly static JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AuthenticationManager()
    {
        Console.WriteLine($"@Init: {nameof(AuthenticationManager)}");
    }

    public AuthenticationManager SetHttpClient(HttpClient client)
    {
        HttpClient = client;
        return this;
    }

    public event Action? OnChange;

    private void LoginStateChanged() => OnChange?.Invoke();

    private async Task<UserToken?> GetToken(string name, string password)
    {
        var url = $"{baseUrl}Api/User/Login/{name}?password={password}";

        var response = await (HttpClient?.GetAsync(url) ?? throw new ArgumentNullException(nameof(HttpClient)));
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<UserToken>(body, jsonOptions);
            return token;
        }
        else return null;
    }

    private async Task<User?> GetUser(string name, string token)
    {
        var url = $"{baseUrl}Api/User/{name}?token={token}";

        var response = await (HttpClient?.GetAsync(url) ?? throw new ArgumentNullException(nameof(HttpClient)));
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<User>(body, jsonOptions);
            return user;
        }
        else return null;
    }

    public async Task<bool> SignIn(string name, string password)
    {
        var token = await GetToken(name, password);
        if (token is null) return false;

        var user = await GetUser(name, token.Token!);
        if (user is null) return false;

        LoginUser = user;

        LoginStateChanged();

        return true;
    }

    public void Logout()
    {
        LoginUser = null;

        LoginStateChanged();
    }

    public User? LoginUser { get; set; }

    public bool HasLogin => LoginUser is not null;
}
