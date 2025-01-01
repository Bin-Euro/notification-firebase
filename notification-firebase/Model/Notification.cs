using System.Text.Json.Serialization;

namespace notification_firebase.Model
{
    public class Notification
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } // ID của thông báo

        [JsonPropertyName("title")]
        public string Title { get; set; } // Tiêu đề

        [JsonPropertyName("body")]
        public string Body { get; set; } // Nội dung

        [JsonPropertyName("targetActivity")]
        public string TargetActivity { get; set; } // Hoạt động điều hướng

        [JsonPropertyName("extraData")]
        public Dictionary<string, string>? ExtraData { get; set; } // Dữ liệu bổ sung

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } // Thời gian gửi

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; } // Trạng thái đọc
    }
}
