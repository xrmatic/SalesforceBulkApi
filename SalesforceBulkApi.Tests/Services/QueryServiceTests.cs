using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using SalesforceBulkApi.Auth;
using SalesforceBulkApi.Exceptions;
using SalesforceBulkApi.Models;
using SalesforceBulkApi.Services;
using Xunit;

namespace SalesforceBulkApi.Tests.Services;

public class QueryServiceTests
{
    private const string JobId = "750xx000000000002";
    private const string InstanceUrl = "https://na1.salesforce.com";
    private const string ApiVersion = "v59.0";

    private static SalesforceConfig BuildConfig(int maxPollAttempts = 3, int pollIntervalMs = 1) => new()
    {
        ClientId = "cid",
        ClientSecret = "csec",
        LoginUrl = "https://login.salesforce.com",
        ApiVersion = ApiVersion,
        MaxPollAttempts = maxPollAttempts,
        PollIntervalMs = pollIntervalMs,
        MaxRecordsPerPage = 100
    };

    private static Mock<ISalesforceAuthService> BuildAuth()
    {
        var auth = new Mock<ISalesforceAuthService>();
        auth.Setup(a => a.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        auth.Setup(a => a.GetInstanceUrlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(InstanceUrl);
        return auth;
    }

    private static string SerializeQueryJobInfo(string jobId, JobState state, long processed = 0) =>
        JsonSerializer.Serialize(new
        {
            id = jobId,
            operation = "query",
            @object = "Account",
            state = state.ToString(),
            numberRecordsProcessed = processed,
            totalProcessingTime = 500L,
            apiActiveProcessingTime = 200L,
            apexProcessingTime = 0L
        });

    private static string BuildResultsPage(bool done, string? locator, int recordCount = 2)
    {
        var records = Enumerable.Range(1, recordCount).Select(i => new { Id = $"001{i:D15}", Name = $"Acct{i}" });
        return JsonSerializer.Serialize(new
        {
            totalSize = recordCount,
            done,
            records
        });
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK,
        string? sforceLocator = null)
    {
        var msg = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        if (sforceLocator is not null)
            msg.Headers.Add("Sforce-Locator", sforceLocator);
        return msg;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteQueryAsync_SinglePage_ReturnsAllRecords()
    {
        var jobInfoOpen = SerializeQueryJobInfo(JobId, JobState.Open);
        var jobInfoComplete = SerializeQueryJobInfo(JobId, JobState.JobComplete, processed: 2);
        var page1 = BuildResultsPage(done: true, locator: null, recordCount: 2);

        var responseQueue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(jobInfoOpen),      // create job
            JsonResponse(jobInfoComplete),  // poll -> complete
            JsonResponse(page1)             // results page 1
        });

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() => responseQueue.Count > 0
                   ? responseQueue.Dequeue()
                   : new HttpResponseMessage(HttpStatusCode.NotFound));

        var svc = new QueryService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<QueryService>.Instance);

        var result = await svc.ExecuteQueryAsync("SELECT Id, Name FROM Account");

        Assert.Equal(JobState.JobComplete, result.JobInfo.State);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(1, result.PagesFetched);
        Assert.NotNull(result.RecordsJson);
        Assert.Contains("Acct1", result.RecordsJson);
    }

    [Fact]
    public async Task ExecuteQueryAsync_MultiplePages_MergesAllRecords()
    {
        var jobInfoOpen = SerializeQueryJobInfo(JobId, JobState.Open);
        var jobInfoComplete = SerializeQueryJobInfo(JobId, JobState.JobComplete, processed: 4);
        var page1 = BuildResultsPage(done: false, locator: "LOCATOR_ABC", recordCount: 2);
        var page2 = BuildResultsPage(done: true, locator: null, recordCount: 2);

        var responseQueue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(jobInfoOpen),                                  // create job
            JsonResponse(jobInfoComplete),                              // poll
            JsonResponse(page1, sforceLocator: "LOCATOR_ABC"),         // page 1
            JsonResponse(page2)                                         // page 2
        });

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() => responseQueue.Count > 0
                   ? responseQueue.Dequeue()
                   : new HttpResponseMessage(HttpStatusCode.NotFound));

        var svc = new QueryService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<QueryService>.Instance);

        var result = await svc.ExecuteQueryAsync("SELECT Id, Name FROM Account");

        Assert.Equal(JobState.JobComplete, result.JobInfo.State);
        Assert.Equal(4, result.TotalRecords);
        Assert.Equal(2, result.PagesFetched);
    }

    [Fact]
    public async Task ExecuteQueryAsync_EmptyQuery_ThrowsArgumentException()
    {
        var svc = new QueryService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(),
            NullLogger<QueryService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => svc.ExecuteQueryAsync(""));
    }

    [Fact]
    public async Task ExecuteQueryAsync_JobFails_ThrowsBulkJobException()
    {
        var jobInfoOpen = SerializeQueryJobInfo(JobId, JobState.Open);
        var jobInfoFailed = SerializeQueryJobInfo(JobId, JobState.Failed);

        var responseQueue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(jobInfoOpen),    // create job
            JsonResponse(jobInfoFailed)   // poll -> failed
        });

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() => responseQueue.Count > 0
                   ? responseQueue.Dequeue()
                   : new HttpResponseMessage(HttpStatusCode.NotFound));

        var svc = new QueryService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<QueryService>.Instance);

        var ex = await Assert.ThrowsAsync<BulkJobException>(() =>
            svc.ExecuteQueryAsync("SELECT Id FROM Account"));

        Assert.Equal(JobId, ex.JobId);
        Assert.Equal(JobState.Failed, ex.JobState);
    }

    [Fact]
    public async Task ExecuteQueryAsync_NoContent_ReturnsEmptyJson()
    {
        var jobInfoOpen = SerializeQueryJobInfo(JobId, JobState.Open);
        var jobInfoComplete = SerializeQueryJobInfo(JobId, JobState.JobComplete, processed: 0);

        var responseQueue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(jobInfoOpen),     // create job
            JsonResponse(jobInfoComplete), // poll
            new HttpResponseMessage(HttpStatusCode.NoContent)  // no results
        });

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() => responseQueue.Count > 0
                   ? responseQueue.Dequeue()
                   : new HttpResponseMessage(HttpStatusCode.NotFound));

        var svc = new QueryService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<QueryService>.Instance);

        var result = await svc.ExecuteQueryAsync("SELECT Id FROM Account WHERE Id = 'nonexistent'");

        Assert.Equal("[]", result.RecordsJson);
        Assert.Equal(0, result.TotalRecords);
        Assert.Equal(0, result.PagesFetched);
    }

    [Fact]
    public async Task ExecuteQueryAsync_PollTimeout_ThrowsBulkJobException()
    {
        var jobInfoOpen = SerializeQueryJobInfo(JobId, JobState.Open);
        var jobInfoInProgress = SerializeQueryJobInfo(JobId, JobState.InProgress);

        int callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() =>
               {
                   callCount++;
                   return callCount == 1
                       ? JsonResponse(jobInfoOpen)
                       : JsonResponse(jobInfoInProgress);
               });

        var svc = new QueryService(
            BuildConfig(maxPollAttempts: 2, pollIntervalMs: 1),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<QueryService>.Instance);

        var ex = await Assert.ThrowsAsync<BulkJobException>(() =>
            svc.ExecuteQueryAsync("SELECT Id FROM Account"));

        Assert.Equal(JobState.InProgress, ex.JobState);
    }
}
