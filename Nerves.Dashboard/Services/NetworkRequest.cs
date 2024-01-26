namespace Nerves.Dashboard.Services;

public class NetworkRequest
{
    private HttpClient? HttpClient { get; set; }

    public NetworkRequest()
    {
        Console.WriteLine($"@Init: {nameof(NetworkRequest)}");
    }

    public NetworkRequest SetHttpClient(HttpClient client)
    {
        HttpClient = client;
        return this;
    }

    public async Task<string?> GetAsync(string url)
    {
        var request = await HttpClient!.GetAsync(url);

        if (request.IsSuccessStatusCode)
        {
            var body = await request.Content.ReadAsStringAsync();
            return body;
        }
        else return null;
    }

    public async Task<bool> DeleteAsync(string url)
    {
        var request = await HttpClient!.DeleteAsync(url);
        return request.IsSuccessStatusCode;
    }
}