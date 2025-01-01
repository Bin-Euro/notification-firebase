using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace notification_firebase.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly DeviceTokenService _deviceTokenService;

        public NotificationController(DeviceTokenService deviceTokenService)
        {
            _deviceTokenService = deviceTokenService;
        }

        /// <summary>
        /// Gửi thông báo đến thiết bị cụ thể.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendNotificationToDevice([FromBody] SendNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceToken))
                return BadRequest(new { error = "Device token is required." });

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { error = "Title and Body are required." });

            try
            {
                var result = await _deviceTokenService.SendNotificationAsync(
                    request.Title,
                    request.Body,
                    request.DeviceToken,
                    "TargetActivityName",
                    request.Data
                );

                return Ok(new { message = "Notification sent successfully.", result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách thông báo của một AccountID.
        /// </summary>
        [HttpGet("notifications/{accountId}")]
        public async Task<IActionResult> GetNotificationsByAccountId(string accountId)
        {
            try
            {
                var notifications = await _deviceTokenService.GetNotificationsByAccountIdAsync(accountId);
                return Ok(new { notifications });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lưu hoặc cập nhật Device Token theo AccountID.
        /// </summary>

        [HttpPost("device-tokens")]
        public async Task<IActionResult> SaveDeviceToken([FromBody] SaveDeviceTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccountId) || string.IsNullOrWhiteSpace(request.DeviceToken))
                return BadRequest(new { error = "AccountId and DeviceToken are required." });

            try
            {
                await _deviceTokenService.SaveDeviceTokenAsync(request.AccountId, request.DeviceToken, request.DeviceInfo);
                return Ok(new { message = "Device token saved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Xoá Device Token khi logout.
        /// </summary>

        [HttpDelete("device-tokens/{accountId}/{deviceToken}")]
        public async Task<IActionResult> DeleteDeviceToken(string accountId, string deviceToken)
        {
            try
            {
                await _deviceTokenService.DeleteDeviceTokenAsync(accountId, deviceToken);
                return Ok(new { message = "Device token deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gửi thông báo đến tất cả các thiết bị.
        /// </summary>

        [HttpPost("send/all")]
        public async Task<IActionResult> SendNotificationToAll([FromBody] NotificationBroadcastRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { error = "Title and Body are required." });

            try
            {
                var result = await _deviceTokenService.SendNotificationToAllAsync(request.Title, request.Body, "TargetActivityName", request.Data);
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gửi thông báo đến tất cả thiết bị của một AccountID.
        /// </summary>

        [HttpPost("send/account/{accountId}")]
        public async Task<IActionResult> SendNotificationToAccount(string accountId, [FromBody] NotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { error = "Title and Body are required." });

            try
            {
                var result = await _deviceTokenService.SendNotificationToAccountAsync(accountId, request.Title, request.Body, "TargetActivityName", request.Data);
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        //    /// <summary>
        //    /// Lưu thông báo theo AccountID.
        //    /// </summary>
        //    [HttpPost("save-notification/{accountId}")]
        //    public async Task<IActionResult> SaveNotification(string accountId, [FromBody] NotificationRequest request)
        //    {
        //        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        //            return BadRequest(new { error = "Title and Body are required." });

        //        try
        //        {
        //            await _deviceTokenService.SaveNotificationAsync(accountId, request.Title, request.Body, "TargetActivityName", request.Data);
        //            return Ok(new { message = "Notification saved successfully." });
        //        }
        //        catch (Exception ex)
        //        {
        //            return StatusCode(500, new { error = ex.Message });
        //        }
        //    }
    }

    /// <summary>
    /// Yêu cầu gửi thông báo đến thiết bị.
    /// </summary>
    public class SendNotificationRequest
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string DeviceToken { get; set; }
        public Dictionary<string, string>? Data { get; set; }
    }

    /// <summary>
    /// Yêu cầu lưu hoặc cập nhật Device Token.
    /// </summary>
    public class SaveDeviceTokenRequest
    {
        public string AccountId { get; set; }
        public string DeviceToken { get; set; }
        public string DeviceInfo { get; set; }
    }

    /// <summary>
    /// Yêu cầu gửi thông báo đến nhiều thiết bị.
    /// </summary>
    public class NotificationBroadcastRequest
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string>? Data { get; set; }
    }

    /// <summary>
    /// Yêu cầu gửi hoặc lưu thông báo.
    /// </summary>
    public class NotificationRequest
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string>? Data { get; set; }
    }
}
