/* kontakte.js — KontaktDatenbank client-side scripts */

(function () {
    'use strict';

    // ── Darkmode Toggle ──────────────────────────────────────────────────────────
    var themeToggle = document.getElementById('themeToggle');
    var themeIcon   = document.getElementById('themeIcon');

    function applyTheme(dark) {
        if (dark) {
            document.documentElement.setAttribute('data-theme', 'dark');
        } else {
            document.documentElement.removeAttribute('data-theme');
        }
        if (themeIcon) {
            themeIcon.className = dark ? 'bi bi-sun-fill' : 'bi bi-moon-fill';
        }
        if (themeToggle) {
            themeToggle.title = dark ? 'Hellmodus aktivieren' : 'Dunkelmodus aktivieren';
        }
    }

    // Beim Laden: gespeicherte Präferenz anwenden (inline-script hat es schon
    // für den HTML-Root gesetzt, hier Icon + title aktualisieren)
    var savedTheme = localStorage.getItem('mc-theme');
    applyTheme(savedTheme === 'dark');

    if (themeToggle) {
        themeToggle.addEventListener('click', function () {
            var isDark = document.documentElement.getAttribute('data-theme') === 'dark';
            applyTheme(!isDark);
            localStorage.setItem('mc-theme', isDark ? 'light' : 'dark');
        });
    }

    // ── Sidebar toggle (mobile) ──────────────────────────────────────────────────
    var sidebar   = document.getElementById('sidebar');
    var toggleBtn = document.getElementById('sidebarToggle');

    if (sidebar && toggleBtn) {
        var overlay = document.createElement('div');
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

    // ── Auto-dismiss alerts after 5 s ────────────────────────────────────────────
    document.querySelectorAll('.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            var bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        }, 5000);
    });

})();
