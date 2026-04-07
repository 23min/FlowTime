using FlowTime.Core.Models;

namespace FlowTime.Core.Metrics;

public static class RuntimeAnalyticalDescriptorExtensions
{
    public static string ToContractCategory(this RuntimeAnalyticalDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Category switch
        {
            RuntimeAnalyticalNodeCategory.Service => "service",
            RuntimeAnalyticalNodeCategory.Queue => "queue",
            RuntimeAnalyticalNodeCategory.Dlq => "dlq",
            RuntimeAnalyticalNodeCategory.Router => "router",
            RuntimeAnalyticalNodeCategory.Dependency => "dependency",
            RuntimeAnalyticalNodeCategory.Sink => "sink",
            RuntimeAnalyticalNodeCategory.Constant => "constant",
            RuntimeAnalyticalNodeCategory.Expression => "expression",
            _ => "service"
        };
    }

    public static string ToContractIdentity(this RuntimeAnalyticalDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Identity switch
        {
            RuntimeAnalyticalIdentity.Service => "service",
            RuntimeAnalyticalIdentity.ServiceWithBuffer => "serviceWithBuffer",
            RuntimeAnalyticalIdentity.Queue => "queue",
            RuntimeAnalyticalIdentity.Dlq => "dlq",
            RuntimeAnalyticalIdentity.Router => "router",
            RuntimeAnalyticalIdentity.Dependency => "dependency",
            RuntimeAnalyticalIdentity.Sink => "sink",
            RuntimeAnalyticalIdentity.Constant => "constant",
            RuntimeAnalyticalIdentity.Pmf => "pmf",
            RuntimeAnalyticalIdentity.Expression => "expression",
            _ => "service"
        };
    }
}