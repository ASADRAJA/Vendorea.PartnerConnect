using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Transport.Interfaces;

namespace Vendorea.PartnerConnect.Transport.AS2;

/// <summary>
/// Configuration for AS2 transport.
/// </summary>
public class AS2Configuration
{
    /// <summary>
    /// The AS2 identifier for this party.
    /// </summary>
    public string AS2From { get; set; } = string.Empty;

    /// <summary>
    /// The AS2 identifier for the partner.
    /// </summary>
    public string AS2To { get; set; } = string.Empty;

    /// <summary>
    /// The partner's AS2 endpoint URL.
    /// </summary>
    public string PartnerUrl { get; set; } = string.Empty;

    /// <summary>
    /// The URL where MDN (receipts) should be sent.
    /// </summary>
    public string? MdnUrl { get; set; }

    /// <summary>
    /// Whether to request a signed MDN.
    /// </summary>
    public bool RequestSignedMdn { get; set; } = true;

    /// <summary>
    /// Whether to sign outgoing messages.
    /// </summary>
    public bool SignMessages { get; set; } = true;

    /// <summary>
    /// Whether to encrypt outgoing messages.
    /// </summary>
    public bool EncryptMessages { get; set; } = true;

    /// <summary>
    /// Whether to compress outgoing messages.
    /// </summary>
    public bool CompressMessages { get; set; } = false;

    /// <summary>
    /// Signing algorithm (e.g., "sha256").
    /// </summary>
    public string SigningAlgorithm { get; set; } = "sha256";

    /// <summary>
    /// Encryption algorithm (e.g., "aes128-cbc", "3des").
    /// </summary>
    public string EncryptionAlgorithm { get; set; } = "aes128-cbc";

    /// <summary>
    /// Path to the signing certificate (PFX).
    /// </summary>
    public string? SigningCertificatePath { get; set; }

    /// <summary>
    /// Password for the signing certificate.
    /// </summary>
    public string? SigningCertificatePassword { get; set; }

    /// <summary>
    /// Path to the partner's public certificate for encryption.
    /// </summary>
    public string? PartnerCertificatePath { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// AS2 message receipt (MDN).
/// </summary>
public class AS2MessageReceipt
{
    public string MessageId { get; set; } = string.Empty;
    public string OriginalMessageId { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public string? DispositionType { get; set; }
    public string? DispositionModifier { get; set; }
    public string? ReceivedContentMic { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// AS2 transport client for sending and receiving EDI documents via AS2 protocol.
/// </summary>
public class AS2TransportClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AS2Configuration _config;
    private readonly ILogger<AS2TransportClient> _logger;
    private X509Certificate2? _signingCertificate;
    private X509Certificate2? _partnerCertificate;

    public AS2TransportClient(
        IHttpClientFactory httpClientFactory,
        AS2Configuration configuration,
        ILogger<AS2TransportClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _config = configuration;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

        LoadCertificates();
    }

    /// <summary>
    /// Sends a document to the AS2 partner.
    /// </summary>
    public async Task<AS2SendResult> SendAsync(
        byte[] content,
        string contentType,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var messageId = $"<{Guid.NewGuid()}@{_config.AS2From}>";
        var result = new AS2SendResult { MessageId = messageId };

        try
        {
            _logger.LogInformation(
                "Sending AS2 message {MessageId} to {Partner}",
                messageId,
                _config.AS2To);

            var processedContent = content;
            var currentContentType = contentType;

            // Sign the message if configured
            if (_config.SignMessages && _signingCertificate != null)
            {
                var signedData = SignContent(processedContent, currentContentType);
                processedContent = signedData.Data;
                currentContentType = signedData.ContentType;
            }

            // Encrypt the message if configured
            if (_config.EncryptMessages && _partnerCertificate != null)
            {
                var encryptedData = EncryptContent(processedContent);
                processedContent = encryptedData.Data;
                currentContentType = encryptedData.ContentType;
            }

            // Build the request
            using var request = new HttpRequestMessage(HttpMethod.Post, _config.PartnerUrl);
            request.Content = new ByteArrayContent(processedContent);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(currentContentType);

            // Add AS2 headers
            request.Headers.Add("AS2-From", _config.AS2From);
            request.Headers.Add("AS2-To", _config.AS2To);
            request.Headers.Add("Message-ID", messageId);
            request.Headers.Add("Subject", fileName ?? "EDI Document");
            request.Headers.Add("Date", DateTime.UtcNow.ToString("R"));
            request.Headers.Add("MIME-Version", "1.0");

            // MDN options
            if (_config.RequestSignedMdn)
            {
                var mdnOptions = $"signed-receipt-protocol=required, pkcs7-signature; signed-receipt-micalg=required, {_config.SigningAlgorithm}";
                request.Headers.Add("Disposition-Notification-Options", mdnOptions);

                if (!string.IsNullOrEmpty(_config.MdnUrl))
                {
                    request.Headers.Add("Receipt-Delivery-Option", _config.MdnUrl);
                }
            }
            else
            {
                request.Headers.Add("Disposition-Notification-To", _config.MdnUrl ?? _config.PartnerUrl);
            }

            // Send the request
            var response = await _httpClient.SendAsync(request, cancellationToken);

            result.HttpStatusCode = (int)response.StatusCode;
            result.IsSuccessful = response.IsSuccessStatusCode;

            if (response.IsSuccessStatusCode)
            {
                // Process synchronous MDN if present
                var mdnContent = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (mdnContent.Length > 0)
                {
                    result.Receipt = ParseMdn(mdnContent, response.Content.Headers.ContentType?.MediaType);
                }

                _logger.LogInformation(
                    "AS2 message {MessageId} sent successfully",
                    messageId);
            }
            else
            {
                result.ErrorMessage = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "AS2 message {MessageId} failed: {StatusCode} - {Error}",
                    messageId,
                    response.StatusCode,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending AS2 message {MessageId}", messageId);
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Processes an incoming AS2 message and returns an MDN.
    /// </summary>
    public AS2ReceiveResult ReceiveAndProcess(
        byte[] content,
        string contentType,
        IDictionary<string, string> headers)
    {
        var result = new AS2ReceiveResult();

        try
        {
            var messageId = headers.TryGetValue("Message-ID", out var mid) ? mid : Guid.NewGuid().ToString();
            var as2From = headers.TryGetValue("AS2-From", out var from) ? from : "unknown";
            var as2To = headers.TryGetValue("AS2-To", out var to) ? to : "unknown";

            result.MessageId = messageId;
            result.AS2From = as2From;
            result.AS2To = as2To;

            _logger.LogInformation(
                "Received AS2 message {MessageId} from {From}",
                messageId,
                as2From);

            // Decrypt if necessary
            var processedContent = content;
            if (contentType.Contains("pkcs7-mime") || contentType.Contains("enveloped-data"))
            {
                processedContent = DecryptContent(content);
            }

            // Verify signature if present
            if (contentType.Contains("pkcs7-signature") || contentType.Contains("signed-data"))
            {
                var verifyResult = VerifySignature(processedContent);
                result.SignatureVerified = verifyResult.IsValid;
                processedContent = verifyResult.Content;
            }

            result.Content = processedContent;
            result.IsSuccessful = true;

            // Generate MDN
            result.MdnContent = GenerateMdn(messageId, true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing incoming AS2 message");
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
            result.MdnContent = GenerateMdn(
                result.MessageId ?? "unknown",
                false,
                ex.Message);
        }

        return result;
    }

    private void LoadCertificates()
    {
        if (!string.IsNullOrEmpty(_config.SigningCertificatePath) && File.Exists(_config.SigningCertificatePath))
        {
            _signingCertificate = new X509Certificate2(
                _config.SigningCertificatePath,
                _config.SigningCertificatePassword,
                X509KeyStorageFlags.Exportable);
        }

        if (!string.IsNullOrEmpty(_config.PartnerCertificatePath) && File.Exists(_config.PartnerCertificatePath))
        {
            _partnerCertificate = new X509Certificate2(_config.PartnerCertificatePath);
        }
    }

    private (byte[] Data, string ContentType) SignContent(byte[] content, string contentType)
    {
        var contentInfo = new ContentInfo(content);
        var signedCms = new SignedCms(contentInfo, true);
        var signer = new CmsSigner(_signingCertificate);

        signedCms.ComputeSignature(signer);
        var signedData = signedCms.Encode();

        return (signedData, "application/pkcs7-mime; smime-type=signed-data");
    }

    private (byte[] Data, string ContentType) EncryptContent(byte[] content)
    {
        var contentInfo = new ContentInfo(content);
        var envelopedCms = new EnvelopedCms(contentInfo);

        var recipient = new CmsRecipient(_partnerCertificate);
        envelopedCms.Encrypt(recipient);

        return (envelopedCms.Encode(), "application/pkcs7-mime; smime-type=enveloped-data");
    }

    private byte[] DecryptContent(byte[] encryptedContent)
    {
        var envelopedCms = new EnvelopedCms();
        envelopedCms.Decode(encryptedContent);

        if (_signingCertificate != null)
        {
            envelopedCms.Decrypt(new X509Certificate2Collection(_signingCertificate));
        }
        else
        {
            envelopedCms.Decrypt();
        }

        return envelopedCms.ContentInfo.Content;
    }

    private (bool IsValid, byte[] Content) VerifySignature(byte[] signedContent)
    {
        var signedCms = new SignedCms();
        signedCms.Decode(signedContent);

        try
        {
            signedCms.CheckSignature(true);
            return (true, signedCms.ContentInfo.Content);
        }
        catch
        {
            return (false, signedCms.ContentInfo.Content);
        }
    }

    private AS2MessageReceipt? ParseMdn(byte[] mdnContent, string? contentType)
    {
        // Basic MDN parsing - would need full implementation for production
        var content = Encoding.UTF8.GetString(mdnContent);

        return new AS2MessageReceipt
        {
            IsSuccessful = content.Contains("processed") || content.Contains("automatic-action"),
            ReceivedAt = DateTime.UtcNow
        };
    }

    private byte[] GenerateMdn(string originalMessageId, bool success, string? errorMessage)
    {
        var disposition = success
            ? "automatic-action/MDN-sent-automatically; processed"
            : $"automatic-action/MDN-sent-automatically; failed/error: {errorMessage}";

        var mdn = $"""
            Content-Type: multipart/report; report-type=disposition-notification; boundary="----=_Part_MDN"

            ------=_Part_MDN
            Content-Type: text/plain

            The message sent to Partner AS2 has been received.
            Original-Message-ID: {originalMessageId}

            ------=_Part_MDN
            Content-Type: message/disposition-notification

            Original-Recipient: rfc822; {_config.AS2To}
            Final-Recipient: rfc822; {_config.AS2To}
            Original-Message-ID: {originalMessageId}
            Disposition: {disposition}

            ------=_Part_MDN--
            """;

        return Encoding.UTF8.GetBytes(mdn);
    }

    public ValueTask DisposeAsync()
    {
        _signingCertificate?.Dispose();
        _partnerCertificate?.Dispose();
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Result of sending an AS2 message.
/// </summary>
public class AS2SendResult
{
    public string MessageId { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public int HttpStatusCode { get; set; }
    public AS2MessageReceipt? Receipt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of receiving an AS2 message.
/// </summary>
public class AS2ReceiveResult
{
    public string? MessageId { get; set; }
    public string? AS2From { get; set; }
    public string? AS2To { get; set; }
    public bool IsSuccessful { get; set; }
    public bool SignatureVerified { get; set; }
    public byte[]? Content { get; set; }
    public byte[]? MdnContent { get; set; }
    public string? ErrorMessage { get; set; }
}
