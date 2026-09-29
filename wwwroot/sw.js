/* Tajiri Sacco service worker.
   Blazor Server needs a live connection, so pages and data always come from the network.
   We only cache the app shell (styles, scripts, fonts, icons) so the app opens fast, and show a friendly
   offline page when there is no connection. Nothing personal (API, member data, photos) is ever cached. */
const VERSION = 'tajiri-v1';
const SHELL = [
  '/offline.html',
  '/css/bootstrap/bootstrap.min.css',
  '/lib/bootstrap-icons/bootstrap-icons.css',
  '/css/milestone.css', '/css/milestone.p2.css', '/css/milestone.p3.css', '/css/milestone.auth.css',
  '/css/milestone.v2.css', '/css/milestone.control.css', '/css/milestone.site.css', '/css/milestone.v6.css', '/css/milestone.v7.css', '/css/milestone.app.css',
  '/js/milestone.js',
  '/img/app/icon-192.png', '/img/app/icon-512.png'
];

self.addEventListener('install', (e) => {
  e.waitUntil(caches.open(VERSION).then((c) => Promise.allSettled(SHELL.map((u) => c.add(u)))).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (e) => {
  e.waitUntil(caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== VERSION).map((k) => caches.delete(k)))).then(() => self.clients.claim()));
});

const NEVER = ['/_blazor', '/api/', '/media/member', '/media/user', '/_framework/blazor.server.js'];

self.addEventListener('fetch', (e) => {
  const req = e.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;
  if (NEVER.some((p) => url.pathname.startsWith(p))) return;

  // Pages: always the network; offline page if that fails.
  if (req.mode === 'navigate') {
    e.respondWith(fetch(req).catch(() => caches.match('/offline.html')));
    return;
  }

  // Static files: serve from cache straight away, refresh in the background.
  if (/\.(css|js|woff2?|png|jpg|jpeg|webp|svg|ico)$/i.test(url.pathname)) {
    e.respondWith(caches.open(VERSION).then(async (c) => {
      const hit = await c.match(req);
      const net = fetch(req).then((res) => { if (res.ok) c.put(req, res.clone()); return res; }).catch(() => hit);
      return hit || net;
    }));
  }
});
