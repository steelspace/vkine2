let observer = null;
let dotnetRef = null;
let pendingIds = new Set();
let debounceTimer = null;
let flatpickrInstance = null;

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
        altFormat: 'M j, Y',
        minDate: 'today',
        allowInput: false,
        clickOpens: true,
        onChange: (selectedDates) => {
            if (selectedDates.length === 2) {
                const from = formatDate(selectedDates[0]);
                const to = formatDate(selectedDates[1]);
                dotnetRef.invokeMethodAsync('OnDateRangeChanged', from, to);
            }
        },
        onClose: (selectedDates) => {
            // If user closes with only 1 date or 0, treat as same-day or clear
            if (selectedDates.length === 1) {
                const from = formatDate(selectedDates[0]);
                dotnetRef.invokeMethodAsync('OnDateRangeChanged', from, from);
            }
        }
    });
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
    pendingIds.clear();
    dotnetRef = null;
}
