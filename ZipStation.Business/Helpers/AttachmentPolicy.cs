namespace ZipStation.Business.Helpers;

/// <summary>
/// Single source of truth for what may be attached to a ticket message or a kanban story,
/// and how large it may be. Both controllers validate through here so the two surfaces
/// can't drift apart the way they did when story attachments were video-only.
/// </summary>
/// <remarks>
/// This is a UX filter, not a security boundary — <c>application/octet-stream</c> is allowed
/// because browsers report plenty of ordinary files (.log, .env, .ts) that way, so anything
/// can get through under that content type. The actual protections are that blobs are served
/// from the storage provider's origin (never ours) and that risky types are forced to download
/// rather than render. See <see cref="RequiresForcedDownload"/>.
/// </remarks>
public static class AttachmentPolicy
{
    /// <summary>Cap for video, which is legitimately large.</summary>
    public const long MaxVideoSize = 100 * 1024 * 1024; // 100 MB

    /// <summary>Cap for everything else.</summary>
    public const long MaxFileSize = 20 * 1024 * 1024; // 20 MB

    /// <summary>
    /// Largest body any attachment endpoint accepts. Drives the <c>[RequestSizeLimit]</c>
    /// attributes, which must be compile-time constants and so can't branch on content type —
    /// the per-type cap is enforced in the handler via <see cref="MaxSizeFor"/>.
    /// </summary>
    public const long MaxRequestSize = MaxVideoSize;

    /// <summary>Cap for images embedded in rich-text bodies, which are inlined on every read.</summary>
    public const long MaxInlineImageSize = 5 * 1024 * 1024; // 5 MB

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp", "image/svg+xml",
        // Only formats browsers can actually play back in a <video> tag.
        "video/mp4", "video/quicktime", "video/webm", "video/x-m4v",
        "audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4",
        "text/plain", "text/csv", "text/markdown", "text/xml", "application/json", "application/xml",
        "application/pdf", "application/zip", "application/x-zip-compressed",
        "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        // Browsers report unknown-but-common files (.log, .env, ...) as octet-stream.
        "application/octet-stream",
    };

    /// <summary>
    /// Types allowed for rich-text inline embedding. Deliberately narrower than
    /// <see cref="AllowedContentTypes"/>: these render as &lt;img&gt; inside sanitized HTML,
    /// and SVG is excluded because it can carry script.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedInlineImageContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp",
    };

    public static bool IsAllowed(string? contentType) =>
        contentType != null && AllowedContentTypes.Contains(contentType);

    public static bool IsAllowedInlineImage(string? contentType) =>
        contentType != null && AllowedInlineImageContentTypes.Contains(contentType);

    public static bool IsVideo(string? contentType) =>
        contentType != null && contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for types a browser would execute rather than merely display if it navigated to the
    /// blob directly. SVG is the one on the allow-list: it is XML that can embed &lt;script&gt;,
    /// so a raw link to one is a stored-XSS vector against whatever origin serves it.
    /// Callers pass this to the storage layer to pin <c>Content-Disposition: attachment</c>.
    /// </summary>
    /// <remarks>
    /// Forcing the disposition does not break inline rendering — browsers honour
    /// Content-Disposition on top-level navigation but ignore it for subresource loads,
    /// so an SVG still paints normally inside an &lt;img&gt; tag.
    /// </remarks>
    public static bool RequiresForcedDownload(string? contentType) =>
        string.Equals(contentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>Size cap for a given content type.</summary>
    public static long MaxSizeFor(string? contentType) =>
        IsVideo(contentType) ? MaxVideoSize : MaxFileSize;

    /// <summary>Rejection message for a file that exceeds <see cref="MaxSizeFor"/>.</summary>
    public static string SizeLimitMessage(string? contentType) =>
        $"File exceeds {MaxSizeFor(contentType) / (1024 * 1024)}MB limit";

    /// <summary>Rejection message for a disallowed content type.</summary>
    public const string UnsupportedTypeMessage = "Unsupported file type";
}
