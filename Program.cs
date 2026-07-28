using AzDoPrMonitor;

var org = Environment.GetEnvironmentVariable("AZDO_ORG") ?? "rr-wfm";
var pat = Environment.GetEnvironmentVariable("AZDO_PAT");

if (string.IsNullOrWhiteSpace(pat))
{
    Console.Error.WriteLine("Missing AZDO_PAT environment variable.");
    Console.Error.WriteLine("Create a Personal Access Token (Code: Read scope is enough) at:");
    Console.Error.WriteLine($"  https://dev.azure.com/{org}/_usersSettings/tokens");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Then run:");
    Console.Error.WriteLine("  export AZDO_PAT=\"<your-pat>\"");
    return 1;
}

using var client = new AzureDevOpsClient(org, pat);

string myId;
try
{
    Console.WriteLine($"Connecting to {org}...");
    myId = await client.GetMyIdAsync(CancellationToken.None);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to connect to Azure DevOps org '{org}': {ex.Message}");
    return 1;
}

var app = new PrMonitorApp(client, org, myId);
app.Run();
return 0;
