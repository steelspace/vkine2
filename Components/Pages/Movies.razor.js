let observer = null;
let dotnetRef = null;

export function initializeScrollObserver(element, dotnetReference) {
    dotnetRef = dotnetReference;
    
    // Create intersection observer for infinite scroll
    const options = {
        root: null,
        rootMargin: '600px', // Trigger 600px before reaching the end
        threshold: 0
    };
    
    observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting && dotnetRef) {
                dotnetRef.invokeMethodAsync('OnScrollNearEnd');
            }
        });
    }, options);
    
    // Observe the container to detect when user scrolls near the bottom
    if (element) {
        observer.observe(element);
    }
}

export function dispose() {
    if (observer) {
        observer.disconnect();
        observer = null;
    }
    dotnetRef = null;
}
