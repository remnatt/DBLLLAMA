using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PersistentApiClient
{
    public class LogEntry
    {
        public string Timestamp { get; set; }
        public string SessionId { get; set; }
        public string EventType { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class SessionInfo
    {
        public string SessionId { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public int TotalEvents { get; set; }
    }

    public class LogData
    {
        public SessionInfo SessionInfo { get; set; }
        public List<LogEntry> Events { get; set; }
    }

    public class DBLLogger
    {
        private readonly string _logFile;
        private readonly List<LogEntry> _logEntries;
        private readonly string _sessionId;
        private readonly string _startTime;
        private readonly object _lockObject = new object();

        public DBLLogger(string logFile = "dblpsLog.json")
        {
            _logFile = logFile;
            _logEntries = new List<LogEntry>();
            _sessionId = $"session_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            _startTime = DateTime.Now.ToString("O");
        }

        public void LogEvent(string eventType, string message, object data = null, string level = "INFO")
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("O"),
                SessionId = _sessionId,
                EventType = eventType,
                Level = level,
                Message = message,
                Data = data
            };

            lock (_lockObject)
            {
                _logEntries.Add(entry);
            }

            // Log to console
            Console.WriteLine($"[{level}] {eventType}: {message}");
            if (data != null)
            {
                Console.WriteLine($"    Data: {JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })}");
            }
        }

        public void LogConnection(string host, int port, bool success = true, string error = null)
        {
            var connectionData = new
            {
                host = host,
                port = port,
                success = success,
                error = error
            };

            if (success)
            {
                LogEvent("CONNECTION_SUCCESS", $"Successfully connected to {host}:{port}", connectionData);
            }
            else
            {
                LogEvent("CONNECTION_FAILED", $"Failed to connect to {host}:{port}", connectionData, "ERROR");
            }
        }

        public void LogNetworkActivity(string activityType, object details)
        {
            LogEvent("NETWORK_ACTIVITY", activityType, details);
        }

        public void SaveLog()
        {
            try
            {
                var logData = new LogData
                {
                    SessionInfo = new SessionInfo
                    {
                        SessionId = _sessionId,
                        StartTime = _startTime,
                        EndTime = DateTime.Now.ToString("O"),
                        TotalEvents = _logEntries.Count
                    },
                    Events = _logEntries.ToList()
                };

                var json = JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_logFile, json);

                Console.WriteLine($"Log saved to {_logFile} with {_logEntries.Count} events");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to save log: {e.Message}");
            }
        }
    }

    public class PersistentClient
    {
        private const string ApiHost = "ul2ahv9ohheiyu3t.dblgnds.channel.or.jp";
        private const int ApiPort = 34210;
        private const int ApiTimeout = 16000;
        private const int ApiVersion = 275;

        private readonly DBLLogger _logger;
        private TcpClient _tcpClient;
        private SslStream _sslStream;
        private NetworkStream _networkStream;
        private int _sequenceNumber = 1;
        private bool _isConnected = false;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _listenTask;
        private readonly Random _random = new Random();

        public bool IsConnected => _isConnected;
        public event Action<string> OnDisconnected;
        public event Action<object> OnDataReceived;

        public PersistentClient()
        {
            _logger = new DBLLogger();
            _cancellationTokenSource = new CancellationTokenSource();
            
            _logger.LogEvent("CLIENT_INIT", "DBL Client initialized", new
            {
                apihost = ApiHost,
                apiport = ApiPort,
                apiversion = ApiVersion
            });
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.LogEvent("CONNECT_START", $"Starting connection to {ApiHost}:{ApiPort}");

                _logger.LogEvent("DNS_LOOKUP", $"Resolving hostname: {ApiHost}");
                var addresses = await Dns.GetHostAddressesAsync(ApiHost);
                var ip = addresses.FirstOrDefault()?.ToString();
                _logger.LogEvent("DNS_RESOLVED", $"Hostname resolved to: {ip}");

                var proxyInfo = GetHttpProxy();
                
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, ApiPort);
                _networkStream = _tcpClient.GetStream();

                _sslStream = new SslStream(_networkStream, false, ValidateServerCertificate);
                await _sslStream.AuthenticateAsClientAsync(ApiHost);

                _isConnected = true;
                _logger.LogConnection(ApiHost, ApiPort, true);
              
                _listenTask = Task.Run(async () => await ListenForDataAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception e)
            {
                _logger.LogConnection(ApiHost, ApiPort, false, e.Message);
                _isConnected = false;
                return false;
            }
        }

        private (string host, int port) GetHttpProxy()
        {
            try
            {
                var proxy = Environment.GetEnvironmentVariable("HTTP_PROXY");
                if (string.IsNullOrEmpty(proxy))
                    return (null, 0);

                if (proxy.StartsWith("http://"))
                    proxy = proxy.Substring(7);

                var parts = proxy.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int port))
                {
                    _logger.LogEvent("PROXY_CHECK", "Checked for HTTP proxy", new
                    {
                        proxy_found = true,
                        proxy_host = parts[0],
                        proxy_port = port
                    });
                    return (parts[0], port);
                }
            }
            catch (Exception e)
            {
                _logger.LogEvent("PROXY_ERROR", $"Error checking proxy: {e.Message}", null, "ERROR");
            }

            _logger.LogEvent("PROXY_CHECK", "Checked for HTTP proxy", new
            {
                proxy_found = false,
                proxy_host = (string)null,
                proxy_port = (int?)null
            });
            return (null, 0);
        }

        private async Task ListenForDataAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isConnected)
                {
                    var bytesRead = await _sslStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        _logger.LogEvent("CONNECTION_CLOSED", "Server closed the connection");
                        break;
                    }

                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    _logger.LogNetworkActivity("PACKET_RECEIVED", new
                    {
                        packet_size = bytesRead,
                        timestamp = DateTime.Now.ToString("O")
                    });

                    OnDataReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogEvent("LISTEN_CANCELLED", "Listening task was cancelled");
            }
            catch (Exception e)
            {
                _logger.LogEvent("LISTEN_ERROR", $"Error in listening task: {e.Message}", new { error = e.Message }, "ERROR");
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (!_isConnected || _sslStream == null)
            {
                _logger.LogEvent("SEND_ERROR", "Cannot send data - not connected", null, "ERROR");
                return;
            }

            try
            {
                await _sslStream.WriteAsync(data, 0, data.Length);
                await _sslStream.FlushAsync();

                _logger.LogNetworkActivity("PACKET_SENT", new
                {
                    sequence_number = _sequenceNumber++,
                    packet_size = data.Length,
                    timestamp = DateTime.Now.ToString("O")
                });
            }
            catch (Exception e)
            {
                _logger.LogEvent("SEND_ERROR", $"Failed to send data: {e.Message}", new { error = e.Message }, "ERROR");
            }
        }

        public async Task SendTextAsync(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);
            await SendDataAsync(data);
        }

        public async Task DisconnectAsync()
        {
            if (!_isConnected)
                return;

            _isConnected = false;
            _cancellationTokenSource?.Cancel();

            try
            {
                if (_listenTask != null)
                    await _listenTask;

                _sslStream?.Close();
                _networkStream?.Close();
                _tcpClient?.Close();

                _logger.LogEvent("DISCONNECT", "Disconnected from server");
                OnDisconnected?.Invoke("Manual disconnect");
            }
            catch (Exception e)
            {
                _logger.LogEvent("DISCONNECT_ERROR", $"Error during disconnect: {e.Message}", new { error = e.Message }, "ERROR");
            }
            finally
            {
                _logger.SaveLog();
            }
        }

        public async Task WaitAsync(string reason, double seconds)
        {
            _logger.LogEvent("WAIT_START", $"Waiting for {seconds:F2} second(s) to {reason}");
            await Task.Delay(TimeSpan.FromSeconds(seconds), _cancellationTokenSource.Token);
            _logger.LogEvent("WAIT_END", $"Finished waiting for {reason}");
        }

        public async Task WaitRandomAsync(string reason, double minSeconds, double maxSeconds)
        {
            var waitTime = _random.NextDouble() * (maxSeconds - minSeconds) + minSeconds;
            _logger.LogEvent("WAIT_RANDOM", $"Random wait: {waitTime:F2}s for {reason}");
            await WaitAsync(reason, waitTime);
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _cancellationTokenSource?.Dispose();
        }
    }

    public class ApiController
    {
        private readonly PersistentClient _client;
        private readonly ILogger<ApiController> _logger;

        public ApiController(PersistentClient client, ILogger<ApiController> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<IResult> GetStatus()
        {
            return Results.Ok(new
            {
                connected = _client.IsConnected,
                timestamp = DateTime.Now.ToString("O"),
                status = _client.IsConnected ? "Connected" : "Disconnected"
            });
        }

        public async Task<IResult> Connect()
        {
            if (_client.IsConnected)
                return Results.Ok(new { message = "Already connected", connected = true });

            var success = await _client.ConnectAsync();
            return Results.Ok(new
            {
                message = success ? "Connection successful" : "Connection failed",
                connected = success
            });
        }

        public async Task<IResult> Disconnect()
        {
            if (!_client.IsConnected)
                return Results.Ok(new { message = "Already disconnected", connected = false });

            await _client.DisconnectAsync();
            return Results.Ok(new { message = "Disconnected successfully", connected = false });
        }

        public async Task<IResult> SendData(HttpRequest request)
        {
            if (!_client.IsConnected)
                return Results.BadRequest(new { error = "Not connected to server" });

            try
            {
                using var reader = new StreamReader(request.Body);
                var data = await reader.ReadToEndAsync();
                
                await _client.SendTextAsync(data);
                return Results.Ok(new { message = "Data sent successfully", data = data });
            }
            catch (Exception e)
            {
                return Results.BadRequest(new { error = e.Message });
            }
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            builder.Services.AddSingleton<PersistentClient>();
            builder.Services.AddSingleton<ApiController>();
            builder.Services.AddLogging();

            var app = builder.Build();

            var client = app.Services.GetRequiredService<PersistentClient>();
            var controller = app.Services.GetRequiredService<ApiController>();

            client.OnDisconnected += (reason) =>
            {
                Console.WriteLine($"Client disconnected: {reason}");
            };

            client.OnDataReceived += (data) =>
            {
                Console.WriteLine($"Data received: {((byte[])data).Length} bytes");
            };

            app.MapGet("/status", controller.GetStatus);
            app.MapPost("/connect", controller.Connect);
            app.MapPost("/disconnect", controller.Disconnect);
            app.MapPost("/send", controller.SendData);

            app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Persistent API Client</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .container { max-width: 800px; margin: 0 auto; }
        .status { padding: 10px; margin: 10px 0; border-radius: 4px; }
        .connected { background-color: #d4edda; color: #155724; }
        .disconnected { background-color: #f8d7da; color: #721c24; }
        button { padding: 10px 20px; margin: 5px; cursor: pointer; }
        textarea { width: 100%; height: 100px; margin: 10px 0; }
        .log { background-color: #f8f9fa; padding: 10px; margin: 10px 0; border-radius: 4px; max-height: 300px; overflow-y: auto; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Persistent API Client</h1>
        <div id='status' class='status'>Checking status...</div>
        <button onclick='connect()'>Connect</button>
        <button onclick='disconnect()'>Disconnect</button>
        <button onclick='checkStatus()'>Check Status</button>
        <h3>Send Data</h3>
        <textarea id='dataInput' placeholder='Enter data to send...'></textarea>
        <button onclick='sendData()'>Send Data</button>
        <div id='log' class='log'></div>
    </div>

    <script>
        function log(message) {
            const logDiv = document.getElementById('log');
            logDiv.innerHTML += new Date().toLocaleTimeString() + ': ' + message + '<br>';
            logDiv.scrollTop = logDiv.scrollHeight;
        }

        async function checkStatus() {
            try {
                const response = await fetch('/status');
                const data = await response.json();
                const statusDiv = document.getElementById('status');
                statusDiv.textContent = `Status: ${data.status} (${data.timestamp})`;
                statusDiv.className = `status ${data.connected ? 'connected' : 'disconnected'}`;
                log(`Status: ${data.status}`);
            } catch (error) {
                log(`Error checking status: ${error.message}`);
            }
        }

        async function connect() {
            try {
                const response = await fetch('/connect', { method: 'POST' });
                const data = await response.json();
                log(data.message);
                checkStatus();
            } catch (error) {
                log(`Error connecting: ${error.message}`);
            }
        }

        async function disconnect() {
            try {
                const response = await fetch('/disconnect', { method: 'POST' });
                const data = await response.json();
                log(data.message);
                checkStatus();
            } catch (error) {
                log(`Error disconnecting: ${error.message}`);
            }
        }

        async function sendData() {
            try {
                const data = document.getElementById('dataInput').value;
                if (!data.trim()) {
                    log('No data to send');
                    return;
                }
                
                const response = await fetch('/send', {
                    method: 'POST',
                    headers: { 'Content-Type': 'text/plain' },
                    body: data
                });
                
                const result = await response.json();
                if (response.ok) {
                    log(`Data sent successfully: ${result.data}`);
                    document.getElementById('dataInput').value = '';
                } else {
                    log(`Error sending data: ${result.error}`);
                }
            } catch (error) {
                log(`Error sending data: ${error.message}`);
            }
        }

        // Check status on page load
        checkStatus();
        
        // Auto-refresh status every 10 seconds
        setInterval(checkStatus, 10000);
    </script>
</body>
</html>", "text/html"));

            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() =>
            {
                Console.WriteLine("Application is shutting down...");
                client.DisconnectAsync().Wait();
            });

            Console.WriteLine("Starting DBLPS server...");
            Console.WriteLine("Web interface available at; http://localhost:5000");
            Console.WriteLine("DBLPS endpoints:");
            Console.WriteLine("  GET  /status    - Check connection status");
            Console.WriteLine("  POST /connect   - Connect to server");
            Console.WriteLine("  POST /disconnect - Disconnect from server");
            Console.WriteLine("  POST /send      - Send data to server");
            Console.WriteLine();
            Console.WriteLine("press crtl c to exit");

            await app.RunAsync();
        }
    }
}
