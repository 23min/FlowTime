using FlowTime.Sim.Service;

namespace FlowTime.Sim.Service.Extensions;

/// <summary>
/// Extension methods for validating templates at application startup
/// </summary>
public static class TemplateValidationExtensions
{
    /// <summary>
    /// Validates all templates in the templates directory at startup.
    /// Logs warnings for any templates that fail to parse, but does not fail startup.
    /// Performs the same validation as runtime template loading.
    /// </summary>
    /// <param name="app">The WebApplication instance</param>
    /// <returns>A task representing the validation operation</returns>
    public static async Task ValidateTemplatesAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Validating templates at startup...");
            // Use the node-based core service
            var templateService = app.Services.GetRequiredService<FlowTime.Sim.Core.Services.INodeBasedTemplateService>();
            var templates = await templateService.GetAllTemplatesAsync();
            
            if (templates.Count == 0)
            {
                logger.LogWarning("No templates found in templates directory");
            }
            else
            {
                logger.LogInformation("Successfully validated {TemplateCount} templates at startup", templates.Count);
                
                // Log template IDs for visibility
                foreach (var template in templates)
                {
                    logger.LogDebug("Validated template: {TemplateId} - {TemplateTitle}", template.Metadata.Id, template.Metadata.Title);
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail startup - just warn
            logger.LogError(ex, "Template validation encountered an error at startup");
        }
    }
}