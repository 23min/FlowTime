window.ftTheme = {
  get: function(){
    return localStorage.getItem('ft.theme')
        || localStorage.getItem('ft-theme')
        || 'light';
  },
  set: function(mode){
    localStorage.setItem('ft.theme', mode);
    localStorage.setItem('ft-theme', mode);
    if (document.body) {
      document.body.classList.toggle('dark-mode', mode === 'dark');
      document.body.dataset.ftTheme = mode;
    }
    try {
      const evt = new CustomEvent('ft-theme-changed', { detail: mode });
      window.dispatchEvent(evt);
    } catch { /* ignore */ }
  }
};

(function(){
  try {
    const stored = window.ftTheme.get();
    window.ftTheme.set(stored);
  } catch {
    // ignore startup failures
  }
})();

window.FlowTime = window.FlowTime || {};
window.FlowTime.downloadText = function(filename, content, mimeType){
  try {
    const blob = new Blob([content], { type: mimeType || 'text/plain' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename || 'flowtime-export.txt';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function(){ URL.revokeObjectURL(link.href); }, 0);
  } catch (err) {
    console.error('FlowTime.downloadText failed', err);
  }
};

window.FlowTime.openTextInNewTab = function(filename, content, mimeType){
  try {
    const blob = new Blob([content], { type: mimeType || 'text/plain' });
    const url = URL.createObjectURL(blob);
    const newTab = window.open(url, '_blank', 'noopener');
    if (!newTab) {
      return;
    }
    newTab.document.title = filename || 'flowtime-export';
    setTimeout(function(){ URL.revokeObjectURL(url); }, 0);
  } catch (err) {
    console.error('FlowTime.openTextInNewTab failed', err);
  }
};
