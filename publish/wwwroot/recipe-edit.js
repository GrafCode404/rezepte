(function () {
    "use strict";

    var REPO = "GrafCode404/rezepte-content";
    var ALLOWED_USER = "GrafCode404";
    var TOKEN_KEY = "rezepte.notes.token";
    var BASE = "https://api.github.com/repos/" + REPO + "/contents/";
    var Lib = globalThis.RecipeEditLib;

    function token() {
        return localStorage.getItem(TOKEN_KEY);
    }

    function api(path, options) {
        var headers = {
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28"
        };
        var t = token();
        if (t) {
            headers["Authorization"] = "Bearer " + t;
        }
        return fetch(BASE + path, Object.assign({ headers: headers, cache: "no-store" }, options))
            .then(function (r) {
                return r.json().catch(function () { return null; }).then(function (body) {
                    return { status: r.status, body: body };
                });
            });
    }

    function currentUser() {
        var headers = {
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28"
        };
        var t = token();
        if (t) {
            headers["Authorization"] = "Bearer " + t;
        }
        return fetch("https://api.github.com/user", { headers: headers, cache: "no-store" })
            .then(function (r) {
                return r.json().catch(function () { return null; }).then(function (body) {
                    return { status: r.status, body: body };
                });
            });
    }

    // Liefert { sha, entries } für recipes/index.json (entries = Array oder []).
    function loadIndex() {
        return api("recipes/index.json").then(function (r) {
            if (r.status === 404) {
                return { sha: null, entries: [] };
            }
            if (r.status !== 200 || !r.body || !r.body.content) {
                throw new Error("index.json konnte nicht geladen werden (Status " + r.status + ").");
            }
            var text = Lib.fromBase64(r.body.content.replace(/\n/g, ""));
            var entries;
            try {
                entries = JSON.parse(text);
            } catch (e) {
                entries = [];
            }
            return { sha: r.body.sha, entries: entries };
        });
    }

    // Liefert die sha einer Datei oder null, wenn sie nicht existiert.
    function fileSha(path) {
        return api(path).then(function (r) {
            if (r.status === 404) {
                return null;
            }
            if (r.status !== 200 || !r.body) {
                throw new Error("Datei konnte nicht gelesen werden (Status " + r.status + ").");
            }
            return r.body.sha;
        });
    }

    // Schreibt eine Datei (base64). Bei sha-Konflikt wirft ein Fehler-Objekt mit .conflict = true.
    function putFile(path, text, sha, message) {
        var body = {
            message: message,
            content: Lib.toBase64(text)
        };
        if (sha) {
            body.sha = sha;
        }
        return api(path, {
            method: "PUT",
            body: JSON.stringify(body)
        }).then(function (r) {
            if (r.status === 200 || r.status === 201) {
                return { ok: true };
            }
            var err = new Error("Speichern fehlgeschlagen (Status " + r.status + ").");
            err.conflict = r.status === 409 || r.status === 422;
            throw err;
        });
    }

    // Speichert ein Rezept (neu oder bearbeitet). fileName = null bei neuem Rezept.
    function save(args) {
        var fileName = args.fileName || null;
        var markdown = args.markdown || "";
        var title = args.title || "Rezept";

        if (!token()) {
            return Promise.resolve({ ok: false, error: "Nicht angemeldet – bitte unter „Zugang“ verbinden." });
        }

        return currentUser().then(function (u) {
            if (u.status !== 200 || !u.body || !u.body.login) {
                return { ok: false, error: "Token ungültig oder abgelaufen." };
            }
            if (u.body.login !== ALLOWED_USER) {
                return { ok: false, error: "Dieser Account (" + u.body.login + ") ist nicht freigegeben." };
            }

            var finalName = fileName || Lib.slugify(title) + ".md";
            var mdPath = "recipes/" + finalName;
            var mdMessage = fileName ? ("Update " + title) : ("Add " + title);

            return loadIndex().then(function (idx) {
                return fileSha(mdPath).then(function (mdSha) {
                    var next = Lib.buildIndex(idx.entries, finalName, markdown);
                    var newIndex = JSON.stringify(next);

                    return putFile(mdPath, markdown, mdSha, mdMessage).then(function () {
                        return writeIndex(idx, newIndex, mdMessage).then(function () {
                            return { ok: true, fileName: finalName };
                        });
                    });
                });
            });
        }).catch(function (e) {
            return { ok: false, error: e && e.message ? e.message : "Unbekannter Fehler." };
        });
    }

    // Schreibt index.json mit einem Retry bei sha-Konflikt (parallel laufender Workflow).
    function writeIndex(idx, newIndex, message) {
        return putFile("recipes/index.json", newIndex, idx.sha, "Update index (" + message + ")")
            .catch(function (e) {
                if (!e.conflict) {
                    throw e;
                }
                return loadIndex().then(function (fresh) {
                    return putFile("recipes/index.json", newIndex, fresh.sha, "Update index (" + message + ")");
                });
            });
    }

    window.RecipeEdit = {
        save: save,
        isLoggedIn: function () {
            if (!token()) {
                return Promise.resolve(false);
            }
            return currentUser().then(function (u) {
                return u.status === 200 && u.body && u.body.login === ALLOWED_USER;
            }).catch(function () { return false; });
        }
    };
})();
