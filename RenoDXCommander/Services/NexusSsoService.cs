using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace RenoDXCommander.Services;

/// <summary>
/// Implements the Nexus Mods WebSocket SSO flow.
/// https://github.com/Nexus-Mods/node-nexus-api/blob/master/docs/README.md#single-sign-on
///
/// Flow:
///   1. Generate a random UUID (the "id")
///   2. Connect to wss://sso.nexusmods.com
///   3. Send { "id": "...", "appid": "rhi" }
///   4. Open browser to https://www.nexusmods.com/sso?id={id}&application=rhi
///   5. Send WebSocket ping every 30 seconds until the user authorises
///   6. Receive the API key as a plain-text WebSocket message
///   7. Return the key; caller validates and saves it
/// </summary>
public class NexusSsoService
{
    private const string SsoUrl    = "wss://sso.nexusmods.com";
    private const string BrowserUrl = "https://www.nexusmods.com/sso?id={0}&application=rhi";
    private const string AppId     = "rhi";

    /// <summary>
    /// Fires when the browser URL is ready for the user to open.
    /// Caller should display or open this URL.
    /// </summary>
    public event Action<string>? BrowserUrlReady;

    /// <summary>
    /// Starts the SSO flow and waits for the user to authorise in the browser.
    /// Returns the API key on success, null on timeout or error.
    /// </summary>
    public async Task<string?> AuthoriseAsync(
        CancellationToken cancellationToken = default,
        int timeoutSeconds = 120)
    {
        var sessionId = Guid.NewGuid().ToString();
        var ssoUri    = new Uri(SsoUrl);
        var authUrl   = string.Format(BrowserUrl, sessionId);

        using var ws  = new ClientWebSocket();
        ws.Options.SetRequestHeader("User-Agent", "RHI");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            CrashReporter.Log($"[NexusSsoService] Connecting to {SsoUrl}");
            await ws.ConnectAsync(ssoUri, cts.Token).ConfigureAwait(false);

            // Step 3: send { "id": "...", "appid": "rhi" }
            var handshake = JsonSerializer.Serialize(new { id = sessionId, appid = AppId });
            var handshakeBytes = Encoding.UTF8.GetBytes(handshake);
            await ws.SendAsync(
                new ArraySegment<byte>(handshakeBytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cts.Token).ConfigureAwait(false);
            CrashReporter.Log($"[NexusSsoService] Sent handshake, session={sessionId}");

            // Step 4: fire event so the caller can open the browser
            BrowserUrlReady?.Invoke(authUrl);

            // Start ping timer (every 30 seconds as required by the spec)
            using var pingTimer = new System.Timers.Timer(30_000);
            pingTimer.Elapsed += async (_, _) =>
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                        await ws.SendAsync(
                            new ArraySegment<byte>(Array.Empty<byte>()),
                            WebSocketMessageType.Binary,
                            endOfMessage: true,
                            CancellationToken.None).ConfigureAwait(false);
                }
                catch { /* ignore — connection may have closed */ }
            };
            pingTimer.Start();

            // Step 6: read response (API key arrives as a plain text message)
            var buffer = new byte[4096];
            while (!cts.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    CrashReporter.Log("[NexusSsoService] Server closed connection");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    CrashReporter.Log("[NexusSsoService] Received SSO response");

                    // The spec says the response is the plain key, but Nexus wraps it in JSON
                    // Try JSON first, fall back to treating the entire body as the key
                    string? apiKey = null;
                    try
                    {
                        var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("data", out var dataElem))
                            apiKey = dataElem.GetString();
                        else if (doc.RootElement.ValueKind == JsonValueKind.String)
                            apiKey = doc.RootElement.GetString();
                    }
                    catch
                    {
                        // Not JSON — treat as raw key
                        apiKey = json.Trim();
                    }

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        CrashReporter.Log("[NexusSsoService] API key received successfully");
                        return apiKey;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            CrashReporter.Log("[NexusSsoService] Authorisation timed out or was cancelled");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[NexusSsoService] Failed — {ex.Message}");
        }

        return null;
    }
}
