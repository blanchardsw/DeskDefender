namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Enumeration of login event types
    /// </summary>
    public enum LoginEventType
    {
        /// <summary>
        /// Unknown or unrecognized login event
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Successful login attempt
        /// </summary>
        Success = 1,

        /// <summary>
        /// Failed login attempt
        /// </summary>
        Failure = 2,

        /// <summary>
        /// User logoff event
        /// </summary>
        Logoff = 3,

        /// <summary>
        /// User-initiated logoff event
        /// </summary>
        UserLogoff = 4
    }
}
