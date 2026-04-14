/* kontakte.js — KontakteDB client-side scripts */

(function () {
    'use strict';

    // ── Sidebar toggle (mobile) ──────────────────────────────────────────────
    const sidebar = document.getElementById('sidebar');
    const toggleBtn = document.getElementById('sidebarToggle');

    if (sidebar && toggleBtn) {
        // Create overlay
        const overlay = document.createElement('div');
        overlay.className = 'mc-sidebar-overlay';
        document.body.appendChild(overlay);

        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('open');
            overlay.classList.toggle('active');
        });

        overlay.addEventListener('click', function () {
            sidebar.classList.remove('open');
            overlay.classList.remove('active');
        });
    }

    // ── Auto-dismiss alerts after 5 s ────────────────────────────────────────
    document.querySelectorAll('.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        }, 5000);
    });

    // ── Confirm delete on links with data-confirm ────────────────────────────
    document.querySelectorAll('[data-confirm]').forEach(function (el) {
        el.addEventListener('click', function (e) {
            if (!confirm(el.dataset.confirm || 'Wirklich löschen?')) {
                e.preventDefault();
            }
        });
    });

    // ── Sort direction toggle for table headers ───────────────────────────────
    // Allows clicking a sort header to reverse direction
    document.querySelectorAll('[data-sort]').forEach(function (el) {
        el.style.cursor = 'pointer';
        el.addEventListener('click', function () {
            const url = new URL(window.location.href);
            const current = url.searchParams.get('sortBy');
            const dir = url.searchParams.get('sortDir') || 'asc';
            const col = el.dataset.sort;

            url.searchParams.set('sortBy', col);
            url.searchParams.set('sortDir', current === col && dir === 'asc' ? 'desc' : 'asc');
            url.searchParams.set('page', '1');
            window.location.href = url.toString();
        });
    });

})();
