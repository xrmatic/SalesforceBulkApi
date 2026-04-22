# SalesforceBulkApi

A C# .NET 8 library for bulk importing, exporting and managing large datasets (500,000+ records) via the **Salesforce Bulk API v2**, using OAuth2 client credentials (`clientId` / `clientSecret`).

---

## Features

| Capability | Details |
|---|---|
| **Authentication** | OAuth2 `client_credentials` flow with automatic token caching |
| **Create (Insert)** | Upload CSV or JSON → Bulk API v2 ingest job |
| **Read (Query)** | Execute SOQL → returns complete paginated JSON dataset |
| **Update** | Update existing records by `Id` via CSV or JSON |
| **Upsert** | Insert-or-update using an external ID field |
| **Delete / HardDelete** | Soft or hard delete by `Id` |
| **Large datasets** | 500,000+ records per job; automatic polling & status feedback |
| **Error handling** | Typed exceptions, per-record success/failed/unprocessed result CSVs |
| **DI support** | `services.AddSalesforceBulkApi(config)` extension method |
| **Standalone** | `SalesforceBulkClientFactory.Create(config)` — no DI required |

---

## Quick start

### 1. Configure

```csharp
var config = new SalesforceConfig
{
    ClientId     = "YOUR_CONNECTED_APP_CLIENT_ID",
    ClientSecret = "YOUR_CONNECTED_APP_CLIENT_SECRET",
    LoginUrl     = "https://login.salesforce.com",   // or https://test.salesforce.com for sandbox
    ApiVersion   = "v59.0"
};
```

### 2. Create a client

**Without DI:**

```csharp
using var client = SalesforceBulkClientFactory.Create(config);
```

**With Microsoft.Extensions.DependencyInjection:**

```csharp
services.AddSalesforceBulkApi(config);
// Then inject SalesforceBulkClient via constructor
```

### 3. CRUD operations

#### Insert (CSV)

```csharp
string csv = "Name,Industry\nAcme Corp,Technology\nGlobal Inc,Finance\n";
BulkJobResult result = await client.CreateFromCsvAsync("Account", csv);

Console.WriteLine($"Inserted: {result.SuccessCount}, Failed: {result.FailedCount}");
if (!result.IsFullySuccessful)
    Console.WriteLine(result.FailedRecordsCsv);
```

#### Insert (JSON)

```csharp
string json = """[{"Name":"Acme Corp","Industry":"Technology"}]""";
BulkJobResult result = await client.CreateFromJsonAsync("Account", json);
```

#### Read (SOQL query → full JSON dataset)

```csharp
QueryResult result = await client.ReadAsync("SELECT Id, Name, Industry FROM Account");

Console.WriteLine($"Total records: {result.TotalRecords}, Pages: {result.PagesFetched}");
Console.WriteLine(result.RecordsJson);   // complete JSON array
```

#### Update

```csharp
string csv = "Id,Industry\n001xx000000001,Manufacturing\n";
BulkJobResult result = await client.UpdateFromCsvAsync("Account", csv);
```

#### Upsert

```csharp
string csv = "ExternalId__c,Name\nEXT-001,Acme Corp\n";
BulkJobResult result = await client.UpsertFromCsvAsync("Account", csv, "ExternalId__c");
```

#### Delete

```csharp
string csv = "Id\n001xx000000001\n";
BulkJobResult result = await client.DeleteFromCsvAsync("Account", csv);
```

---

## Configuration options

| Property | Default | Description |
|---|---|---|
| `ClientId` | _(required)_ | OAuth2 consumer key |
| `ClientSecret` | _(required)_ | OAuth2 consumer secret |
| `LoginUrl` | `https://login.salesforce.com` | Auth endpoint (use `https://test.salesforce.com` for sandbox) |
| `ApiVersion` | `v59.0` | Salesforce API version |
| `MaxPollAttempts` | 60 | Max status polls before timeout |
| `PollIntervalMs` | 5000 | Milliseconds between status polls |
| `MaxRecordsPerPage` | 50000 | Max records per query results page |
| `HttpTimeoutSeconds` | 120 | HTTP request timeout |

---

## Project structure

```
SalesforceBulkApi/
├── Auth/
│   ├── ISalesforceAuthService.cs   # Auth interface
│   └── SalesforceAuthService.cs    # OAuth2 client_credentials implementation
├── Exceptions/
│   ├── SalesforceException.cs      # Base exception
│   ├── SalesforceAuthException.cs  # Auth failures
│   └── BulkJobException.cs         # Job-level failures with jobId + state
├── Models/
│   ├── SalesforceConfig.cs         # Configuration DTO
│   ├── OperationType.cs            # Insert/Update/Upsert/Delete/HardDelete enum
│   ├── ContentType.cs              # CSV/JSON enum
│   ├── JobState.cs                 # Open/InProgress/JobComplete/Failed/Aborted enum
│   ├── BulkJobInfo.cs              # Ingest job status DTO
│   ├── BulkJobResult.cs            # Full ingest result (counts + result CSVs)
│   ├── QueryJobInfo.cs             # Query job status DTO
│   └── QueryResult.cs              # Full query result (JSON array + metadata)
├── Services/
│   ├── IBulkApiService.cs          # Ingest service interface
│   ├── BulkApiService.cs           # Bulk API v2 ingest implementation
│   ├── IQueryService.cs            # Query service interface
│   └── QueryService.cs             # Bulk API v2 query + pagination implementation
├── SalesforceBulkClient.cs         # High-level CRUD entry point
├── SalesforceBulkClientFactory.cs  # Standalone (no-DI) factory
└── ServiceCollectionExtensions.cs  # DI registration helpers
SalesforceBulkApi.Tests/
├── Auth/SalesforceAuthServiceTests.cs
├── Services/BulkApiServiceTests.cs
├── Services/QueryServiceTests.cs
└── SalesforceConfigTests.cs
```

---

## Running tests

```bash
dotnet test
```

25 unit tests covering authentication, bulk ingest, and query workflows using mock HTTP handlers.
