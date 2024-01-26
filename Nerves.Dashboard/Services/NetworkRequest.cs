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

    public async Task<string?> RequestAsync(string url)
    {
        var request = await HttpClient!.GetAsync(url);

        if (request.IsSuccessStatusCode)
        {
            var body = await request.Content.ReadAsStringAsync();
            return body;
        }
        else return null;
    }
}