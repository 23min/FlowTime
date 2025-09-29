namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Exception thrown when template parsing fails.
/// </summary>
public class TemplateParsingException : Exception
{
    public TemplateParsingException(string message) : base(message) { }
    public TemplateParsingException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when template validation fails.
/// </summary>
public class TemplateValidationException : Exception
{
    public TemplateValidationException(string message) : base(message) { }
    public TemplateValidationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when node compilation fails.
/// </summary>
public class NodeCompilationException : Exception
{
    public NodeCompilationException(string message) : base(message) { }
    public NodeCompilationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when node validation fails.
/// </summary>
public class NodeValidationException : Exception
{
    public NodeValidationException(string message) : base(message) { }
    public NodeValidationException(string message, Exception innerException) : base(message, innerException) { }
}