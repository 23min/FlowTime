window.ftTheme = {
  get: function(){ return localStorage.getItem('ft-theme') || 'light'; },
  set: function(mode){
    localStorage.setItem('ft-theme', mode);
    document.body.classList.toggle('dark-mode', mode==='dark');
    try {
      const evt = new CustomEvent('ft-theme-changed', { detail: mode });
      window.dispatchEvent(evt);
    } catch { /* ignore */ }
  }
};

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
