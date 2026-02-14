let observer = null;
let dotnetRef = null;
let pendingIds = new Set();
let debounceTimer = null;
let flatpickrInstance = null;
let scrollHandler = null;

export function observeCards(gridElement, dotnetReference) {
    // Disconnect previous observer if re-attaching after search clear
    if (observer) {
        observer.disconnect();
    }

    dotnetRef = dotnetReference;

    observer = new IntersectionObserver((entries) => {
        let hasNew = false;
        for (const entry of entries) {
            if (entry.isIntersecting) {
                const id = parseInt(entry.target.dataset.movieId, 10);
                if (!isNaN(id) && entry.target.classList.contains('placeholder')) {
                    pendingIds.add(id);
                    hasNew = true;
                }
                // Stop observing once triggered
                observer.unobserve(entry.target);
            }
        }
        if (hasNew) {
            flushPending();
        }
    }, {
        root: null,
        rootMargin: '400px',   // start loading 400px before the card is in view
        threshold: 0
    });

    // Observe all placeholder cards currently in the grid
    observePlaceholders(gridElement);

    // Use MutationObserver to pick up cards Blazor re-renders (placeholder → loaded → placeholder again on re-render)
    const mutationObs = new MutationObserver(() => observePlaceholders(gridElement));
    mutationObs.observe(gridElement, { childList: true, subtree: true });
}

export function initDateRangePicker(inputElement, dotnetReference) {
    if (flatpickrInstance) {
        flatpickrInstance.destroy();
    }

    dotnetRef = dotnetReference;

    flatpickrInstance = flatpickr(inputElement, {
        mode: 'range',
        dateFormat: 'Y-m-d',
        altInput: true,
        altFormat: 'M j',
        minDate: 'today',
        allowInput: false,
        clickOpens: true,
        animate: true,
        monthSelectorType: 'static',
        prevArrow: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"></polyline></svg>',
        nextArrow: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg>',
        onChange: (selectedDates) => {
            if (selectedDates.length === 2) {
                const from = formatDate(selectedDates[0]);
                const to = formatDate(selectedDates[1]);
                dotnetRef.invokeMethodAsync('OnDateRangeChanged', from, to);
            }
        },
        onClose: (selectedDates) => {
            if (selectedDates.length === 1) {
                const from = formatDate(selectedDates[0]);
                dotnetRef.invokeMethodAsync('OnDateRangeChanged', from, from);
            }
        }
    });

    // copy accessible name to the altInput that flatpickr creates
    try {
        if (flatpickrInstance && flatpickrInstance.altInput) {
            const alt = flatpickrInstance.altInput;
            const aria = inputElement.getAttribute('aria-label') || inputElement.getAttribute('placeholder') || 'Date range';
            if (!alt.getAttribute('aria-label')) alt.setAttribute('aria-label', aria);
            if (!alt.getAttribute('role')) alt.setAttribute('role', 'textbox');
        }
    } catch (err) {
        // best-effort
        console.warn('flatpickr aria copy failed', err);
    }
}

export function clearDateRange() {
    if (flatpickrInstance) {
        flatpickrInstance.clear();
    }
}

function formatDate(date) {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
}

function observePlaceholders(gridElement) {
    if (!observer || !gridElement) return;
    const cards = gridElement.querySelectorAll('.movie-card.placeholder');
    for (const card of cards) {
        observer.observe(card);
    }
}

function flushPending() {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
        if (pendingIds.size === 0 || !dotnetRef) return;
        const ids = Array.from(pendingIds);
        pendingIds.clear();
        dotnetRef.invokeMethodAsync('OnCardsVisible', ids);
    }, 100);   // 100ms debounce to batch cards that enter viewport together
}

export function initStickyToolbar(toolbarElement) {
    if (scrollHandler) {
        window.removeEventListener('scroll', scrollHandler, { passive: true });
    }

    // Ensure accessible names/ids are present at runtime (helps axe/pa11y)
    try {
        const searchInput = toolbarElement.querySelector('.search-input');
        if (searchInput && !searchInput.getAttribute('aria-label')) {
            const placeholder = searchInput.getAttribute('placeholder') || 'Search';
            searchInput.setAttribute('aria-label', placeholder);
        }

        const dateInput = toolbarElement.querySelector('.date-range-input');
        if (dateInput && !dateInput.getAttribute('aria-label')) {
            dateInput.setAttribute('aria-label', 'Date range');
        }

        const label = toolbarElement.querySelector('.time-filter-label');
        const value = toolbarElement.querySelector('.time-filter-value');
        const slider = toolbarElement.querySelector('.time-slider');
        if (label && !label.id) label.id = 'time-filter-label';
        if (value && !value.id) value.id = 'time-filter-value';
        if (slider && !slider.getAttribute('aria-labelledby')) slider.setAttribute('aria-labelledby', 'time-filter-label');
        if (slider && !slider.getAttribute('aria-describedby')) slider.setAttribute('aria-describedby', 'time-filter-value');
    } catch (err) {
        // defensive — accessibility augmentations are best-effort
        console.warn('initStickyToolbar accessibility init failed', err);
    }

    let lastScrollY = window.scrollY;
    let ticking = false;

    scrollHandler = () => {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(() => {
            const currentY = window.scrollY;
            // Only hide/show after scrolling past the toolbar's own height
            if (currentY > 80) {
                if (currentY > lastScrollY + 5) {
                    // Scrolling down — hide
                    toolbarElement.classList.add('toolbar-hidden');
                } else if (currentY < lastScrollY - 3) {
                    // Scrolling up (even slightly) — show
                    toolbarElement.classList.remove('toolbar-hidden');
                }
            } else {
                // Near top — always show
                toolbarElement.classList.remove('toolbar-hidden');
            }
            lastScrollY = currentY;
            ticking = false;
        });
    };

    window.addEventListener('scroll', scrollHandler, { passive: true });
}

export function dispose() {
    clearTimeout(debounceTimer);
    if (observer) {
        observer.disconnect();
        observer = null;
    }
    if (flatpickrInstance) {
        flatpickrInstance.destroy();
        flatpickrInstance = null;
    }
    if (scrollHandler) {
        window.removeEventListener('scroll', scrollHandler, { passive: true });
        scrollHandler = null;
    }
    pendingIds.clear();
    dotnetRef = null;
}
