using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Newtonsoft.Json;

/// <summary>
/// Simple C# class for Datadog Direct API logging with Serilog
/// Compatible with .NET Standard 2.0
/// </summary>
public class DataDogSeriLogger
{
    private static bool _isInitialized = false;
    private static ILogger _logger;

    /// <summary>
    /// Initialize Serilog with Datadog Direct API
    /// </summary>
    /// <param name="apiKey">Your Datadog API key</param>
    /// <param name="serviceName">Service name</param>
    /// <param name="environment">Environment</param>
    /// <param name="logFilePath">Optional file path for backup logs</param>
    /// <returns>True if successful</returns>
    public static bool Initialize(string apiKey, string serviceName , string environment , string logFilePath = null)
    {
        try
        {
            if (_isInitialized)
            {
                return true;
            }

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console();

            // Add file logging if path is provided
            if (!string.IsNullOrEmpty(logFilePath))
            {
                loggerConfig = loggerConfig.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day);
            }

            // Add Datadog Direct API sink
            if (!string.IsNullOrEmpty(apiKey))
            {
                loggerConfig = loggerConfig.WriteTo.Sink(new DatadogDirectSink(apiKey, serviceName, environment));
            }

            _logger = loggerConfig.CreateLogger();
            Log.Logger = _logger;

            _isInitialized = true;

            LogInformation("Datadog Direct API logging initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing Datadog logger: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Initialize basic logging without Datadog
    /// </summary>
    /// <param name="logFilePath">Optional file path for logs</param>
    public static bool InitializeBasic(string logFilePath = null)
    {
        try
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console();

            // Add file logging if path is provided
            if (!string.IsNullOrEmpty(logFilePath))
            {
                loggerConfig = loggerConfig.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day);
            }

            _logger = loggerConfig.CreateLogger();

            Log.Logger = _logger;
            _isInitialized = true;

            LogInformation("Basic logging initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing basic logger: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Log information message
    /// </summary>
    public static void LogInformation(string message, params object[] args)
    {
        if (_isInitialized)
        {
            if (args != null && args.Length > 0)
            {
                Log.Information(message, args);
            }
            else
            {
                Log.Information(message);
            }
        }
        else
        {
            Console.WriteLine($"INFO: {message}");
        }
    }

    /// <summary>
    /// Log warning message
    /// </summary>
    public static void LogWarning(string message, params object[] args)
    {
        if (_isInitialized)
        {
            if (args != null && args.Length > 0)
            {
                Log.Warning(message, args);
            }
            else
            {
                Log.Warning(message);
            }
        }
        else
        {
            Console.WriteLine($"WARN: {message}");
        }
    }

    /// <summary>
    /// Log error message
    /// </summary>
    public static void LogError(string message, params object[] args)
    {
        if (_isInitialized)
        {
            if (args != null && args.Length > 0)
            {
                Log.Error(message, args);
            }
            else
            {
                Log.Error(message);
            }
        }
        else
        {
            Console.WriteLine($"ERROR: {message}");
        }
    }

    /// <summary>
    /// Log error with exception
    /// </summary>
    public static void LogError(Exception ex, string message, params object[] args)
    {
        if (_isInitialized)
        {
            if (args != null && args.Length > 0)
            {
                Log.Error(ex, message, args);
            }
            else
            {
                Log.Error(ex, message);
            }
        }
        else
        {
            Console.WriteLine($"ERROR: {message} - {ex.Message}");
        }
    }

    /// <summary>
    /// Log debug message
    /// </summary>
    public static void LogDebug(string message, params object[] args)
    {
        if (_isInitialized)
        {
            if (args != null && args.Length > 0)
            {
                Log.Debug(message, args);
            }
            else
            {
                Log.Debug(message);
            }
        }
        else
        {
            Console.WriteLine($"DEBUG: {message}");
        }
    }

    /// <summary>
    /// Close and flush logs
    /// </summary>
    public static void CloseAndFlush()
    {
        try
        {
            if (_isInitialized)
            {
                LogInformation("Closing and flushing logs");
                Log.CloseAndFlush();
                _isInitialized = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing logger: {ex.Message}");
        }
    }
    }

/// <summary>
/// Simple Serilog sink for Datadog Direct API
/// Compatible with .NET Standard 2.0
/// </summary>
public class DatadogDirectSink : ILogEventSink, IDisposable
{
    private readonly string _apiKey;
    private readonly string _serviceName;
    private readonly string _environment;
    private readonly HttpClient _httpClient;
    private readonly string _datadogUrl = "https://http-intake.logs.datadoghq.com/v1/input/{0}";
    private readonly System.Collections.Concurrent.ConcurrentBag<Task> _pendingTasks;
    private readonly object _disposeLock = new object();
    private bool _isDisposed = false;

    public DatadogDirectSink(string apiKey, string serviceName, string environment)
    {
        _apiKey = apiKey;
        _serviceName = serviceName;
        _environment = environment;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.Add("DD-API-KEY", apiKey);
        _pendingTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();
    }

    public void Emit(LogEvent logEvent)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var task = Task.Run(() => SendToDatadog(logEvent));
            _pendingTasks.Add(task);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DatadogDirectSink: {ex.Message}");
        }
    }

    private async Task SendToDatadog(LogEvent logEvent)
    {
        try
        {
            if (_isDisposed)
            {
                return;
            }

            var logEntry = new
            {
                timestamp = logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                level = GetDatadogLevel(logEvent.Level),
                message = logEvent.RenderMessage(),
                service = _serviceName,
                env = _environment,
                hostname = Environment.MachineName,
                source = "csharp"
            };

            var json = JsonConvert.SerializeObject(logEntry);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = string.Format(_datadogUrl, _apiKey);

            var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine($"Failed to send log to Datadog: {response.StatusCode} - {responseBody}");
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Timeout sending log to Datadog");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Network error sending log to Datadog: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending log to Datadog: {ex.Message}");
        }
    }

    private string GetDatadogLevel(LogEventLevel level)
    {
        switch (level)
        {
            case LogEventLevel.Debug:
            case LogEventLevel.Verbose:
                return "debug";
            case LogEventLevel.Information:
                return "info";
            case LogEventLevel.Warning:
                return "warn";
            case LogEventLevel.Error:
                return "error";
            case LogEventLevel.Fatal:
                return "fatal";
            default:
                return "info";
        }
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            try
            {
                // Wait for all pending tasks to complete with a timeout
                var allTasks = _pendingTasks.Where(t => t != null && !t.IsCompleted).ToArray();
                
                if (allTasks.Length > 0)
                {
                    Console.WriteLine($"Waiting for {allTasks.Length} pending log(s) to be sent to Datadog...");
                    
                    // Wait up to 30 seconds for all tasks to complete
                    var completed = Task.WaitAll(allTasks, TimeSpan.FromSeconds(30));
                    
                    if (!completed)
                    {
                        Console.WriteLine($"Warning: Some logs may not have been sent to Datadog (timeout)");
                    }
                    else
                    {
                        Console.WriteLine("All pending logs sent to Datadog successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while flushing logs to Datadog: {ex.Message}");
            }
            finally
            {
                _httpClient?.Dispose();
            }
        }
    }
}
