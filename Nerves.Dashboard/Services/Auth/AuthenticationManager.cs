using System.Text.Json;
using Blazored.LocalStorage;
using Nerves.Shared.Models;

namespace Nerves.Dashboard.Services.Auth;

public class AuthenticationManager
{
    private HttpClient? HttpClient { get; set; }

    private ILocalStorageService? LocalStorage { get; set; }

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

    public AuthenticationManager SetLocalStorage(ILocalStorageService storageService)
    {
        LocalStorage = storageService;
        return this;
    }

    public event Action? OnChange;

    private void LoginStateChanged() => OnChange?.Invoke();

    public async Task<UserToken?> GetToken(string name, string password)
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

    public async Task<User?> GetUser(string name, string token)
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

        await LocalStorage!.SetItemAsStringAsync("userId", name);
        await LocalStorage!.SetItemAsStringAsync("userToken", JsonSerializer.Serialize(token));

        return true;
    }

    public async Task<bool> UpdatePassword(string name, string token, string new_passwd)
    {
        var url = $"{baseUrl}Api/User/Update/{name}?token={token}&&new_passwd={new_passwd}";

        var response = await (HttpClient?.GetAsync(url) ?? throw new ArgumentNullException(nameof(HttpClient)));
        if (response.IsSuccessStatusCode)
            return true;
        else return false;
    }

    public async void Logout()
    {
        LoginUser = null;

        LoginStateChanged();

        await LocalStorage!.RemoveItemAsync("userId");
        await LocalStorage!.RemoveItemAsync("userToken");
    }

    public async void ContinueLatestLogin()
    {
        if (HasLogin) return;

        var signed = await LocalStorage!.ContainKeyAsync("userId") && await LocalStorage!.ContainKeyAsync("userToken");

        if (!signed) return;

        var token = JsonSerializer.Deserialize<UserToken>(await LocalStorage!.GetItemAsStringAsync("userToken"));

        if (token is null || token.Id is null || token.Token is null)
            return;

        var user = await GetUser(token!.Id!, token!.Token!);

        if (user is null) return;

        LoginUser = user;

        LoginStateChanged();
    }

    public User? LoginUser { get; set; }

    public bool HasLogin => LoginUser is not null;
}
