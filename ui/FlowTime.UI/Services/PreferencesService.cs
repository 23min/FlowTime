using Microsoft.JSInterop;

namespace FlowTime.UI.Services;

// Central lightweight preferences store (localStorage backed)
// Consolidates: selected model key (api-demo), future additions (layout prefs, wrap toggle, etc.)
public sealed class PreferencesService
{
	private readonly IJSRuntime js;
	private bool loaded;

	private const string modelKeyStorage = "ft.modelKey";

	public string? ModelKey { get; private set; }

	public PreferencesService(IJSRuntime js) => this.js = js;

	public async Task EnsureLoadedAsync()
	{
		if (loaded) return; loaded = true;
		try
		{
			ModelKey = await js.InvokeAsync<string?>("localStorage.getItem", modelKeyStorage);
		}
		catch { /* ignore */ }
	}

	public async Task SetModelKeyAsync(string key)
	{
		ModelKey = key;
		try { await js.InvokeVoidAsync("localStorage.setItem", modelKeyStorage, key); } catch { }
	}
}
