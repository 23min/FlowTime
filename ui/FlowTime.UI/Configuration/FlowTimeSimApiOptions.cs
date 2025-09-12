namespace FlowTime.UI.Configuration;

public class FlowTimeApiOptions
{
    public const string SectionName = "FlowTimeApi";

    /// <summary>
    /// The base URL for the FlowTime API (default: http://localhost:8080/)
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8080/";

    /// <summary>
    /// The API version to use (default: v1)
    /// </summary>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>
    /// Timeout for API requests in minutes (default: 3)
    /// </summary>
    public int TimeoutMinutes { get; set; } = 3;
}

public class FlowTimeSimApiOptions
{
    public const string SectionName = "FlowTimeSimApi";

    /// <summary>
    /// The base URL for the FlowTime-Sim API (default: http://localhost:8091/)
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8091/";

    /// <summary>
    /// The API version to use (default: v1)
    /// </summary>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>
    /// Timeout for API requests in minutes (default: 5)
    /// </summary>
    public int TimeoutMinutes { get; set; } = 5;
}
