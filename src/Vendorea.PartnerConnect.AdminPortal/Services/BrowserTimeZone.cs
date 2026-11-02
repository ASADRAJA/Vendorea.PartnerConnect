using Microsoft.JSInterop;

namespace Vendorea.PartnerConnect.AdminPortal.Services;

/// <summary>
/// The viewer's own time zone, resolved once per circuit from the browser, plus the formatting
/// helpers the pages use to render a UTC instant in it.
/// </summary>
/// <remarks>
/// This exists because <c>DateTime.ToLocalTime()</c> is the wrong tool in Blazor Server: it runs on
/// the server and converts to the <em>server's</em> zone. The droplet runs UTC, so every timestamp
/// in the portal was rendered in UTC no matter where the operator was sitting.
///
/// Every DateTime the API returns is UTC — all 209 columns are <c>timestamp with time zone</c>, so
/// Npgsql hands back Kind=Utc and System.Text.Json preserves it through the <c>Z</c> suffix. The
/// conversion therefore only needs the viewer's zone, which only the browser knows.
///
/// Falls back to UTC — the previous behaviour — if the browser reports nothing usable or the id
/// doesn't resolve on this host, so a formatting helper can never be the thing that breaks a page.
/// </remarks>
public class BrowserTimeZone
{
    /// <summary>Placeholder used wherever a timestamp is absent, matching the pages' existing dash.</summary>
    public const string EmptyPlaceholder = "—";

    private readonly ILogger<BrowserTimeZone> _logger;

    private TimeZoneInfo _zone = TimeZoneInfo.Utc;

    public BrowserTimeZone(ILogger<BrowserTimeZone> logger) => _logger = logger;

    /// <summary>True once initialization has run (whether or not the browser gave us a usable id).</summary>
    public bool Resolved { get; private set; }

    /// <summary>The resolved zone's id — "UTC" when the browser's zone could not be determined.</summary>
    public string ZoneId => _zone.Id;

    /// <summary>
    /// Asks the browser for its IANA zone id and resolves it. Safe to call repeatedly; only the
    /// first call does any work.
    ///
    /// This portal renders ServerPrerendered, so unlike the customer portal the shell cannot call
    /// this during initialization — there is no JS during the prerender pass. It is called from
    /// OnAfterRenderAsync(firstRender) instead, which only ever runs once the circuit is live. The
    /// cost is that the prerendered paint shows UTC for an instant before the re-render corrects it.
    /// </summary>
    public async Task InitializeAsync(IJSRuntime js)
    {
        if (Resolved)
            return;

        // Every path below that leaves the zone as UTC says so. Falling back silently would be
        // indistinguishable, on screen, from this feature never having been built.
        try
        {
            var id = await js.InvokeAsync<string?>("partnerConnectTimeZone");

            if (string.IsNullOrWhiteSpace(id))
                _logger.LogWarning("Browser reported no time zone; timestamps will render in UTC.");
            else if (TryFindZone(id, out var zone))
                _zone = zone;
            else
                _logger.LogWarning(
                    "Browser time zone '{ZoneId}' is not known to this host; timestamps will render in UTC.", id);
        }
        catch (Exception ex)
        {
            // A failed interop call must not take the shell down with it; UTC is a correct, if
            // unhelpful, answer.
            _logger.LogWarning(ex, "Could not read the browser time zone; timestamps will render in UTC.");
        }
        finally
        {
            Resolved = true;
        }
    }

    /// <summary>Converts a UTC instant into the viewer's zone.</summary>
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), _zone);

    /// <summary>Converts a nullable UTC instant into the viewer's zone.</summary>
    public DateTime? ToLocal(DateTime? utc) => utc is { } v ? ToLocal(v) : null;

    /// <summary>
    /// Formats a UTC instant in the viewer's zone, or returns <paramref name="placeholder"/> when
    /// there is no value.
    /// </summary>
    public string Format(DateTime? utc, string format = "g", string placeholder = EmptyPlaceholder) =>
        utc is { } v ? ToLocal(v).ToString(format) : placeholder;

    /// <summary>Formats a non-nullable UTC instant in the viewer's zone.</summary>
    public string Format(DateTime utc, string format = "g") => ToLocal(utc).ToString(format);

    /// <summary>"Now" as the viewer sees it — for date headings, not for arithmetic (use UTC for that).</summary>
    public DateTime Now => ToLocal(DateTime.UtcNow);

    private static bool TryFindZone(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    /// <summary>
    /// Normalizes an incoming value to a UTC-kinded instant. Values arrive Kind=Utc; Unspecified is
    /// treated as UTC because that is what the API stores and sends. ConvertTimeFromUtc throws on a
    /// Local-kinded input, so that case is converted rather than assumed away.
    /// </summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
