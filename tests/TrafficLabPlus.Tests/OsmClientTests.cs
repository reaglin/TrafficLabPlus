using System.Net;
using TrafficLabPlus.Core.Osm;

namespace TrafficLabPlus.Tests;

/// <summary>How TrafficLab+ talks to OpenStreetMap, against a pretend network.</summary>
public class OsmClientTests
{
    private sealed class FakeNetwork(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            return Task.FromResult(answer(request));
        }
    }

    private static (OsmClient Client, FakeNetwork Net) Client(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var net = new FakeNetwork(answer);
        var http = new HttpClient(net);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(OsmClient.UserAgent);
        return (new OsmClient(http), net);
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private const string Roads = """{"elements":[{"type":"node","id":1,"lat":29,"lon":-81}]}""";

    [Fact]
    public async Task EveryRequestSaysItIsTrafficLabPlus()
    {
        (OsmClient client, FakeNetwork net) = Client(_ => Ok(Roads));

        await client.RoadsAsync(29.22, -81.10, 29.23, -81.09);

        Assert.Contains("TrafficLabPlus", net.Asked.Single().Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TooBigAnAreaIsRefusedBeforeAsking()
    {
        (OsmClient client, FakeNetwork net) = Client(_ => Ok(Roads));

        var ex = await Assert.ThrowsAsync<OsmServiceException>(() => client.RoadsAsync(29.0, -81.2, 29.2, -81.0));

        Assert.Contains("Zoom in", ex.Message, StringComparison.Ordinal);
        Assert.Empty(net.Asked);
    }

    [Fact]
    public async Task ABusyServerIsPassedOverForTheNext()
    {
        (OsmClient client, FakeNetwork net) = Client(r => r.RequestUri!.Host.Contains("overpass-api", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            : Ok(Roads));

        string json = await client.RoadsAsync(29.22, -81.10, 29.23, -81.09);

        Assert.Equal(Roads, json);
        Assert.Equal(2, net.Asked.Count);
    }

    [Fact]
    public async Task WhenEveryServerIsBusyItSaysToWait()
    {
        (OsmClient client, _) = Client(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout));

        var ex = await Assert.ThrowsAsync<OsmServiceException>(() => client.RoadsAsync(29.22, -81.10, 29.23, -81.09));

        Assert.Contains("busy", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnErrorPageInsteadOfRoadsIsNotTakenForRoads()
    {
        (OsmClient client, _) = Client(_ => Ok("<html>runtime error: out of memory</html>"));

        await Assert.ThrowsAsync<OsmServiceException>(() => client.RoadsAsync(29.22, -81.10, 29.23, -81.09));
    }

    [Fact]
    public void PlacesAreReadWithTheirBoxAndTown()
    {
        const string answer = """[{"lat":"29.2251825","lon":"-81.0906298","display_name":"LPGA Boulevard, Daytona Beach, Florida","boundingbox":["29.2248283","29.2255366","-81.0914902","-81.0897695"],"address":{"road":"LPGA Boulevard","city":"Daytona Beach","state":"Florida"}}]""";

        OsmPlace p = Assert.Single(OsmClient.ReadPlaces(answer));

        Assert.Equal("Daytona Beach, Florida", p.Area);
        Assert.Equal([29.2248283, -81.0914902, 29.2255366, -81.0897695], p.Box!);   // south, west, north, east
    }

    [Fact]
    public async Task ASearchAsksNominatimWithTheTextEscaped()
    {
        (OsmClient client, FakeNetwork net) = Client(_ => Ok("[]"));

        List<OsmPlace> found = await client.SearchAsync("LPGA Blvd & Williamson");

        Assert.Empty(found);
        Uri asked = net.Asked.Single().RequestUri!;
        Assert.Equal("nominatim.openstreetmap.org", asked.Host);
        Assert.Contains("q=LPGA%20Blvd%20%26%20Williamson", asked.Query, StringComparison.Ordinal);
    }
}
