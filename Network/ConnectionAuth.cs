using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HttpClient = System.Net.Http.HttpClient;
using Tower.System;

namespace Tower.Network;

public partial class Connection
{
    public async Task<string?> RequestAuthToken(string username)
    {
        var url = $"https://{Settings.RemoteHost}:8000/token/test";
        var requestData = new Dictionary<string, string>
        {
            ["username"] = username
        };


        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        using var client = new HttpClient(handler);
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json"
            ));
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body).RootElement;
            if (json.TryGetProperty("jwt", out var jwtElem))
            {
                var jwt = jwtElem.GetString();
                // GD.Print($"[{nameof(Connection)}] Requesting auth token succeed: {jwt}");
                return jwt;
            }

            _logger.LogError("Invalid json");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Error requesting token: {}", ex.Message);
        }

        return null;
    }
}