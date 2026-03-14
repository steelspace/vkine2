(function () {
  const storageKey = 'vkine-theme'; // values: 'light' | 'dark'

  function apply(mode) {
    document.documentElement.setAttribute('data-theme', mode);
    document.documentElement.dataset.vkineThemeMode = mode;
    document.dispatchEvent(new CustomEvent('vkine-theme-changed', { detail: { mode, applied: mode } }));
  }

  function set(mode) {
    try {
      localStorage.setItem(storageKey, mode);
    } catch (e) {
      console.warn('Could not persist theme', e);
    }
    apply(mode);
  }

  function get() {
    try {
      const stored = localStorage.getItem(storageKey);
      return stored === 'dark' ? 'dark' : 'light';
    } catch (e) {
      return 'light';
    }
  }

  function init() {
    apply(get());

    // DOM fallback: allow elements with `.toggle-theme` to toggle theme even when Blazor/interop is not connected
    document.addEventListener('click', function (ev) {
      try {
        const el = ev.target && ev.target.closest && ev.target.closest('.toggle-theme');
        if (el) {
          ev.preventDefault();
          window.vkineTheme.toggle();
          console.debug('[vkine-theme] toggle clicked (DOM fallback)');
        }
      } catch (err) {
        console.warn('[vkine-theme] click-handler error', err);
      }
    });
  }

  // expose API
  window.vkineTheme = {
    init,
    get,
    set,
    apply,
    toggle() {
      const next = get() === 'dark' ? 'light' : 'dark';
      set(next);
      return next;
    }
  };

  function getPreferred() {
    try {
      const stored = localStorage.getItem(storageKey);
      if (stored === 'dark' || stored === 'light') return stored;
      return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    } catch (e) {
      return 'light';
    }
  }

  // Guard against Blazor's DOM morph stripping data-theme from <html>
  function installThemeGuard() {
    new MutationObserver(() => {
      const current = document.documentElement.getAttribute('data-theme');
      const preferred = getPreferred();
      if (current !== preferred) {
        document.documentElement.setAttribute('data-theme', preferred);
        document.documentElement.dataset.vkineThemeMode = preferred;
      }
    }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  }

  // auto init on load
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => { init(); installThemeGuard(); });
  } else {
    init();
    installThemeGuard();
  }
})();
