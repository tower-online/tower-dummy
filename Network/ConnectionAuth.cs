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
            ), _cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(_cancellationToken);
            var root = JsonDocument.Parse(body).RootElement;
            
            var jwtElem = root.GetProperty("jwt");
            return jwtElem.GetString();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Error requesting token: {}", ex.Message);
        }
        catch (Exception)
        {
            _logger.LogError("Invalid JSON");
        }

        return null;
    }

    public async Task<List<string>?> RequestCharacters(string username, string? token)
    {
        if (token is null) return null;
        
        var url = $"https://{Settings.RemoteHost}:8000/characters";
        var requestData = new Dictionary<string, string>
        {
            ["platform"] = "TEST",
            ["username"] = username,
            ["jwt"] = token
        };
        
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        using var client = new HttpClient(handler);
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json"
            ), _cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(_cancellationToken);
            var root = JsonDocument.Parse(body).RootElement;

            var charactersElem = root.GetProperty("characters");
            List<string> characters = [];
            foreach (var characterElem in charactersElem.EnumerateArray())
            {
                characters.Add(characterElem.GetProperty("name").GetString()!);
            }

            return characters;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Error requesting token: {}", ex.Message);
        }
        catch (Exception)
        {
            _logger.LogError("Invalid JSON");
        }

        return null;
    }
}