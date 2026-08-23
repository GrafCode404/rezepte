(function (global) {
    "use strict";

    function toBase64(str) {
        var bytes = new TextEncoder().encode(str);
        var bin = "";
        var chunk = 0x8000;
        for (var i = 0; i < bytes.length; i += chunk) {
            bin += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
        }
        return btoa(bin);
    }

    function fromBase64(b64) {
        var bin = atob(b64);
        var bytes = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) {
            bytes[i] = bin.charCodeAt(i);
        }
        return new TextDecoder().decode(bytes);
    }

    function slugify(title) {
        return title
            .toLowerCase()
            .replace(/\u00e4/g, "ae")
            .replace(/\u00f6/g, "oe")
            .replace(/\u00fc/g, "ue")
            .replace(/\u00df/g, "ss")
            .replace(/[^a-z0-9]+/g, "-")
            .replace(/^-+|-+$/g, "");
    }

    // Ersetzt in `entries` den Eintrag mit `fileName` (oder hängt ihn an).
    function buildIndex(entries, fileName, markdown) {
        var next = [];
        var found = false;
        (entries || []).forEach(function (e) {
            if (e && e.name === fileName) {
                next.push({ name: fileName, content: markdown });
                found = true;
            } else {
                next.push(e);
            }
        });
        if (!found) {
            next.push({ name: fileName, content: markdown });
        }
        return next;
    }

    var DRAFT_KEY = "rezepte.newrecipe.draft";

    function getDraft() {
        try { return localStorage.getItem(DRAFT_KEY) || ""; } catch (e) { return ""; }
    }

    function setDraft(text) {
        try { localStorage.setItem(DRAFT_KEY, text); } catch (e) { }
    }

    function clearDraft() {
        try { localStorage.removeItem(DRAFT_KEY); } catch (e) { }
    }

    var api = {
        toBase64: toBase64,
        fromBase64: fromBase64,
        slugify: slugify,
        buildIndex: buildIndex,
        getDraft: getDraft,
        setDraft: setDraft,
        clearDraft: clearDraft
    };

    if (typeof module !== "undefined" && module.exports) {
        module.exports = api;
    } else {
        global.RecipeEditLib = api;
    }
})(typeof window !== "undefined" ? window : globalThis);
