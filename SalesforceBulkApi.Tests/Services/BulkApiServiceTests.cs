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

public class BulkApiServiceTests
{
    private const string JobId = "750xx000000000001";
    private const string InstanceUrl = "https://na1.salesforce.com";
    private const string ApiVersion = "v59.0";

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SalesforceConfig BuildConfig(int maxPollAttempts = 3, int pollIntervalMs = 1) => new()
    {
        ClientId = "cid",
        ClientSecret = "csec",
        LoginUrl = "https://login.salesforce.com",
        ApiVersion = ApiVersion,
        MaxPollAttempts = maxPollAttempts,
        PollIntervalMs = pollIntervalMs
    };

    private static Mock<ISalesforceAuthService> BuildAuth()
    {
        var auth = new Mock<ISalesforceAuthService>();
        auth.Setup(a => a.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        auth.Setup(a => a.GetInstanceUrlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(InstanceUrl);
        return auth;
    }

    private static string SerializeJobInfo(string jobId, JobState state,
        long processed = 10, long failed = 0) =>
        JsonSerializer.Serialize(new
        {
            id = jobId,
            operation = "insert",
            @object = "Account",
            state = state.ToString(),
            numberRecordsProcessed = processed,
            numberRecordsFailed = failed,
            totalProcessingTime = 1000L,
            apiActiveProcessingTime = 500L,
            apexProcessingTime = 0L
        });


    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static HttpResponseMessage CsvResponse(string csv = "") =>
        new(HttpStatusCode.OK) { Content = new StringContent(csv, System.Text.Encoding.UTF8, "text/csv") };

    private static HttpResponseMessage NotFoundResponse() =>
        new(HttpStatusCode.NotFound);

    // -------------------------------------------------------------------------
    // ExecuteCsvJobAsync tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteCsvJobAsync_HappyPath_ReturnsSuccessResult()
    {
        var jobInfoOpen = SerializeJobInfo(JobId, JobState.Open);
        var jobInfoComplete = SerializeJobInfo(JobId, JobState.JobComplete, processed: 5, failed: 0);
        var successCsv = "sf__Id,sf__Created,Name\n001,true,TestAccount\n";

        // Sequential responses: create -> upload -> close -> poll -> successfulResults -> failedResults -> unprocessedrecords
        var responseQueue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(jobInfoOpen),                                                                          // POST create job
            new HttpResponseMessage(HttpStatusCode.Created),                                                   // PUT upload data
            new HttpResponseMessage(HttpStatusCode.OK)                                                         // PATCH close
                { Content = new StringContent(jobInfoOpen, System.Text.Encoding.UTF8, "application/json") },
            JsonResponse(jobInfoComplete),                                                                     // GET poll
            CsvResponse(successCsv),                                                                           // GET successfulResults
            NotFoundResponse(),                                                                                 // GET failedResults
            NotFoundResponse()                                                                                  // GET unprocessedrecords
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

        var svc = new BulkApiService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<BulkApiService>.Instance);

        var result = await svc.ExecuteCsvJobAsync("Account", OperationType.Insert, "Name\nTestAccount\n");

        Assert.Equal(JobState.JobComplete, result.JobInfo.State);
        Assert.Equal(successCsv, result.SuccessfulRecordsCsv);
        Assert.Equal(0, result.FailedCount);
        Assert.True(result.IsFullySuccessful);
    }

    [Fact]
    public async Task ExecuteCsvJobAsync_EmptyData_ThrowsArgumentException()
    {
        var svc = new BulkApiService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(),
            NullLogger<BulkApiService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ExecuteCsvJobAsync("Account", OperationType.Insert, ""));
    }

    [Fact]
    public async Task ExecuteCsvJobAsync_EmptyObject_ThrowsArgumentException()
    {
        var svc = new BulkApiService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(),
            NullLogger<BulkApiService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ExecuteCsvJobAsync("", OperationType.Insert, "Name\nTest\n"));
    }

    [Fact]
    public async Task ExecuteCsvJobAsync_UpsertWithoutExternalId_ThrowsArgumentException()
    {
        var svc = new BulkApiService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(),
            NullLogger<BulkApiService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ExecuteCsvJobAsync("Account", OperationType.Upsert, "ExternalId__c\n123\n"));
    }

    [Fact]
    public async Task ExecuteCsvJobAsync_JobFails_ThrowsBulkJobException()
    {
        var jobInfoOpen = SerializeJobInfo(JobId, JobState.Open);
        var jobInfoFailed = SerializeJobInfo(JobId, JobState.Failed);

        var responseQueue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(jobInfoOpen),                              // create job
            new HttpResponseMessage(HttpStatusCode.Created),       // upload data
            JsonResponse(jobInfoOpen),                             // close (PATCH)
            JsonResponse(jobInfoFailed)                            // poll -> failed
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

        var svc = new BulkApiService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<BulkApiService>.Instance);

        var ex = await Assert.ThrowsAsync<BulkJobException>(() =>
            svc.ExecuteCsvJobAsync("Account", OperationType.Insert, "Name\nTest\n"));

        Assert.Equal(JobId, ex.JobId);
        Assert.Equal(JobState.Failed, ex.JobState);
    }

    [Fact]
    public async Task ExecuteCsvJobAsync_PollTimeout_ThrowsBulkJobException()
    {
        var jobInfoOpen = SerializeJobInfo(JobId, JobState.Open);
        var jobInfoInProgress = SerializeJobInfo(JobId, JobState.InProgress);

        // Return InProgress forever to trigger timeout
        var handler = new Mock<HttpMessageHandler>();
        int callCount = 0;
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() =>
               {
                   callCount++;
                   // First call: create job; second: upload; third: close; rest: poll
                   return callCount <= 3
                       ? JsonResponse(jobInfoOpen)
                       : JsonResponse(jobInfoInProgress);
               });

        var svc = new BulkApiService(
            BuildConfig(maxPollAttempts: 2, pollIntervalMs: 1),
            BuildAuth().Object,
            new HttpClient(handler.Object),
            NullLogger<BulkApiService>.Instance);

        var ex = await Assert.ThrowsAsync<BulkJobException>(() =>
            svc.ExecuteCsvJobAsync("Account", OperationType.Insert, "Name\nTest\n"));

        Assert.Equal(JobState.InProgress, ex.JobState);
    }

    // -------------------------------------------------------------------------
    // ExecuteJsonJobAsync tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteJsonJobAsync_EmptyData_ThrowsArgumentException()
    {
        var svc = new BulkApiService(
            BuildConfig(),
            BuildAuth().Object,
            new HttpClient(),
            NullLogger<BulkApiService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ExecuteJsonJobAsync("Account", OperationType.Insert, ""));
    }
}
