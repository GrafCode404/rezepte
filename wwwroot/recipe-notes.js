(function () {
    "use strict";

    var CLIENT_ID = "Ov23ctSOYH1Q2mLHUUdz";
    var ALLOWED_USER = "Jigby";
    var REPO = "Jigby/rezepte";
    var LABEL = "anmerkung";
    var TOKEN_KEY = "rezepte.notes.token";

    function text(v) {
        return document.createTextNode(v);
    }

    function make(tag, className, content) {
        var node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (content !== undefined && content !== null) {
            if (typeof content === "string") {
                node.appendChild(text(content));
            } else {
                node.appendChild(content);
            }
        }
        return node;
    }

    function github(path, options) {
        var token = localStorage.getItem(TOKEN_KEY);
        var headers = {
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28"
        };
        if (token) {
            headers["Authorization"] = "Bearer " + token;
        }
        return fetch("https://api.github.com" + path, Object.assign({ headers: headers }, options))
            .then(function (r) {
                return r.json().catch(function () { return null; }).then(function (body) {
                    return { status: r.status, body: body };
                });
            });
    }

    function getCurrentUser() {
        return github("/user").then(function (r) {
            if (r.status === 200 && r.body && r.body.login) {
                return r.body.login;
            }
            return null;
        });
    }

    function ensureLabel() {
        return github("/repos/" + REPO + "/labels/" + LABEL).then(function (r) {
            if (r.status === 200) {
                return;
            }
            return github("/repos/" + REPO + "/labels", {
                method: "POST",
                body: JSON.stringify({ name: LABEL, color: "d4b08c" })
            });
        });
    }

    function loadNotes(slug) {
        return github("/repos/" + REPO + "/issues?labels=" + LABEL + "&state=all&per_page=100")
            .then(function (r) {
                if (r.status !== 200 || !r.body) {
                    return [];
                }
                var marker = "slug=" + slug;
                var notes = [];
                r.body.forEach(function (issue) {
                    var body = issue.body || "";
                    var m = body.match(/<!--\s*slug=([^\s>]+)\s*-->/);
                    if (m && m[1] === slug) {
                        var clean = body.replace(/<!--\s*slug=[^\s>]+\s*-->\s*/, "");
                        notes.push({
                            number: issue.number,
                            title: issue.title,
                            body: clean,
                            created: issue.created_at,
                            url: issue.html_url
                        });
                    }
                });
                return notes;
            });
    }

    function postNote(slug, title, text) {
        var marker = "<!-- slug=" + slug + " -->";
        return ensureLabel().then(function () {
            return github("/repos/" + REPO + "/issues", {
                method: "POST",
                body: JSON.stringify({
                    title: "Anmerkung: " + title,
                    body: marker + "\n\n" + text,
                    labels: [LABEL]
                })
            });
        });
    }

    function deleteNote(number) {
        return github("/repos/" + REPO + "/issues/" + number, {
            method: "PATCH",
            body: JSON.stringify({ state: "closed" })
        });
    }

    function render(container, slug, title) {
        var token = localStorage.getItem(TOKEN_KEY);
        if (token) {
            getCurrentUser().then(function (login) {
                if (!login) {
                    localStorage.removeItem(TOKEN_KEY);
                    renderAnonymous(container, slug, title);
                    return;
                }
                if (login !== ALLOWED_USER) {
                    renderDenied(container, login);
                    return;
                }
                renderPanel(container, slug, title);
            });
        } else {
            renderAnonymous(container, slug, title);
        }
    }

    function renderAnonymous(container, slug, title) {
        container.innerHTML = "";
        var hint = make("p", "notes-hint", "Anmerkungen sind nur für den Besitzer dieser Website gedacht und haushalten nicht offen für andere. Du musst dich erst mit GitHub anmelden.");
        var btn = make("button", "btn btn-primary", "Mit GitHub anmelden");
        btn.type = "button";
        btn.addEventListener("click", function () {
            startLogin(container, slug, title);
        });
        container.appendChild(hint);
        container.appendChild(btn);
    }

    function renderDenied(container, login) {
        container.innerHTML = "";
        var hint = make("p", "notes-hint", "Dieser GitHub-Account (" + login + ") ist nicht freigegeben. Kommentare darf nur der Besitzer schreiben.");
        var btn = make("button", "btn btn-outline-secondary", "Abmelden");
        btn.type = "button";
        btn.addEventListener("click", function () {
            localStorage.removeItem(TOKEN_KEY);
            renderAnonymous(container);
        });
        container.appendChild(hint);
        container.appendChild(btn);
    }

    function renderPanel(container, slug, title) {
        container.innerHTML = "";
        var logged = make("div", "notes-logged-in");
        var logout = make("button", "btn btn-sm btn-outline-secondary", "Abmelden");
        logout.type = "button";
        logout.addEventListener("click", function () {
            localStorage.removeItem(TOKEN_KEY);
            renderAnonymous(container, slug, title);
        });
        logged.appendChild(make(
            "small",
            "text-muted",
            "Angemeldet als " + ALLOWED_USER
        ));
        logged.appendChild(logout);

        var form = make("form", "notes-form");
        var area = document.createElement("textarea");
        area.className = "form-control notes-textarea";
        area.placeholder = "Falsche Mengenangabe, Backzeit, Tippfehler …";
        area.rows = 3;
        var submit = make("button", "btn btn-primary", "Anmerkung speichern");
        submit.type = "submit";
        var status = make("div", "notes-status");
        form.appendChild(make("label", null, "Neue Anmerkung"));
        form.appendChild(area);
        form.appendChild(submit);
        form.appendChild(status);
        form.addEventListener("submit", function (event) {
            event.preventDefault();
            var value = area.value.trim();
            if (!value) {
                return;
            }
            status.textContent = "Wird gespeichert …";
            submit.disabled = true;
            postNote(slug, title, value).then(function (r) {
                if (r.status === 201 || r.status === 200) {
                    area.value = "";
                    status.textContent = "Gespeichert.";
                    return notesList(list, slug);
                }
                if (r.status === 401) {
                    localStorage.removeItem(TOKEN_KEY);
                    renderAnonymous(container, slug, title);
                    return;
                }
                status.textContent = "Speichern fehlgeschlagen (Status " + r.status + ").";
            }).finally(function () {
                submit.disabled = false;
            });
        });

        var listHeading = make("h3", "notes-heading", "Bisherige Anmerkungen");
        var list = make("div", "notes-list");
        list.appendChild(make("p", "notes-hint", "Lädt …"));

        container.appendChild(logged);
        container.appendChild(form);
        container.appendChild(listHeading);
        container.appendChild(list);

        renderList(list, slug);
    }

    function notesList(list, slug, status) {
        list.innerHTML = "";
        loadNotes(slug).then(function (notes) {
            if (!notes.length) {
                list.appendChild(make("p", "text-muted", "Noch keine Anmerkungen zu diesem Rezept."));
                return;
            }
            notes.forEach(function (note) {
                var item = make("div", "notes-item");
                var meta = make("div", "notes-meta");
                var date = note.created ? new Date(note.created).toLocaleDateString("de-DE") : "";
                meta.appendChild(make("span", "text-muted", "#" + note.number + " · " + date));
                var del = make("button", "btn btn-sm btn-link notes-delete", "Löschen");
                del.type = "button";
                del.addEventListener("click", function () {
                    del.disabled = true;
                    deleteNote(note.number).then(function () {
                        notesList(list, slug);
                    });
                });
                meta.appendChild(del);
                item.appendChild(meta);
                var body = make("div", "notes-body");
                var lines = (note.body || "").split(/\r?\n/);
                lines.forEach(function (line) {
                    body.appendChild(text(line));
                    body.appendChild(document.createElement("br"));
                });
                item.appendChild(body);
                list.appendChild(item);
            });
        });
    }

    function startLogin(container, slug, title) {
        container.innerHTML = "";
        container.appendChild(make("div", "notes-hint", "Verbindung zu GitHub wird aufgebaut …"));

        fetch("https://github.com/login/device/code", {
            method: "POST",
            headers: { "Accept": "application/json", "Content-Type": "application/x-www-form-urlencoded" },
            body: "client_id=" + encodeURIComponent(CLIENT_ID) + "&scope=public_repo"
        }).then(function (r) {
            return r.json().then(function (body) { return { status: r.status, body: body }; });
        }).then(function (result) {
            if (result.status !== 200 || !result.body || !result.body.device_code) {
                container.innerHTML = "";
                container.appendChild(make("p", "notes-error", "Login fehlgeschlagen: " + ((result.body && result.body.error_description) || result.body || "unbekannter Fehler")));
                container.appendChild(backButton(container, slug, title));
                return;
            }
            showDeviceCode(container, slug, title, result.body);
        }).catch(function () {
            container.innerHTML = "";
            container.appendChild(make("p", "notes-error", "Login fehlgeschlagen – der GitHub-Login-Dienst ist hier nicht erreichbar."));
            container.appendChild(backButton(container, slug, title));
        });
    }

    function showDeviceCode(container, slug, title, info) {
        container.innerHTML = "";
        var hint = make("p", "notes-hint", "Code eingeben auf github.com/login/device – dann bestätigst du die Freigabe:");
        var code = make("div", "notes-code", info.user_code);
        var link = make("a", "btn btn-outline-secondary", "Github öffnen");
        link.href = info.verification_uri || "https://github.com/login/device";
        link.target = "_blank";
        link.rel = "noopener";
        container.appendChild(hint);
        container.appendChild(code);
        container.appendChild(link);

        var interval = Math.max(info.interval || 5, 5);
        var attempts = Math.floor((info.expires_in || 900) / interval);
        var status = make("div", "notes-status", "Warte auf Bestätigung …");
        container.appendChild(status);

        function poll(remaining) {
            if (remaining <= 0) {
                status.textContent = "Zeit abgelaufen – bitte erneut beginnen.";
                return;
            }
            fetch("https://github.com/login/oauth/access_token", {
                method: "POST",
                headers: { "Accept": "application/json", "Content-Type": "application/x-www-form-urlencoded" },
                body: "client_id=" + encodeURIComponent(CLIENT_ID) +
                    "&device_code=" + encodeURIComponent(info.device_code) +
                    "&grant_type=urn:ietf:params:oauth:grant-type:device_code"
            }).then(function (r) {
                return r.json().then(function (data) { return { status: r.status, body: data }; });
            }).then(function (result) {
                if (result.body && result.body.access_token) {
                    var token = result.body.access_token;
                    localStorage.setItem(TOKEN_KEY, token);
                    getCurrentUser().then(function (login) {
                        if (login === ALLOWED_USER) {
                            renderPanel(container, slug, title);
                        } else {
                            localStorage.removeItem(TOKEN_KEY);
                            renderDenied(container, login || "unbekannt");
                        }
                    });
                    return;
                }
                if (result.body && result.body.error === "authorization_pending") {
                    setTimeout(function () { poll(remaining - 1); }, interval * 1000);
                    return;
                }
                status.textContent = "Bestätigung fehlgeschlagen: " + (result.body && (result.body.error_description || result.body.error) || ("Status " + result.status));
                container.appendChild(backButton(container, slug, title));
            }).catch(function () {
                status.textContent = "Login-Verbindung fehlgeschlagen.";
                container.appendChild(backButton(container, slug, title));
            });
        }
        poll(180);
    }

    function backButton(container, slug, title) {
        var btn = make("button", "btn btn-outline-secondary", "Zurück");
        btn.type = "button";
        btn.addEventListener("click", function () {
            render(container, slug, title);
        });
        return btn;
    }

    window.RecipeNotes = {
        init: function (slug, title) {
            var container = document.getElementById("recipe-notes");
            if (!container) {
                return;
            }
            if (container.dataset.slug === slug) {
                return;
            }
            container.dataset.slug = slug;
            render(container, slug, title);
        }
    };
})();