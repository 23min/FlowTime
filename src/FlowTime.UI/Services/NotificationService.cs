using MudBlazor;
using FlowTime.UI.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<NotificationService> logger;
    private ExpertLayout? expertLayout;
    
    public NotificationService(ISnackbar snackbar, ILogger<NotificationService> logger)
    {
        this.snackbar = snackbar;
        this.logger = logger;
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
        // Log to console for debugging
        LogNotification(message, severity);
        
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
        // Log to console for debugging
        LogNotification(message, severity);
        
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
    
    private void LogNotification(string message, Severity severity)
    {
        switch (severity)
        {
            case Severity.Error:
                logger.LogError("NOTIFICATION: {Message}", message);
                break;
            case Severity.Warning:
                logger.LogWarning("NOTIFICATION: {Message}", message);
                break;
            case Severity.Info:
                logger.LogInformation("NOTIFICATION: {Message}", message);
                break;
            case Severity.Success:
                logger.LogInformation("NOTIFICATION (Success): {Message}", message);
                break;
            default:
                logger.LogDebug("NOTIFICATION: {Message}", message);
                break;
        }
    }
}