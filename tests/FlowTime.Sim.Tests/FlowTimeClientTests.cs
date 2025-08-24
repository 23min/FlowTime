using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FlowTime.Sim.Cli;
using Xunit;

namespace FlowTime.Sim.Tests;

public class FlowTimeClientTests
{
    [Fact]
    public async Task RunAsync_Throws_On_400_With_Error_Message()
    {
        var handler = new StubHandler((req) =>
        {
            var content = new StringContent("""{ "error": "bad model" }""", Encoding.UTF8, "application/json");
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content };
        });
        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FlowTimeClient.RunAsync(http, "http://localhost:8080", "bad: yaml", CancellationToken.None));

        Assert.Contains("bad model", ex.Message);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
