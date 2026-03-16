(function () {
  // ── History API: browser back button closes modal ────────────────
  let _closedViaPopState = false;

  function pushModalHistory() {
    history.pushState({ vkineModal: true }, '');
  }

  function popModalHistory() {
    if (_closedViaPopState) {
      _closedViaPopState = false;
      return;
    }
    if (history.state && history.state.vkineModal) {
      history.back();
    }
  }

  window.addEventListener('popstate', () => {
    const modal = document.querySelector('[data-testid="movie-modal"]');
    if (!modal || modal.classList.contains('page-mode')) return;
    _closedViaPopState = true;
    const close = modal.querySelector('[data-testid="modal-close"]');
    if (close) close.click();
  });

  // ── Swipe-down to close ──────────────────────────────────────────
  function setupSwipeToClose() {
    const content = document.querySelector('[data-testid="movie-modal-content"]');
    if (!content || content.dataset.swipeBound === '1') return;
    content.dataset.swipeBound = '1';

    let startY = 0;
    let startScrollTop = 0;
    let dragging = false;

    content.addEventListener('touchstart', (e) => {
      startY = e.touches[0].clientY;
      startScrollTop = content.scrollTop;
      dragging = false;
    }, { passive: true });

    content.addEventListener('touchmove', (e) => {
      const dy = e.touches[0].clientY - startY;
      if (startScrollTop <= 0 && dy > 0) {
        dragging = true;
        const clamped = Math.min(dy, window.innerHeight * 0.6);
        content.style.transition = 'none';
        content.style.transform = `translateY(${clamped}px)`;
        if (e.cancelable) e.preventDefault();
      }
    }, { passive: false });

    content.addEventListener('touchend', (e) => {
      if (!dragging) return;
      const dy = e.changedTouches[0].clientY - startY;
      content.style.transition = '';
      if (dy > 80) {
        content.style.transition = 'transform 0.25s ease';
        content.style.transform = `translateY(100%)`;
        setTimeout(() => {
          const close = document.querySelector('[data-testid="modal-close"]');
          if (close) close.click();
        }, 220);
      } else {
        content.style.transform = '';
      }
      dragging = false;
    }, { passive: true });

    content.addEventListener('touchcancel', () => {
      content.style.transition = '';
      content.style.transform = '';
      dragging = false;
    }, { passive: true });
  }


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

  function updateSkipButton() {
    const content = document.querySelector('[data-testid="movie-modal-content"]');
    const btn = content?.querySelector('.skip-to-showtimes');
    if (!content || !btn) return;
    btn.classList.toggle('visible', content.scrollHeight > content.clientHeight);
  }

  function updateScrollTopButton() {
    const content = document.querySelector('[data-testid="movie-modal-content"]');
    const btn = content?.querySelector('.scroll-to-top');
    if (!content || !btn) return;
    const fontSize = parseFloat(getComputedStyle(content).fontSize) || 16;
    const threshold = fontSize * 5;
    btn.classList.toggle('visible', content.scrollTop >= threshold);
  }

  function setupModalScrollControls() {
    const content = document.querySelector('[data-testid="movie-modal-content"]');
    if (!content) return;
    if (content.dataset.scrollControlsBound !== '1') {
      content.addEventListener('scroll', () => {
        updateSkipButton();
        updateScrollTopButton();
      }, { passive: true });
      content.dataset.scrollControlsBound = '1';
    }
    updateSkipButton();
    updateScrollTopButton();
  }

  // ── Scroll position save/restore for Movies ↔ MovieDetail navigation ──
  const SCROLL_KEY = 'vkine-movies-scroll';

  function saveMoviesScroll() {
    sessionStorage.setItem(SCROLL_KEY, String(window.scrollY));
  }

  function restoreMoviesScroll() {
    const y = sessionStorage.getItem(SCROLL_KEY);
    if (y === null) return;
    sessionStorage.removeItem(SCROLL_KEY);
    const top = parseInt(y, 10);
    if (!top) return;
    // Defer to let Blazor finish painting the layout
    requestAnimationFrame(() => window.scrollTo({ top, behavior: 'instant' }));
  }

  // ── Swipe-down to go back (page mode) ───────────────────────────────────
  function setupPageSwipeToClose() {
    const backdrop = document.querySelector('[data-testid="movie-modal"].page-mode');
    if (!backdrop || backdrop.dataset.pageSwipeBound === '1') return;
    backdrop.dataset.pageSwipeBound = '1';

    const content = backdrop.querySelector('[data-testid="movie-modal-content"]');
    let startY = 0;
    let dragging = false;

    function applyDrag(dy) {
      const clamped = Math.min(dy, window.innerHeight * 0.6);
      backdrop.style.transition = 'none';
      backdrop.style.transform = `translateY(${clamped}px)`;
      if (content) {
        content.style.borderRadius = '22px 22px 0 0';
        content.style.overflow = 'clip';
      }
    }

    function resetDrag() {
      backdrop.style.transition = 'transform 0.3s ease';
      backdrop.style.transform = '';
      if (content) {
        content.style.transition = 'border-radius 0.3s ease';
        content.style.borderRadius = '';
        setTimeout(() => {
          content.style.transition = '';
          content.style.overflow = '';
        }, 300);
      }
      setTimeout(() => backdrop.style.transition = '', 300);
    }

    document.addEventListener('touchstart', (e) => {
      startY = e.touches[0].clientY;
      dragging = false;
    }, { passive: true });

    document.addEventListener('touchmove', (e) => {
      if (window.scrollY > 0) return;
      const dy = e.touches[0].clientY - startY;
      if (dy > 0) {
        dragging = true;
        applyDrag(dy);
        if (e.cancelable) e.preventDefault();
      }
    }, { passive: false });

    document.addEventListener('touchend', (e) => {
      if (!dragging) return;
      const dy = e.changedTouches[0].clientY - startY;
      if (dy > 80) {
        backdrop.style.transition = 'transform 0.25s ease';
        backdrop.style.transform = `translateY(100%)`;
        setTimeout(() => {
          const close = backdrop.querySelector('[data-testid="modal-close"]');
          if (close) close.click();
        }, 220);
      } else {
        resetDrag();
      }
      dragging = false;
    }, { passive: true });

    document.addEventListener('touchcancel', () => {
      resetDrag();
      dragging = false;
    }, { passive: true });
  }

  // Scroll controls for page mode (scroll on window instead of modal-content)
  function setupPageScrollControls() {
    if (window._vkinePageScrollBound) return;
    window._vkinePageScrollBound = true;

    function update() {
      const content = document.querySelector('[data-testid="movie-modal-content"]');
      if (!content) return;

      const scrollTopBtn = content.querySelector('.scroll-to-top');
      if (scrollTopBtn) {
        scrollTopBtn.classList.toggle('visible', window.scrollY > 80);
      }

      const skipBtn = content.querySelector('.skip-to-showtimes');
      if (skipBtn) {
        const showtimes = document.getElementById('showtimes-section');
        const visible = showtimes ? showtimes.getBoundingClientRect().top > window.innerHeight : false;
        skipBtn.classList.toggle('visible', visible);
      }
    }

    window.addEventListener('scroll', update, { passive: true });
    update();
  }

  window.vkineMovie = Object.assign(window.vkineMovie || {}, {
    analyzeBackdrop,
    clearBackdropClass,
    installEscapeHandler,
    removeEscapeHandler,
    lockScroll,
    unlockScroll,
    updateSkipButton,
    updateScrollTopButton,
    setupModalScrollControls,
    setupPageScrollControls,
    setupPageSwipeToClose,
    saveMoviesScroll,
    restoreMoviesScroll,
    pushModalHistory,
    popModalHistory,
    setupSwipeToClose
  });
})();