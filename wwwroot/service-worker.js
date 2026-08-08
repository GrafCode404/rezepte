// build 2026-08-08.3
const cacheName = "rezepte-v2";

// App-Start-Seite für den Offline-Fallback vorcachen.
self.addEventListener("install", (event) => {
    event.waitUntil(caches.open(cacheName).then((cache) => cache.add(self.registration.scope)));
});

// Veraltete Caches entrümpeln.
self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys().then((keys) =>
            Promise.all(keys.filter((k) => k !== cacheName).map((k) => caches.delete(k)))
        )
    );
    self.clients.claim();
});

// Vom Update-Banner angefordert: neuen Service Worker aktivieren.
self.addEventListener("message", (event) => {
    if (event.data && event.data.type === "SKIP_WAITING") {
        self.skipWaiting();
    }
});

self.addEventListener("fetch", (event) => {
    const request = event.request;
    if (request.method !== "GET") {
        return;
    }

    const url = new URL(request.url);
    if (url.origin !== location.origin) {
        return;
    }

    // Seiten-Navigation: Netzwerk zuerst (frische Rezepte), Cache als Fallback.
    if (request.mode === "navigate") {
        event.respondWith(
            fetch(request)
                .then((response) => {
                    if (response.ok) {
                        const copy = response.clone();
                        caches.open(cacheName).then((cache) => cache.put(request, copy));
                    }
                    return response;
                })
                .catch(() =>
                    caches.match(request).then((m) => m || caches.match(self.registration.scope))
                )
        );
        return;
    }

    // Statische Dateien: Cache zuerst, im Hintergrund aktualisieren (stale-while-revalidate).
    event.respondWith(
        caches.match(request).then((cached) => {
            const network = fetch(request)
                .then((response) => {
                    if (response.ok) {
                        const copy = response.clone();
                        caches.open(cacheName).then((cache) => cache.put(request, copy));
                    }
                    return response;
                })
                .catch(() => undefined);
            return cached || network;
        })
    );
});