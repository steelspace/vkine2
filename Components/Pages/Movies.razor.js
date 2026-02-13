let observer = null;
let dotnetRef = null;
let pendingIds = new Set();
let debounceTimer = null;

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
    pendingIds.clear();
    dotnetRef = null;
}
