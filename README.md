# DataDogLogger

# get apikey from MRS data dog website

// With file logging
DatadogDirectLogger.Initialize(apiKey, "MyService", "production", "logs/app.log");

// Without file logging (console + Datadog only)
DatadogDirectLogger.Initialize(apiKey, "MyService", "production");

// Basic logging with console only
DatadogDirectLogger.InitializeBasic();



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
