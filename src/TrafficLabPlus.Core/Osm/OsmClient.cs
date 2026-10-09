using System.Globalization;
using System.Net;
using System.Text.Json;

namespace TrafficLabPlus.Core.Osm;

/// <summary>A place found by name.</summary>
public sealed record OsmPlace(string Name, double Lat, double Lon, double[]? Box, string? Area);

/// <summary>Something went wrong talking to OpenStreetMap, said in words a person can act on.</summary>
public sealed class OsmServiceException : Exception
{
    public OsmServiceException() { }

    public OsmServiceException(string message) : base(message) { }

    public OsmServiceException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Talks to OpenStreetMap's free services the way their usage policies ask: an identified
/// User-Agent, one search at a time and no more than one a second (Nominatim), and one query per
/// box of roads (Overpass), kept in the study so an opened study never asks again.
/// </summary>
public sealed class OsmClient(HttpClient http)
{
    public const string UserAgent = "TrafficLabPlus/0.1 (+https://softwareplus.ai/trafficlab/)";

    public static readonly string[] OverpassServers = ["https://overpass-api.de/api/interpreter", "https://overpass.kumi.systems/api/interpreter"];

    private static readonly SemaphoreSlim OneSearch = new(1, 1);
    private static DateTime _lastSearch = DateTime.MinValue;

    public static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return http;
    }

    /// <summary>Places matching what was typed (a street and town, a town, an address).</summary>
    public async Task<List<OsmPlace>> SearchAsync(string text, CancellationToken cancel = default)
    {
        await OneSearch.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            TimeSpan wait = _lastSearch.AddSeconds(1.1) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancel).ConfigureAwait(false);
            }

            _lastSearch = DateTime.UtcNow;
            string url = "https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&limit=8&q=" + Uri.EscapeDataString(text.Trim());
            string json = await Get(url, "search for the place", cancel).ConfigureAwait(false);
            return ReadPlaces(json);
        }
        finally
        {
            OneSearch.Release();
        }
    }

    public static List<OsmPlace> ReadPlaces(string json)
    {
        var places = new List<OsmPlace>();
        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (JsonElement e in doc.RootElement.EnumerateArray())
        {
            double lat = double.Parse(e.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
            double lon = double.Parse(e.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
            double[]? box = e.TryGetProperty("boundingbox", out JsonElement b)
                ? b.EnumerateArray().Select(x => double.Parse(x.GetString()!, CultureInfo.InvariantCulture)).ToArray()
                : null;
            string? area = null;
            if (e.TryGetProperty("address", out JsonElement a))
            {
                string? town = new[] { "city", "town", "village", "hamlet", "county" }.Select(k => a.TryGetProperty(k, out JsonElement v) ? v.GetString() : null).FirstOrDefault(v => v is not null);
                string? state = a.TryGetProperty("state", out JsonElement st) ? st.GetString() : null;
                area = string.Join(", ", new[] { town, state }.Where(x => !string.IsNullOrEmpty(x)));
            }

            // Nominatim's box is south, north, west, east; TrafficLab+ keeps south, west, north, east
            places.Add(new OsmPlace(e.GetProperty("display_name").GetString() ?? "", lat, lon,
                box is { Length: 4 } ? [box[0], box[2], box[1], box[3]] : null, string.IsNullOrEmpty(area) ? null : area));
        }

        return places;
    }

    /// <summary>Every road in the box, as Overpass answers. Too big a box is refused before asking.</summary>
    public async Task<string> RoadsAsync(double south, double west, double north, double east, CancellationToken cancel = default)
    {
        double tall = Geo.Metres(south, west, north, west) / 1000, wide = Geo.Metres(south, west, south, east) / 1000;
        if (tall > OsmQuery.MaxSideKm || wide > OsmQuery.MaxSideKm)
        {
            throw new OsmServiceException($"That area is {Math.Max(tall, wide):0.#} km across. Zoom in until it is less than {OsmQuery.MaxSideKm} km — about the length of a ten-signal corridor — so the request stays light on a free service.");
        }

        string query = OsmQuery.Roads(south, west, north, east);
        OsmServiceException? last = null;
        foreach (string server in OverpassServers)
        {
            try
            {
                using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
                using HttpResponseMessage response = await http.PostAsync(server, content, cancel).ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.GatewayTimeout or HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway)
                {
                    last = new OsmServiceException("OpenStreetMap's road service is busy right now. Wait a minute and press Load the roads again.");
                    continue;   // the next server
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
                _ = OsmData.Parse(json);   // a busy server can answer 200 with an error page
                return json;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Build.StudyFormatException && !cancel.IsCancellationRequested)
            {
                last = new OsmServiceException("TrafficLab+ could not get the roads from OpenStreetMap. Check the internet connection and press Load the roads again. (Details: " + ex.Message + ")", ex);
            }
        }

        throw last ?? new OsmServiceException("TrafficLab+ could not get the roads from OpenStreetMap.");
    }

    private async Task<string> Get(string url, string what, CancellationToken cancel)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, cancel).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
            {
                throw new OsmServiceException("OpenStreetMap asked TrafficLab+ to slow down. Wait a minute, then " + what + " again.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancel.IsCancellationRequested)
        {
            throw new OsmServiceException("TrafficLab+ could not reach OpenStreetMap to " + what + ". Check the internet connection and try again. (Details: " + ex.Message + ")", ex);
        }
    }
}
