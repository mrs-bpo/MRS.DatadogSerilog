# DataDogSeriLogger

This is going to assume you have at least these versions of these in your project

<PackageReference Include="Serilog" Version="2.12.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="4.1.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />

# get apikey from MRS data dog website

// With file logging
DataDogSeriLogger.Initialize(apiKey, "MyService", "production", "logs/app.log");

// Without file logging (console + Datadog only)
DataDogSeriLogger.Initialize(apiKey, "MyService", "production");

// Basic logging with console only
DataDogSeriLogger.InitializeBasic();



//Json dynamic logging


    var json = new
    {
        Subject = subject,
        Body = body,
        TheUser = WindowsIdentity.GetCurrent().Name,
        Account = account,
        Master = master,
        Lu = lu,
        Phone = phone,
        SourceId = sourceId,
        CallTransactionId = transaction_id,
        CallSessionId = session_id,
        Error = "true"
    };

DatadogDirectLogger.LogError(JsonConvert.SerializeObject(json));
