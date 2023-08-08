namespace RedBoxAuth;

/// <summary>
///     Contains useful constants used in the library
/// </summary>
public struct Constants
{
    /// <summary>
    ///     Name of the header containing the authentication token
    /// </summary>
    public const string TokenHeaderName = "Authorization";

    /// <summary>
    ///     Key of user stored in HttpContext.Items after authorization
    /// </summary>
    public const string UserContextKey = "user";
}