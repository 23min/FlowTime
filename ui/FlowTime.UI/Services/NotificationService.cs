using MudBlazor;
using FlowTime.UI.Layout;
using Microsoft.AspNetCore.Components;

namespace FlowTime.UI.Services;

/// <summary>
/// Service that combines MudBlazor Snackbar functionality with the notification system
/// in the Expert Status Bar. Errors and warnings are added to both snackbar and notifications,
/// while success and info messages only go to snackbar.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Add a message that will show as a snackbar and optionally be added to notifications
    /// </summary>
    void Add(string message, Severity severity);
    
    /// <summary>
    /// Add a message with custom snackbar configuration
    /// </summary>
    void Add(string message, Severity severity, Action<SnackbarOptions> configure);
}

public class NotificationService : INotificationService
{
    private readonly ISnackbar snackbar;
    private ExpertLayout? expertLayout;
    
    public NotificationService(ISnackbar snackbar)
    {
        this.snackbar = snackbar;
    }
    
    /// <summary>
    /// Register the Expert Layout so we can access its notification system
    /// This is called by ExpertLayout during initialization
    /// </summary>
    public void RegisterLayout(ExpertLayout expertLayout)
    {
        this.expertLayout = expertLayout;
    }
    
    public void Add(string message, Severity severity)
    {
        // Always add to snackbar
        snackbar.Add(message, severity);
        
        // Add to notifications only for errors and warnings
        if (severity == Severity.Error || severity == Severity.Warning)
        {
            var notificationSeverity = severity == Severity.Error 
                ? Components.StatusBar.ExpertStatusBar.NotificationSeverity.Error
                : Components.StatusBar.ExpertStatusBar.NotificationSeverity.Warning;
                
            expertLayout?.AddNotification(message, notificationSeverity);
        }
    }
    
    public void Add(string message, Severity severity, Action<SnackbarOptions> configure)
    {
        // Always add to snackbar with configuration
        snackbar.Add(message, severity, configure);
        
        // Add to notifications only for errors and warnings
        if (severity == Severity.Error || severity == Severity.Warning)
        {
            var notificationSeverity = severity == Severity.Error 
                ? Components.StatusBar.ExpertStatusBar.NotificationSeverity.Error
                : Components.StatusBar.ExpertStatusBar.NotificationSeverity.Warning;
                
            expertLayout?.AddNotification(message, notificationSeverity);
        }
    }
}