using System;
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
public class DatadogDirectLogger
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

    public DatadogDirectSink(string apiKey, string serviceName, string environment)
    {
        _apiKey = apiKey;
        _serviceName = serviceName;
        _environment = environment;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("DD-API-KEY", apiKey);
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            Task.Run(() => SendToDatadog(logEvent));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DatadogDirectSink: {ex.Message}");
        }
    }

    private void SendToDatadog(LogEvent logEvent)
    {
        try
        {
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

            var response = _httpClient.PostAsync(url, content).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to send log to Datadog: {response.StatusCode}");
            }
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
        _httpClient?.Dispose();
    }
}
