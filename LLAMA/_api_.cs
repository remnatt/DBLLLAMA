using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

class DBLLogger
{
    private readonly string logFile = "dblpsLog.json";
    private readonly List<Dictionary<string, object>> logEntries = new();
    private readonly string sessionId = $"session_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    private readonly string startTime = DateTime.UtcNow.ToString("o");

    public void LogEvent(string eventType, string message, object? data = null, string level = "INFO")
    {
        var entry = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["session_id"] = sessionId,
            ["event_type"] = eventType,
            ["level"] = level,
            ["message"] = message,
            ["data"] = data
        };

        logEntries.Add(entry);

        Console.WriteLine($"[{level}] {eventType}: {message}");
        if (data != null)
        {
            Console.WriteLine($"    Data: {JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })}");
        }
    }

    public void SaveLog()
    {
        try
        {
            var logData = new Dictionary<string, object>
            {
                ["session_info"] = new Dictionary<string, object>
                {
                    ["session_id"] = sessionId,
                    ["start_time"] = startTime,
                    ["end_time"] = DateTime.UtcNow.ToString("o"),
                    ["total_events"] = logEntries.Count
                },
                ["events"] = logEntries
            };

            File.WriteAllText(logFile, JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Log saved to {logFile} with {logEntries.Count} events");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save log: {ex.Message}");
        }
    }
}

class Client
{
    private const string ApiHost = "ul2ahv9ohheiyu3t.dblgnds.channel.or.jp";
    private const int ApiPort = 34210;
    private const int ApiTimeout = 16000; // ms

    private TcpClient? tcpClient;
    private SslStream? sslStream;
    private readonly DBLLogger logger = new();

    public async Task ConnectAndStayConnected()
    {
        logger.LogEvent("CLIENT_INIT", "Initializing client", new { ApiHost, ApiPort });

        try
        {
            var ip = Dns.GetHostEntry(ApiHost).AddressList[0];
            logger.LogEvent("DNS_RESOLVED", $"Resolved {ApiHost} to {ip}");

            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(ip, ApiPort);
            logger.LogEvent("CONNECTION_SUCCESS", $"Connected to {ip}:{ApiPort}");

            sslStream = new SslStream(
                tcpClient.GetStream(),
                false,
                (sender, cert, chain, errors) => true // cert -1
            );

            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = ApiHost,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            });

            logger.LogEvent("SSL_HANDSHAKE", "SSL Handshake completed");

            // logging
            logger.LogEvent("LISTENING", "Connected and listening. Press Ctrl+C to stop.");
            while (tcpClient.Connected)
            {
                await Task.Delay(1000); // ^^
            }
        }
        catch (Exception ex)
        {
            logger.LogEvent("ERROR", $"Exception: {ex.Message}", new { ex.StackTrace }, "ERROR");
        }
        finally
        {
            CloseConnection();
            logger.SaveLog();
        }
    }

    private void CloseConnection()
    {
        if (sslStream != null)
        {
            sslStream.Close();
            sslStream.Dispose();
        }

        if (tcpClient != null)
        {
            tcpClient.Close();
        }

        logger.LogEvent("DISCONNECTED", "Connection closed");
    }
}

class Program
{
    public static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Shutdown requested by user.");
            Environment.Exit(0);
        };

        var client = new Client();
        await client.ConnectAndStayConnected();
    }
}
