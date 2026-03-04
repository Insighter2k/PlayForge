/**
 * Cover image retry + placeholder logic.
 *
 * Usage on any cover <img>:
 *   <img src="..." data-src="..." onerror="window.onCoverError(this)" />
 *   <div class="cover-placeholder ..." style="display:none" aria-hidden="true"></div>
 *
 * On error: retries up to MAX_RETRIES times (with cache-busting) before
 * hiding the image and revealing the immediately-following placeholder sibling.
 */
(function () {
    'use strict';

    const RETRY_DELAYS = [1000, 2000]; // ms before each retry attempt

    function showPlaceholder(img) {
        var ph = img.nextElementSibling;
        if (ph && ph.classList.contains('cover-placeholder')) {
            ph.style.display = '';
        }
    }

    window.onCoverError = function (img) {
        var src = img.dataset.src;

        // No original URL stored — give up immediately
        if (!src) {
            img.style.display = 'none';
            showPlaceholder(img);
            return;
        }

        var retries = parseInt(img.dataset.coverRetries || '0', 10);

        if (retries < RETRY_DELAYS.length) {
            img.dataset.coverRetries = retries + 1;
            setTimeout(function () {
                // Append cache-buster so the browser re-issues the request
                img.src = src + '?_r=' + (retries + 1);
            }, RETRY_DELAYS[retries]);
        } else {
            img.style.display = 'none';
            showPlaceholder(img);
        }
    };
})();
