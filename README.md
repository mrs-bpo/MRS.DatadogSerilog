# DataDogSeriLogger

This project inherits these versions of serilog and newtonsoft.  If this becomes a problem in the future, they can ben removed and whatever library is using them can add them, themselves.  I can see Newtonsoft being a problem.  That is only there if you want to send serialized json.  This should probably removed but this is a very early version of this object and is a TODO

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


Here is a helper method to keep it to one line if you know your Json structure

        private void LogJson(string subject, string body, Action<string, object[]> logAction)
        {
            var json = new { Subject = subject, Body = body };
            logAction(JsonConvert.SerializeObject(json), new object[] { json });
        }

usage


     LogJson("Dialer", ex.Message, DataDogSeriLogger.LogError);