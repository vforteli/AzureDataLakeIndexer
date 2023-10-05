using Azure.Identity;
using Azure.Search.Documents;
using Azure.Storage.Files.DataLake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchIndexerTest;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        var azureSearchServiceUri = Environment.GetEnvironmentVariable("AzureSearchServiceUri");
        ArgumentNullException.ThrowIfNull(azureSearchServiceUri);

        var datalakeConnectionString = Environment.GetEnvironmentVariable("DatalakeConnectionString");
        ArgumentNullException.ThrowIfNull(datalakeConnectionString);

        services.AddSingleton(new DataLakeServiceClient(new Uri(datalakeConnectionString), new DefaultAzureCredential()));
        services.AddSingleton(o => new PathIndexClient(new SearchClient(new Uri(azureSearchServiceUri), "path-created-index", new DefaultAzureCredential()), o.GetRequiredService<ILogger<PathIndexClient>>()));
        services.AddSingleton(o => new DataLakeIndexer(new SearchClient(new Uri(azureSearchServiceUri), "someindex-large", new DefaultAzureCredential()), o.GetRequiredService<ILogger<DataLakeIndexer>>()));
    })
    .Build();

host.Run();
