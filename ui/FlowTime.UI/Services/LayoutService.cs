using Microsoft.AspNetCore.Components;
using FlowTime.UI.Services.Interface;

namespace FlowTime.UI.Services;

public interface ILayoutService
{
    Type GetLayoutForRoute(string route);
    bool IsLearningRoute(string route);
    bool IsExpertRoute(string route);
}

public class LayoutService : ILayoutService
{
    public Type GetLayoutForRoute(string route)
    {
        if (string.IsNullOrEmpty(route))
            return typeof(Layout.MainLayout);

        // Learning interface routes use LearningLayout
        if (IsLearningRoute(route))
            return typeof(Layout.LearningLayout);
            
        // Expert interface routes use ExpertLayout  
        if (IsExpertRoute(route))
            return typeof(Layout.ExpertLayout);
            
        // Default fallback
        return typeof(Layout.MainLayout);
    }
    
    public bool IsLearningRoute(string route)
    {
        return route.StartsWith("/learn", StringComparison.OrdinalIgnoreCase);
    }
    
    public bool IsExpertRoute(string route)
    {
        return route.StartsWith("/app", StringComparison.OrdinalIgnoreCase) ||
               route.StartsWith("/templates", StringComparison.OrdinalIgnoreCase) ||
               route.StartsWith("/nodes", StringComparison.OrdinalIgnoreCase) ||
               route.StartsWith("/api-demo", StringComparison.OrdinalIgnoreCase) ||
               route.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
    }
}
