/* Installable app: service worker, "Install app" buttons, iPhone instructions, standalone tweaks. */
(function () {
  var deferred = null;
  var ms = (window.ms = window.ms || {});

  function standalone() {
    return (window.matchMedia && matchMedia('(display-mode: standalone)').matches) || window.navigator.standalone === true ||
      document.referrer.indexOf('android-app://') === 0;
  }
  function ios() { return /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1); }
  function iosSafari() { return ios() && /safari/i.test(navigator.userAgent) && !/crios|fxios|edgios/i.test(navigator.userAgent); }

  function update() {
    var on = standalone();
    document.documentElement.classList.toggle('pwa-standalone', on);
    var show = !on && (!!deferred || iosSafari());
    document.querySelectorAll('[data-pwa-install]').forEach(function (b) { if (b.hidden === show) b.hidden = !show; });
  }

  function iosSheet() {
    if (document.getElementById('pwa-ios')) return;
    var d = document.createElement('div');
    d.id = 'pwa-ios';
    d.className = 'pwa-sheet-bg';
    d.innerHTML =
      '<div class="pwa-sheet" role="dialog" aria-modal="true" aria-labelledby="pwa-ios-t">' +
      '<div class="pwa-grab"></div>' +
      '<img src="img/app/icon-192.png" alt="" />' +
      '<h3 id="pwa-ios-t">Install on your iPhone</h3>' +
      '<ol><li>Tap <b>Share</b> <i class="bi bi-box-arrow-up"></i> at the bottom of Safari</li>' +
      '<li>Scroll and tap <b>Add to Home Screen</b> <i class="bi bi-plus-square"></i></li>' +
      '<li>Tap <b>Add</b>. The app appears on your home screen.</li></ol>' +
      '<button type="button" class="ms-btn primary" data-close>Got it</button></div>';
    d.addEventListener('click', function (e) { if (e.target === d || e.target.closest('[data-close]')) d.remove(); });
    document.body.appendChild(d);
  }

  window.addEventListener('beforeinstallprompt', function (e) { e.preventDefault(); deferred = e; update(); });
  window.addEventListener('appinstalled', function () {
    deferred = null; update();
    if (ms.toast) ms.toast('success', 'App installed', 'Open it from your home screen or app list.');
  });

  document.addEventListener('click', function (e) {
    var b = e.target.closest && e.target.closest('[data-pwa-install]');
    if (!b) return;
    e.preventDefault();
    if (deferred) {
      deferred.prompt();
      deferred.userChoice.finally(function () { deferred = null; update(); });
    } else if (iosSafari()) {
      iosSheet();
    }
  });

  // Blazor re-renders buttons; keep their visibility right.
  var t = null;
  new MutationObserver(function () { clearTimeout(t); t = setTimeout(update, 60); })
    .observe(document.documentElement, { childList: true, subtree: true });
  document.addEventListener('DOMContentLoaded', update);
  update();

  ms.pwa = { installable: function () { return !!deferred || iosSafari(); }, standalone: standalone };

  if ('serviceWorker' in navigator && (location.protocol === 'https:' || location.hostname === 'localhost')) {
    window.addEventListener('load', function () { navigator.serviceWorker.register('/sw.js').catch(function () { }); });
  }
})();
