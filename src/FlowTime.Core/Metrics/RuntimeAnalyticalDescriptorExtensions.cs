using FlowTime.Core.Models;

namespace FlowTime.Core.Metrics;

public static class RuntimeAnalyticalDescriptorExtensions
{
    public static string ToLogicalType(this RuntimeAnalyticalDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Identity switch
        {
            RuntimeAnalyticalIdentity.Service => "service",
            RuntimeAnalyticalIdentity.ServiceWithBuffer => "servicewithbuffer",
            RuntimeAnalyticalIdentity.Queue => "queue",
            RuntimeAnalyticalIdentity.Dlq => "dlq",
            RuntimeAnalyticalIdentity.Router => "router",
            RuntimeAnalyticalIdentity.Dependency => "dependency",
            RuntimeAnalyticalIdentity.Sink => "sink",
            RuntimeAnalyticalIdentity.Constant => "const",
            RuntimeAnalyticalIdentity.Pmf => "pmf",
            RuntimeAnalyticalIdentity.Expression => "expression",
            _ => "service"
        };
    }
}