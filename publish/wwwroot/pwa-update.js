(function () {
    "use strict";

    if (!("serviceWorker" in navigator)) {
        return;
    }

    var registration = null;
    var reloadAfterActivation = false;

    // Wenn der neue Service Worker aktiv wird (nach dem Klick auf "Aktualisieren"): neu laden.
    navigator.serviceWorker.addEventListener("controllerchange", function () {
        if (reloadAfterActivation) {
            reloadAfterActivation = false;
            window.location.reload();
        }
    });

    function handleUpdate() {
        if (registration && registration.waiting) {
            reloadAfterActivation = true;
            registration.waiting.postMessage({ type: "SKIP_WAITING" });
        }
    }

    function showUpdateBanner() {
        if (document.getElementById("update-banner")) {
            return;
        }

        var banner = document.createElement("div");
        banner.id = "update-banner";
        banner.style.cssText =
            "position:fixed;bottom:1rem;left:1rem;right:1rem;max-width:28rem;margin:0 auto;" +
            "z-index:1000;background:#1b6ec2;color:#fff;padding:.75rem 1rem;" +
            "border-radius:.75rem;box-shadow:0 .5rem 1.5rem rgba(0,0,0,.35);" +
            "display:flex;align-items:center;gap:.75rem;font-family:system-ui,sans-serif;font-size:14px;";

        var text = document.createElement("span");
        text.style.flex = "1";
        text.textContent = "Neue Version verfügbar.";

        var button = document.createElement("button");
        button.type = "button";
        button.style.cssText =
            "background:#fff;color:#1b6ec2;border:0;padding:.5rem .9rem;border-radius:.5rem;" +
            "font-weight:600;cursor:pointer;";
        button.textContent = "Aktualisieren";
        button.addEventListener("click", handleUpdate);

        var close = document.createElement("button");
        close.type = "button";
        close.textContent = "\u00d7";
        close.setAttribute("aria-label", "Schlie\u00dfen");
        close.style.cssText = "background:none;border:0;color:#fff;cursor:pointer;font-size:1.1rem;";
        close.addEventListener("click", function () { banner.remove(); });

        banner.appendChild(text);
        banner.appendChild(button);
        banner.appendChild(close);
        document.body.appendChild(banner);
    }

    navigator.serviceWorker.ready
        .then(function (reg) {
            registration = reg;

            reg.addEventListener("updatefound", function () {
                var newWorker = reg.installing;
                if (!newWorker) {
                    return;
                }
                newWorker.addEventListener("statechange", function () {
                    // Neue Version heruntergeladen, alte App läuft noch -> Button anzeigen.
                    if (newWorker.state === "installed" && navigator.serviceWorker.controller) {
                        showUpdateBanner();
                    }
                });
            });

            // Im Hintergrund nach Updates suchen.
            setInterval(function () { reg.update().catch(function () { }); }, 60 * 60 * 1000);
        })
        .catch(function () { });

    // Beim Öffnen der App prüfen.
    window.addEventListener("pageshow", function () {
        if (registration) {
            registration.update().catch(function () { });
        }
    });
})();