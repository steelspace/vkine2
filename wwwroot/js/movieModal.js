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

  window.vkineMovie = {
    analyzeBackdrop,
    clearBackdropClass
  };
})();