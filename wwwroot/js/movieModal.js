(function () {
  // Analyze the top area of a backdrop image and toggle .backdrop-light / .backdrop-dark
  function analyzeBackdrop(img, containerSelector) {
    try {
      const container = typeof containerSelector === 'string' ? document.querySelector(containerSelector) : containerSelector;
      if (!img || !container) return;

      const w = img.naturalWidth || img.width;
      const h = img.naturalHeight || img.height;
      if (!w || !h) return;

      // sample only the top portion (header overlays top); downscale for performance
      const sampleH = Math.min(h, Math.max(32, Math.floor(h * 0.18)));
      const sampleW = 120;

      const canvas = document.createElement('canvas');
      canvas.width = sampleW;
      canvas.height = sampleH;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;

      // draw the top slice of the source image into the small canvas
      ctx.drawImage(img, 0, 0, w, sampleH, 0, 0, sampleW, sampleH);

      // compute average luminance
      const data = ctx.getImageData(0, 0, sampleW, sampleH).data;
      let total = 0;
      let count = 0;
      for (let i = 0; i < data.length; i += 4) {
        const r = data[i], g = data[i + 1], b = data[i + 2];
        const lum = 0.2126 * r + 0.7152 * g + 0.0722 * b; // relative luminance (approx)
        total += lum;
        count++;
      }

      const avg = total / Math.max(1, count);
      const threshold = 150; // above => light background

      if (avg >= threshold) {
        container.classList.add('backdrop-light');
        container.classList.remove('backdrop-dark');
      } else {
        container.classList.add('backdrop-dark');
        container.classList.remove('backdrop-light');
      }
    } catch (err) {
      // canvas may be tainted (CORS) — default to dark header for safety
      try {
        const container = typeof containerSelector === 'string' ? document.querySelector(containerSelector) : containerSelector;
        if (container) {
          container.classList.add('backdrop-dark');
          container.classList.remove('backdrop-light');
        }
      } catch (e) { /* ignore */ }
    }
  }

  function clearBackdropClass(containerSelector) {
    try {
      const container = typeof containerSelector === 'string' ? document.querySelector(containerSelector) : containerSelector;
      if (container) container.classList.remove('backdrop-light', 'backdrop-dark');
    } catch (e) { /* ignore */ }
  }

  // Escape-to-close: install a single global listener that clicks the modal close button when Escape is pressed
  let _escapeInstalled = false;
  function _escapeHandler(e) {
    if (!e) return;
    if (e.key === 'Escape' || e.key === 'Esc') {
      const modal = document.querySelector('[data-testid="movie-modal"]');
      if (!modal) return;
      const close = modal.querySelector('[data-testid="modal-close"]');
      if (close) close.click();
    }
  }
  function installEscapeHandler() {
    if (_escapeInstalled) return;
    window.addEventListener('keydown', _escapeHandler, true);
    _escapeInstalled = true;
  }
  function removeEscapeHandler() {
    if (!_escapeInstalled) return;
    window.removeEventListener('keydown', _escapeHandler, true);
    _escapeInstalled = false;
  }

  // auto-install (safe/idempotent)
  installEscapeHandler();

  function lockScroll() {
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    document.body.style.overflow = 'hidden';
    document.body.style.paddingRight = scrollbarWidth + 'px';
  }

  function unlockScroll() {
    document.body.style.overflow = '';
    document.body.style.paddingRight = '';
  }

  window.vkineMovie = Object.assign(window.vkineMovie || {}, {
    analyzeBackdrop,
    clearBackdropClass,
    installEscapeHandler,
    removeEscapeHandler,
    lockScroll,
    unlockScroll
  });
})();