window.ftTheme = {
  get: function(){ return localStorage.getItem('ft-theme') || 'light'; },
  set: function(mode){ localStorage.setItem('ft-theme', mode); document.body.classList.toggle('dark-mode', mode==='dark'); }
};