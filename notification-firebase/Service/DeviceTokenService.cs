using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using notification_firebase.Model;

public class DeviceTokenService
{
    private readonly string _projectId;
    private readonly IConfiguration _configuration;
    private readonly GoogleCredential googleCredential;
    public DeviceTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        googleCredential = GoogleCredential
            .FromFile(_configuration["Firebase:Notification:jsonPath"])
            .CreateScoped(new[] {
                "https://www.googleapis.com/auth/firebase.messaging",
                "https://www.googleapis.com/auth/cloud-platform"
            });

        _projectId = _configuration["Firebase:Notification:projectid"];
    }

    private async Task<string> GenerateAccessToken()
    {
        return await googleCredential.UnderlyingCredential.GetAccessTokenForRequestAsync();
    }

    // Lưu Device Token theo AccountID vào Realtime Database
    public async Task SaveDeviceTokenAsync(string accountId, string deviceToken, string deviceInfo)
    {
        var url = $"https://{_projectId}-default-rtdb.asia-southeast1.firebasedatabase.app/deviceTokens/{accountId}/{deviceToken}.json";
        var data = new
        {
            deviceInfo,
            lastUpdated = DateTime.UtcNow.ToString("o")
        };

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to save device token: {response.ReasonPhrase}");
    }

    // Xoá Device Token khi Logout
    public async Task DeleteDeviceTokenAsync(string accountId, string deviceToken)
    {
        var url = $"https://{_projectId}-default-rtdb.asia-southeast1.firebasedatabase.app/deviceTokens/{accountId}/{deviceToken}.json";

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Delete, url);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to delete device token: {response.ReasonPhrase}");
    }

    // Gửi thông báo đến tất cả thiết bị
    public async Task<string> SendNotificationToAllAsync(string title, string body, string targetActivity, Dictionary<string, string>? extraData = null)
    {
        var allAccountsData = await FetchAllAccountsTokensAsync($"https://{_projectId}-default-rtdb.asia-southeast1.firebasedatabase.app/deviceTokens.json");

        var tokens = new List<string>();
        foreach (var accountTokens in allAccountsData.Values)
        {
            tokens.AddRange(accountTokens);
        }

        if (!tokens.Any())
            return "No device tokens found.";

        foreach (var token in tokens)
        {
            await SendNotificationAsync(title, body, token, targetActivity, extraData);
        }

        return "Notification sent to all devices.";
    }





    // Gửi thông báo đến một AccountID
    public async Task<string> SendNotificationToAccountAsync(string accountId, string title, string body, string targetActivity, Dictionary<string, string>? extraData = null)
    {
        var tokens = await FetchDeviceTokensAsync($"https://{_projectId}-default-rtdb.asia-southeast1.firebasedatabase.app/deviceTokens/{accountId}.json");

        if (tokens == null || !tokens.Any())
            return $"No device tokens found for AccountID {accountId}.";

        foreach (var token in tokens)
        {
            await SendNotificationAsync(title, body, token, targetActivity, extraData);
        }

        return $"Notification sent to AccountID {accountId}.";
    }




    private async Task<List<string>> FetchDeviceTokensAsync(string url)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch device tokens: {response.ReasonPhrase}");

        var responseData = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(responseData) || responseData == "null")
            return new List<string>(); // Trả về danh sách rỗng nếu không có dữ liệu

        // Deserialize dữ liệu JSON
        var tokensData = JsonSerializer.Deserialize<Dictionary<string, object>>(responseData);
        if (tokensData == null)
            return new List<string>();

        // Trường hợp tokens là dictionary với cấu trúc phức tạp
        var tokens = new List<string>();
        foreach (var item in tokensData)
        {
            if (item.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                tokens.Add(item.Key); // Key là deviceToken
            }
        }

        return tokens;
    }
    private async Task<Dictionary<string, List<string>>> FetchAllAccountsTokensAsync(string url)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch device tokens: {response.ReasonPhrase}");

        var responseData = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(responseData) || responseData == "null")
            return new Dictionary<string, List<string>>(); // Trả về danh sách rỗng nếu không có dữ liệu

        // Deserialize dữ liệu JSON
        var accountsData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(responseData);
        if (accountsData == null)
            return new Dictionary<string, List<string>>();

        // Tạo dictionary chứa danh sách tokens theo từng accountId
        var result = new Dictionary<string, List<string>>();
        foreach (var account in accountsData)
        {
            var tokens = new List<string>();
            foreach (var token in account.Value.Keys)
            {
                tokens.Add(token);
            }
            result[account.Key] = tokens;
        }

        return result;
    }




    // Gửi thông báo đến một thiết bị cụ thể
    public async Task<string> SendNotificationAsync(string title, string body, string deviceToken, string targetActivity, Dictionary<string, string>? extraData = null)
    {
        var message = new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title, body },
                data = extraData
            }
        };

        var url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";
        var jsonData = JsonSerializer.Serialize(message);

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { { "Authorization", $"Bearer {await GenerateAccessToken()}" } },
            Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to send notification: {response.ReasonPhrase}");

        // Lưu thông báo
        var accountId = await GetAccountIdFromDeviceTokenAsync(deviceToken);

        // Nếu không tìm được `accountId`, thông báo không được lưu
        if (string.IsNullOrEmpty(accountId))
            throw new Exception($"Failed to find accountId for deviceToken: {deviceToken}");

        // Lưu thông báo
        await SaveNotificationAsync(accountId, title, body, targetActivity, extraData);


        return await response.Content.ReadAsStringAsync();
    }
    private async Task<string?> GetAccountIdFromDeviceTokenAsync(string deviceToken)
    {
        var url = $"https://{_projectId}-default-rtdb.asia-southeast1.firebasedatabase.app/deviceTokens.json";

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch device tokens: {response.ReasonPhrase}");

        var responseData = await response.Content.ReadAsStringAsync();
        var deviceTokensData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(responseData);

        if (deviceTokensData != null)
        {
            foreach (var account in deviceTokensData)
            {
                if (account.Value.ContainsKey(deviceToken))
                {
                    return account.Key; // Trả về `accountId`
                }
            }
        }

        return null; // Không tìm thấy
    }


    // Lấy danh sách thông báo theo AccountID từ Realtime Database
    public async Task<List<Notification>> GetNotificationsByAccountIdAsync(string accountId)
    {
        var url = $"https://{_projectId}-default-rtdb.asia-southeast1.firebasedatabase.app/notifications/{accountId}.json";

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to retrieve notifications: {response.ReasonPhrase}");

        var responseData = await response.Content.ReadAsStringAsync();

        // Deserialize thành dictionary
        var notificationsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseData);

        if (notificationsDict == null)
            return new List<Notification>();

        var notifications = new List<Notification>();

        foreach (var entry in notificationsDict)
        {
            var id = entry.Key;
            var jsonElement = entry.Value;

            try
            {
                // Parse từng thông báo
                var notification = JsonSerializer.Deserialize<Notification>(jsonElement.GetRawText());
                if (notification != null)
                {
                    notification.Id = id;
                    notifications.Add(notification);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing notification with ID {id}: {ex.Message}");
            }
        }

        return notifications;
    }



    // Lưu thông báo theo AccountID vào Realtime Database
    // Lưu thông báo theo AccountID vào Realtime Database
    public async Task SaveNotificationAsync(string accountId, string title, string body, string targetActivity, Dictionary<string, string>? extraData)
    {
        var notificationId = Guid.NewGuid().ToString(); // Tạo ID duy nhất cho thông báo
        var notification = new
        {
            id = notificationId,
            title = title,
            body = body,
            timestamp = DateTime.UtcNow.ToString("o"),
            targetActivity = targetActivity,
            extraData = extraData
        };

        var jsonNotification = JsonSerializer.Serialize(notification);

        using var client = new HttpClient();
        var url = $"https://{_projectId}-default-rtdb.asia-southeast1.firebasedatabase.app/notifications/{accountId}/{notificationId}.json";

        var response = await client.PutAsync(url, new StringContent(jsonNotification, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to save notification: {response.StatusCode} - {error}");
        }
    }


}

public class FirebaseConfig
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("project_id")] public string ProjectId { get; set; }
    [JsonPropertyName("private_key_id")] public string PrivateKeyId { get; set; }
    [JsonPropertyName("private_key")] public string PrivateKey { get; set; }
    [JsonPropertyName("client_email")] public string ClientEmail { get; set; }
    [JsonPropertyName("client_id")] public string ClientId { get; set; }
    [JsonPropertyName("auth_uri")] public string AuthUri { get; set; }
    [JsonPropertyName("token_uri")] public string TokenUri { get; set; }
    [JsonPropertyName("auth_provider_x509_cert_url")] public string AuthProviderX509CertUrl { get; set; }
    [JsonPropertyName("client_x509_cert_url")] public string ClientX509CertUrl { get; set; }
}
