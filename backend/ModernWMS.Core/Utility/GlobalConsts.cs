namespace ModernWMS.Core.Utility
{
    /// <summary>
    /// global constant
    /// </summary>
    public static class GlobalConsts
    {

        /// <summary>
        /// is Swagger enable
        /// </summary>
        public static bool IsEnabledSwagger = true;

        /// <summary>
        /// Is RequestResponseMiddleware enable
        /// </summary>
        public static bool IsRequestResponseMiddleware = true;

        /// <summary>
        /// token cipher
        /// </summary>
        public const string SigningKey = "a-very-long-random-32+byte-secret-string-here-123456";

        /// <summary>
        /// Password will expire every 30 days from last password change.
        /// </summary>
        public static int PasswordExpireDays = 30;


    }
}
