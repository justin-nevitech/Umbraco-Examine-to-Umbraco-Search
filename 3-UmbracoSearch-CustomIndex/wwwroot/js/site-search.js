// Updates the site search without a page reload. The page works the same without this script:
// it fetches the same URLs the links and form already use and swaps in the new #search-results.
(() => {
    const containerId = 'search-results';
    const container = document.getElementById(containerId);
    if (!container || !window.fetch || !window.DOMParser || !history.pushState) {
        return;
    }

    const searchPath = location.pathname;
    let controller = null;

    // Facet, chip, clear and pager links all point back at this page; result links go elsewhere
    const isSearchLink = (link) =>
        link.origin === location.origin && link.pathname === searchPath && !link.hasAttribute('download') && !link.target;

    async function load(url, push) {
        controller?.abort();
        controller = new AbortController();

        container.setAttribute('aria-busy', 'true');
        container.classList.add('opacity-50');

        try {
            const response = await fetch(url, { signal: controller.signal, headers: { 'X-Requested-With': 'fetch' } });
            if (!response.ok) {
                throw new Error(`Search request failed: ${response.status}`);
            }

            const doc = new DOMParser().parseFromString(await response.text(), 'text/html');
            const updated = doc.getElementById(containerId);
            if (!updated) {
                throw new Error('Search results not found in response');
            }

            container.innerHTML = updated.innerHTML;
            if (push) {
                history.pushState(null, '', url);
            }

            // Move focus to the result count so keyboard and screen reader users hear the new results
            document.getElementById('search-results-heading')?.focus();
        } catch (error) {
            if (error.name === 'AbortError') {
                return;
            }
            // Fall back to a normal page load
            location.href = url;
        } finally {
            container.removeAttribute('aria-busy');
            container.classList.remove('opacity-50');
        }
    }

    container.addEventListener('click', (event) => {
        const link = event.target.closest('a[href]');
        if (!link || !isSearchLink(link) || event.defaultPrevented || event.button !== 0
            || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }

        event.preventDefault();
        load(link.href, true);
    });

    container.addEventListener('submit', (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() !== 'get') {
            return;
        }

        event.preventDefault();
        const url = new URL(form.action, location.href);
        url.search = new URLSearchParams(new FormData(form)).toString();
        load(url.href, true);
    });

    // Back and forward buttons
    window.addEventListener('popstate', () => load(location.href, false));
})();
