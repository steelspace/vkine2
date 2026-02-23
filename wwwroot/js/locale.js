(function () {
  const cookieName = '.AspNetCore.Culture';
  const cookieOptions = 'path=/;SameSite=Lax';

  function safe(fn) {
    try {
      return fn();
    } catch {
      return null;
    }
  }

  function parseCulturePair(value) {
    if (!value) return null;
    const match = value.match(/c=([^|]+)/);
    return match ? match[1] : null;
  }

  function readCookie() {
    return safe(() => {
      const match = document.cookie.match(new RegExp('(^| )' + cookieName + '=([^;]+)'));
      return match ? decodeURIComponent(match[2]) : null;
    });
  }

  function writeCookie(value) {
    safe(() => {
      document.cookie = `${cookieName}=${encodeURIComponent(value)};${cookieOptions}`;
    });
  }

  function getCurrentCulture() {
    const cookieValue = readCookie();
    return parseCulturePair(cookieValue) || 'en';
  }

  function applyCulture(culture) {
    const normalized = culture || 'en';
    document.documentElement.lang = normalized;
  }

  function setCulture(culture) {
    if (!culture) return;
    const normalized = culture;
    writeCookie(`c=${normalized}|uic=${normalized}`);
    applyCulture(normalized);
  }

  function setCultureAndReload(culture) {
    setCulture(culture);
    safe(() => location.reload());
  }

  function init() {
    applyCulture(getCurrentCulture());
  }

  window.vkineLocale = {
    init,
    getCulture: getCurrentCulture,
    setCulture,
    setCultureAndReload,
    applyCulture
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
