public partial class Secrets
{
    // Public fork builds have no crash-reporting endpoint. An invalid placeholder
    // makes Sentry throw during application startup; an empty DSN disables it.
    internal static readonly string Dsn = "";
    internal static readonly string OfficialPublicKey = "mock-key";
}
