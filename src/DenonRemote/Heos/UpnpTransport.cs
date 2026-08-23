// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DenonRemote.Heos;

/// <summary>
/// UPnP AVTransport client for seek functionality.
///
/// The HEOS CLI (port 1255) does NOT support seek. The official HEOS app uses
/// the UPnP AVTransport service exposed on port 60006 to jump to a position.
/// This class wraps that single SOAP action.
/// </summary>
public sealed class UpnpTransport
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly string _host;
    private const int Port = 60006;
    private const string ControlPath = "/upnp/control/renderer_dvc/AVTransport";

    public UpnpTransport(string host) => _host = host;

    /// <summary>
    /// Seeks to an absolute position in the currently playing track.
    /// </summary>
    /// <param name="position">Target position as TimeSpan.</param>
    /// <returns>True if the receiver accepted the seek.</returns>
    public async Task<bool> SeekAsync(TimeSpan position, CancellationToken ct = default)
    {
        var target = $"{(int)position.TotalHours}:{position.Minutes:00}:{position.Seconds:00}";
        var soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
  <s:Body>
    <u:Seek xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
      <InstanceID>0</InstanceID>
      <Unit>REL_TIME</Unit>
      <Target>{target}</Target>
    </u:Seek>
  </s:Body>
</s:Envelope>";

        try
        {
            var url = $"http://{_host}:{Port}{ControlPath}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(soap, Encoding.UTF8, "text/xml");
            request.Headers.TryAddWithoutValidation("SOAPAction",
                "\"urn:schemas-upnp-org:service:AVTransport:1#Seek\"");

            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Convenience overload that takes milliseconds (the HEOS progress unit).</summary>
    public Task<bool> SeekAsync(long milliseconds, CancellationToken ct = default) =>
        SeekAsync(TimeSpan.FromMilliseconds(milliseconds), ct);
}
