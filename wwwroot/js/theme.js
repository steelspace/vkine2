(function () {
  const storageKey = 'vkine-theme'; // values: 'system' | 'light' | 'dark'

  function appliedThemeFromMode(mode) {
    if (mode === 'system' || !mode) {
      return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    return mode;
  }

  function apply(mode) {
    const actual = appliedThemeFromMode(mode);
    document.documentElement.setAttribute('data-theme', actual);
    // expose current applied theme value
    document.documentElement.dataset.vkineThemeMode = mode || 'system';
    document.dispatchEvent(new CustomEvent('vkine-theme-changed', { detail: { mode, applied: actual } }));
  }

  function set(mode) {
    try {
      if (mode === 'system') {
        localStorage.removeItem(storageKey);
      } else {
        localStorage.setItem(storageKey, mode);
      }
    } catch (e) {
      console.warn('Could not persist theme', e);
    }
    apply(mode);
  }

  function get() {
    try {
      return localStorage.getItem(storageKey) || 'system';
    } catch (e) {
      return 'system';
    }
  }

  function init() {
    const stored = get();
    apply(stored);

    // react to OS preference changes when mode is system
    if (window.matchMedia) {
      const mq = window.matchMedia('(prefers-color-scheme: dark)');
      mq.addEventListener && mq.addEventListener('change', (e) => {
        const current = get();
        if (!current || current === 'system') {
          apply('system');
        }
      });
    }
  }

  // expose API
  window.vkineTheme = {
    init,
    get,
    set,
    apply,
    toggle() {
      const current = get();
      const next = current === 'system' ? 'dark' : current === 'dark' ? 'light' : 'system';
      set(next);
      return next;
    }
  };

  // auto init on load
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
