namespace SCP_575.Shared
{
    /// <summary>
    /// Provides global identity and network routing string transformation extensions 
    /// to enforce architectural casing consistency across all subsystems.
    /// </summary>
    public static class IdentityExtensions
    {
        /// <summary>
        /// Enforces standard lowercase invariant formatting on a raw network identifier token,
        /// mitigating platform-specific auth casing anomalies and dictionary key mismatches.
        /// </summary>
        public static string NormalizeUserId(this string userId)
        {
            return userId?.ToLowerInvariant() ?? string.Empty;
        }
    }
}