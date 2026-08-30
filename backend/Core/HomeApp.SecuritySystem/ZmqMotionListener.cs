using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using System.IO;
using System.Linq;

namespace HomeApp.SecuritySystem;

public class ZmqMotionListener : BackgroundService
{
    private readonly NotificationService _notificationService;
    private readonly SecuritySystemOptions _options;
    public static byte[]? LatestFrame { get; private set; } // Store latest frame

    public ZmqMotionListener(NotificationService notificationService, SecuritySystemOptions options)
    {
        _notificationService = notificationService;
        _options = options;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            using var subSocket = new SubscriberSocket();
            subSocket.Connect(_options.CameraPublisherEndpoint);
            subSocket.Subscribe(""); // Subscribe to all messages

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var msg = subSocket.ReceiveFrameBytes();
                    // Try to decode as string
                    try
                    {
                        var msgStr = System.Text.Encoding.UTF8.GetString(msg);
                        if (msgStr == "motion_detected")
                        {
                            // Find most recent video file
                            // Use absolute path matching docker-compose mount
                            var dir = _options.RecordedVideoOutputFolder;
                            if (Directory.Exists(dir))
                            {
                                var latestFile = Directory.GetFiles(dir, "*.avi")
                                    .Select(f => new FileInfo(f))
                                    .OrderByDescending(f => f.LastWriteTime)
                                    .FirstOrDefault();
                                if (latestFile != null)
                                {
                                    _notificationService.NotifyUserWithVideoAsync(
                                        "@everyone Something was seen (video attached)", 
                                        latestFile.FullName
                                    ).Wait();
                                }
                                else
                                {
                                    _notificationService.NotifyUserAsync("@everyone Something was seen (no video found)").Wait();
                                }
                            }
                            else
                            {
                                _notificationService.NotifyUserAsync("@everyone Something was seen (no video directory)").Wait();
                            }
                            continue;
                        }
                    }
                    catch
                    {
                        // Not a string, treat as image
                    }
                    // Assume it's a base64-encoded JPEG
                    try
                    {
                        var jpgBytes = Convert.FromBase64String(System.Text.Encoding.UTF8.GetString(msg));
                        LatestFrame = jpgBytes;
                    }
                    catch
                    {
                        // Ignore invalid frames
                    }
                }
                catch
                {
                    // Handle/log errors as needed
                }
            }
        }, stoppingToken);
    }
}
