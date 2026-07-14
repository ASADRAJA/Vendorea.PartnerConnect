using System.Net;
using System.Text;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <summary>
/// Builds consistent, branded HTML + plain-text bodies for PartnerConnect's transactional emails
/// (activation invites, password resets, denial notices, …). Dependency-free string building — a
/// common header/footer and an optional call-to-action button — so every transactional email looks
/// the same. Callers pass plain text (it is HTML-encoded here); the returned <see cref="Rendered"/>
/// carries both the multipart HTML and text alternatives expected by <c>IEmailSender.SendAsync</c>.
/// </summary>
public static class EmailTemplates
{
    private const string Accent = "#0f6cbd";

    /// <summary>An email body in both HTML and plain-text form.</summary>
    public sealed record Rendered(string Html, string Text);

    /// <summary>
    /// Renders a branded email. <paramref name="greetingName"/> and each of <paramref name="paragraphs"/>
    /// are treated as plain text and HTML-encoded. When both <paramref name="buttonText"/> and
    /// <paramref name="buttonUrl"/> are supplied, a call-to-action button (plus a copy-paste link
    /// fallback) is rendered. <paramref name="footerNote"/> is an optional smaller closing line
    /// (e.g. link expiry / "if you didn't expect this…").
    /// </summary>
    public static Rendered Build(
        string greetingName,
        IReadOnlyList<string> paragraphs,
        string? buttonText = null,
        string? buttonUrl = null,
        string? footerNote = null)
    {
        var hasButton = !string.IsNullOrWhiteSpace(buttonText) && !string.IsNullOrWhiteSpace(buttonUrl);

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:'Segoe UI',system-ui,-apple-system,sans-serif;background:#faf9f8;padding:24px;color:#242424;\">");
        html.Append("<div style=\"max-width:480px;margin:0 auto;background:#ffffff;border:1px solid #e1dfdd;border-radius:8px;overflow:hidden;\">");
        html.Append($"<div style=\"padding:20px 28px;border-bottom:1px solid #e1dfdd;font-size:20px;font-weight:600;\">Partner<span style=\"color:{Accent};\">Connect</span></div>");
        html.Append("<div style=\"padding:24px 28px;font-size:14px;line-height:1.5;\">");
        html.Append($"<p style=\"margin:0 0 16px;\">Hello {WebUtility.HtmlEncode(greetingName)},</p>");
        foreach (var paragraph in paragraphs)
            html.Append($"<p style=\"margin:0 0 16px;\">{WebUtility.HtmlEncode(paragraph)}</p>");

        if (hasButton)
        {
            var href = WebUtility.HtmlEncode(buttonUrl);
            html.Append($"<p style=\"margin:24px 0;\"><a href=\"{href}\" style=\"display:inline-block;background:{Accent};color:#ffffff;text-decoration:none;padding:10px 22px;border-radius:4px;font-weight:600;\">{WebUtility.HtmlEncode(buttonText)}</a></p>");
            html.Append($"<p style=\"margin:0 0 16px;font-size:12px;color:#616161;word-break:break-all;\">Or paste this link into your browser:<br/>{href}</p>");
        }

        if (!string.IsNullOrWhiteSpace(footerNote))
            html.Append($"<p style=\"margin:16px 0 0;font-size:12px;color:#616161;\">{WebUtility.HtmlEncode(footerNote)}</p>");

        html.Append("</div>");
        html.Append("<div style=\"padding:16px 28px;border-top:1px solid #e1dfdd;font-size:11px;color:#a19f9d;\">This is an automated message from PartnerConnect. Please do not reply to this email.</div>");
        html.Append("</div></div>");

        var text = new StringBuilder();
        text.Append($"Hello {greetingName},\n\n");
        foreach (var paragraph in paragraphs)
            text.Append(paragraph).Append("\n\n");
        if (hasButton)
            text.Append(buttonText).Append(":\n").Append(buttonUrl).Append("\n\n");
        if (!string.IsNullOrWhiteSpace(footerNote))
            text.Append(footerNote).Append("\n\n");
        text.Append("— PartnerConnect");

        return new Rendered(html.ToString(), text.ToString());
    }
}
