/**
 * Steam-style hover preview card for game entries.
 *
 * Attaches to any element with [data-game-appid].
 * Shows a floating panel after a short delay; the panel:
 *   - fetches /api/game-preview/{appId} on first hover
 *   - auto-plays the trailer video, or runs a screenshot slideshow
 *   - follows the cursor vertically, stays anchored horizontally near the card
 *   - fades in/out smoothly
 */
(function () {
    'use strict';

    const SHOW_DELAY_MS  = 200;  // delay before showing
    const PANEL_WIDTH    = 380;  // px
    const SLIDE_INTERVAL = 3500; // ms between screenshots

    let hoverTimer   = null;
    let slideTimer   = null;
    let slideIndex   = 0;
    let currentCard  = null;

    // Cache fetched data to avoid re-fetching on re-hover
    const cache = new Map();

    // ── Panel lifecycle ───────────────────────────────────────────────────────

    function getPanel() {
        let p = document.getElementById('game-hover-panel');
        if (!p) {
            p = document.createElement('div');
            p.id = 'game-hover-panel';
            p.className = 'game-hover-panel';
            document.body.appendChild(p);
        }
        return p;
    }

    function showLoading(panel, clientX, clientY) {
        panel.innerHTML = '<div class="ghp-loading">Loading…</div>';
        panel.classList.add('game-hover-panel--visible');
        positionPanel(panel, clientX, clientY);
    }

    function hidePanel() {
        clearTimeout(hoverTimer);
        clearInterval(slideTimer);
        currentCard = null;
        const p = document.getElementById('game-hover-panel');
        if (p) p.classList.remove('game-hover-panel--visible');
    }

    // ── Data fetch ────────────────────────────────────────────────────────────

    async function fetchData(appId) {
        if (cache.has(appId)) return cache.get(appId);
        try {
            const res = await fetch(`/api/game-preview/${appId}`);
            if (!res.ok || res.status === 204) return null;
            const text = await res.text();
            if (!text) return null;
            const data = JSON.parse(text);
            cache.set(appId, data);
            return data;
        } catch { return null; }
    }

    // ── Panel rendering ───────────────────────────────────────────────────────

    function reviewClass(score) {
        if (score >= 8) return 'positive';
        if (score >= 5) return 'mixed';
        if (score >  0) return 'negative';
        return 'neutral';
    }

    function esc(str) {
        const d = document.createElement('div');
        d.textContent = str;
        return d.innerHTML;
    }

    function renderPrice(data) {
        if (data.isFree) return '<span class="ghp-free">Free to Play</span>';
        if (!data.price)  return '';
        if (data.discountPercent > 0) {
            return `<span class="ghp-discount">-${data.discountPercent}%</span>`
                 + `<span class="ghp-price-final">${esc(data.price)}</span>`
                 + (data.priceOriginal
                     ? `<span class="ghp-price-orig">${esc(data.priceOriginal)}</span>` : '');
        }
        return `<span class="ghp-price-final">${esc(data.price)}</span>`;
    }

    function renderPanel(panel, data, clientX, clientY) {
        const rc = reviewClass(data.reviewScore);

        const mediaHtml = '<div class="ghp-media" id="ghp-media"></div>';

        const reviewHtml = data.reviewSummary
            ? `<div class="ghp-review ghp-review--${rc}">${esc(data.reviewSummary)}</div>` : '';

        const dateHtml = data.releaseDate
            ? `<div class="ghp-date">${esc(data.releaseDate)}</div>` : '';

        const priceHtml = (data.price || data.isFree)
            ? `<div class="ghp-price-row">${renderPrice(data)}</div>` : '';

        const tagsHtml = data.tags?.length
            ? '<div class="ghp-tags">'
                + data.tags.slice(0, 8).map(t => `<span class="ghp-tag">${esc(t)}</span>`).join('')
                + '</div>'
            : '';

        const allCats = [
            ...(data.genres     || []),
            ...(data.categories || []),
        ];
        const catsHtml = allCats.length
            ? `<div class="ghp-genres">${esc(allCats.join(' · '))}</div>` : '';

        panel.innerHTML = `
            ${mediaHtml}
            <div class="ghp-body">
                <div class="ghp-title">${esc(data.name || '')}</div>
                ${reviewHtml}
                ${dateHtml}
                ${priceHtml}
                ${tagsHtml}
                ${catsHtml}
            </div>`;

        startMedia(data);
        positionPanel(panel, clientX, clientY);
    }

    // ── Media (video / slideshow) ─────────────────────────────────────────────

    function startMedia(data) {
        clearInterval(slideTimer);
        const mediaEl = document.getElementById('ghp-media');
        if (!mediaEl) return;

        if (data.trailerUrl) {
            mediaEl.innerHTML =
                `<video class="ghp-video" src="${encodeURI(data.trailerUrl)}"
                    autoplay muted loop playsinline preload="auto"></video>`;
            return;
        }

        const shots = data.screenshots || [];
        if (shots.length === 0) { mediaEl.style.display = 'none'; return; }

        slideIndex = 0;
        mediaEl.innerHTML = `<img class="ghp-screenshot" src="${encodeURI(shots[0])}" alt="" />`;

        if (shots.length > 1) {
            slideTimer = setInterval(() => {
                const img = document.querySelector('#ghp-media .ghp-screenshot');
                if (!img) return;
                slideIndex = (slideIndex + 1) % shots.length;
                // Crossfade: fade out → swap src → fade in
                img.style.opacity = '0';
                setTimeout(() => {
                    img.src = shots[slideIndex];
                    img.style.opacity = '1';
                }, 250);
            }, SLIDE_INTERVAL);
        }
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    function positionPanel(panel, clientX, clientY) {
        const vw  = window.innerWidth;
        const vh  = window.innerHeight;
        const pw  = PANEL_WIDTH;
        const ph  = panel.offsetHeight || 420;

        // Prefer right of cursor; flip left if not enough room
        let left = clientX + 24;
        if (left + pw > vw - 8) left = clientX - pw - 24;
        left = Math.max(8, left);

        // Follow cursor Y, clamped to viewport
        let top = clientY - 100;
        top = Math.max(8, Math.min(top, vh - ph - 8));

        panel.style.left  = left + 'px';
        panel.style.top   = top  + 'px';
        panel.style.width = pw   + 'px';
    }

    // ── Card interaction ──────────────────────────────────────────────────────

    async function onCardEnter(card, clientX, clientY) {
        currentCard = card;
        const appId = card.dataset.gameAppid;
        if (!appId) return;

        clearTimeout(hoverTimer);
        let lastX = clientX, lastY = clientY;

        // Store latest cursor pos via closure for mousemove (attached below)
        card._ghpSetPos = (x, y) => { lastX = x; lastY = y; };

        hoverTimer = setTimeout(async () => {
            if (currentCard !== card) return;

            const panel = getPanel();
            showLoading(panel, lastX, lastY);

            const data = await fetchData(appId);
            if (currentCard !== card) return; // user left while fetching

            if (!data) {
                panel.classList.remove('game-hover-panel--visible');
                return;
            }
            renderPanel(panel, data, lastX, lastY);
        }, SHOW_DELAY_MS);
    }

    function onCardMove(card, clientX, clientY) {
        card._ghpSetPos?.(clientX, clientY);
        const panel = document.getElementById('game-hover-panel');
        if (panel?.classList.contains('game-hover-panel--visible')) {
            positionPanel(panel, clientX, clientY);
        }
    }

    function onCardLeave() {
        hidePanel();
    }

    // ── Attach / observe ──────────────────────────────────────────────────────

    function attachCard(card) {
        if (card._ghpAttached) return;
        card._ghpAttached = true;

        card.addEventListener('mouseenter', e => onCardEnter(card, e.clientX, e.clientY));
        card.addEventListener('mousemove',  e => onCardMove(card, e.clientX, e.clientY));
        card.addEventListener('mouseleave', ()  => onCardLeave());
    }

    function attachAll() {
        document.querySelectorAll('[data-game-appid]').forEach(attachCard);
    }

    // Re-attach when Blazor re-renders game cards
    const observer = new MutationObserver(mutations => {
        for (const m of mutations) {
            for (const node of m.addedNodes) {
                if (node.nodeType !== 1) continue;
                if (node.dataset?.gameAppid) attachCard(node);
                node.querySelectorAll?.('[data-game-appid]').forEach(attachCard);
            }
        }
    });

    observer.observe(document.body, { childList: true, subtree: true });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', attachAll);
    } else {
        attachAll();
    }
})();
