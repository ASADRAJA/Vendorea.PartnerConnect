using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>The result of redacting a payload.</summary>
public sealed record RedactionResult(string Content, IReadOnlyList<string> RedactedPaths);

/// <summary>
/// Strips personal data out of a provider payload before it is stored.
/// </summary>
/// <remarks>
/// **Matched on property name, anywhere in the document, not on path.** That is the deliberate
/// choice. A path-based list — `recipient.deliveryAddress.addressLine1` and so on — reads more
/// precisely and fails silently the moment the provider moves a field, which Amazon has just
/// done: v0's flat `ShippingAddress` became v2026-01-01's nested `recipient.deliveryAddress`. A
/// redactor pinned to the old paths would have gone on reporting that it redacted nothing, which
/// is the worst possible way for this to break.
///
/// Name matching also catches fields that do not exist yet. If Amazon adds `buyer.secondaryEmail`,
/// the substring rule has it already.
///
/// Over-redaction is the safe direction of error here. The archive exists to answer "what shape
/// did this arrive in?" and "what status did it carry?"; it does not need a buyer's street to do
/// that, and the authoritative copy lives on the order row under its own retention.
/// </remarks>
public static class PayloadRedactor
{
    /// <summary>What replaces a redacted value. Distinguishable from null and from "".</summary>
    public const string Marker = "[redacted]";

    /// <summary>
    /// Property names removed outright, case-insensitive.
    /// </summary>
    /// <remarks>
    /// <c>name</c> is here on its own. In the Orders payload the innocuous ones are compounds —
    /// <c>channelName</c>, <c>marketplaceName</c>, <c>companyName</c>, <c>streetName</c> — so a
    /// bare <c>name</c> is the recipient's, and the compounds that do carry personal data are
    /// listed individually.
    /// </remarks>
    private static readonly HashSet<string> RedactedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Buyer identity
        "buyerName", "buyerEmail", "buyerCompanyName", "buyerPurchaseOrderNumber",
        "buyerTaxInformation", "taxingRegion", "taxId", "taxRegistrationId",

        // Recipient identity and street address
        "name", "companyName",
        "addressLine1", "addressLine2", "addressLine3",
        "postalCode", "phone", "phoneNumber",
        "streetName", "streetNumber", "complement", "neighborhood",
        "addressType", "municipality",

        // Gift and buyer-authored text, which routinely contains names
        "giftMessageText", "buyerNote", "deliveryInstructions"
    };

    /// <summary>
    /// Substrings that make any property name personal regardless of the rest of it.
    /// </summary>
    private static readonly string[] RedactedSubstrings = { "email", "phone" };

    /// <summary>
    /// Kept on purpose: coarse geography is not identifying on its own and is the first thing
    /// anyone looks at when a tax, currency or marketplace question comes up.
    /// </summary>
    private static readonly HashSet<string> AlwaysKept = new(StringComparer.OrdinalIgnoreCase)
    {
        "city", "stateOrRegion", "countryCode", "districtOrCounty", "marketplaceId"
    };

    public static RedactionResult Redact(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var paths = new List<string>();
            var buffer = new ArrayBufferWriter<byte>();

            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                WriteRedacted(document.RootElement, writer, path: string.Empty, paths);
            }

            return new RedactionResult(Encoding.UTF8.GetString(buffer.WrittenSpan), paths);
        }
        catch (JsonException)
        {
            // Not JSON. Storing an unparseable body would mean storing something we have not
            // inspected, so nothing is stored — only the fact that there was something.
            return new RedactionResult(
                $"{{\"unparseable\":true,\"length\":{json.Length}}}",
                new[] { "$" });
        }
    }

    private static void WriteRedacted(
        JsonElement element, Utf8JsonWriter writer, string path, List<string> paths)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = path.Length == 0 ? property.Name : $"{path}.{property.Name}";

                    if (ShouldRedact(property.Name))
                    {
                        writer.WriteString(property.Name, Marker);
                        paths.Add(childPath);
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    WriteRedacted(property.Value, writer, childPath, paths);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    WriteRedacted(item, writer, $"{path}[{index++}]", paths);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool ShouldRedact(string propertyName)
    {
        if (AlwaysKept.Contains(propertyName))
        {
            return false;
        }

        if (RedactedNames.Contains(propertyName))
        {
            return true;
        }

        return RedactedSubstrings.Any(s => propertyName.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
}
