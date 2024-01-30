using System.Text.Json;
using System.Web;
using Blazored.LocalStorage;
using Nerves.Shared.Models.Auth;
using Nerves.Shared.Models.User;

namespace Nerves.Dashboard.Services.Auth;

public class AuthenticationManager
{
    private HttpClient? HttpClient { get; set; }

    private ILocalStorageService? LocalStorage { get; set; }

    private readonly string baseUrl = Instances.BaseUrl;

    private readonly static JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string? DeviceId { get; set; }

    public AuthenticationManager()
    {
        Console.WriteLine($"@Init: {nameof(AuthenticationManager)}");
    }

    public async Task<AuthenticationManager> Init()
    {
        if (await LocalStorage!.ContainKeyAsync("deviceId"))
            DeviceId = await LocalStorage!.GetItemAsStringAsync("deviceId");
        else
        {
            var deviceId = Guid.NewGuid().ToString();
            await LocalStorage!.SetItemAsStringAsync("deviceId", deviceId);
            DeviceId = deviceId;
        }

        return this;
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

    public async Task<UserToken?> GetUserToken(string name, string password)
    {
        var url = $"{baseUrl}Api/User/Login/{name}?password={password}&deviceId={DeviceId}";

        Console.WriteLine(url);

        var response = await (HttpClient?.GetAsync(url) ?? throw new ArgumentNullException(nameof(HttpClient)));
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<UserToken>(body, jsonOptions);
            return token;
        }
        else return null;
    }

    public static Token? GetToken(UserToken userToken, string deviceId)
    {
        return userToken.GetToken(deviceId);
    }

    public async Task<User?> GetUser(string name, string token)
    {
        var url = $"{baseUrl}Api/User/{name}?token={token}&deviceId={DeviceId}";

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
        var userToken = await GetUserToken(name, password);
        if (userToken is null) return false;

        var token = GetToken(userToken, DeviceId!);
        if (token is null) return false;

        var user = await GetUser(name, token.Value.Value!);
        if (user is null) return false;

        LoginUser = user;

        LoginStateChanged();

        await LocalStorage!.SetItemAsStringAsync("userId", name);
        await LocalStorage!.SetItemAsStringAsync("userToken", JsonSerializer.Serialize(token));

        return true;
    }

    public async Task<bool> UpdatePassword(string name, string token, string new_passwd)
    {
        var url = $"{baseUrl}Api/User/Update/{name}?token={token}&new_passwd={new_passwd}&deviceId={DeviceId}";

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

    public async Task<Token?> GetLocalStorageToken()
    {
        if (await LocalStorage!.ContainKeyAsync("userToken"))
            return JsonSerializer.Deserialize<Token>(
                await LocalStorage!.GetItemAsStringAsync("userToken")
            );
        else return null;
    }

    public async void ContinueLatestLogin()
    {
        if (HasLogin) return;

        var signed = await LocalStorage!.ContainKeyAsync("userId") && await LocalStorage!.ContainKeyAsync("userToken");

        if (!signed) return;

        var token = JsonSerializer.Deserialize<Token>(await LocalStorage!.GetItemAsStringAsync("userToken"));

        if (token.Value is null)
            return;

        var user = await GetUser(await LocalStorage!.GetItemAsStringAsync("userId"), token.Value);

        if (user is null) return;

        LoginUser = user;

        LoginStateChanged();
    }

    public User? LoginUser { get; set; }

    public bool HasLogin => LoginUser is not null;
}
