(function () {
    "use strict";

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

    function basePath() {
        return location.pathname.indexOf("/rezepte") === 0 ? "/rezepte" : "";
    }

    function loginPath() {
        return basePath() + "/zugang";
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

    function postNote(slug, title, noteText) {
        var marker = "<!-- slug=" + slug + " -->";
        return ensureLabel().then(function () {
            return github("/repos/" + REPO + "/issues", {
                method: "POST",
                body: JSON.stringify({
                    title: "Anmerkung: " + title,
                    body: marker + "\n\n" + noteText,
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
                    localStorage.removeItem(TOKEN_KEY);
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
        var hint = make("p", "notes-hint",
            "Anmerkungen schreibt nur du. Melde dich über den Zugang an, um Anmerkungen zu hinterlassen.");
        var btn = make("a", "btn btn-primary", "Zugang");
        btn.href = loginPath();
        container.appendChild(hint);
        container.appendChild(btn);

        container.appendChild(make("h3", "notes-heading", "Bisherige Anmerkungen"));
        var list = make("div", "notes-list");
        container.appendChild(list);
        notesList(list, slug, false);
    }

    function renderDenied(container, login) {
        container.innerHTML = "";
        container.appendChild(make("p", "notes-hint notes-error",
            "Dieser GitHub-Account (" + login + ") ist nicht freigegeben – nur der Besitzer kann Anmerkungen schreiben."));
        var btn = make("a", "btn btn-primary", "Zugang");
        btn.href = loginPath();
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
        logged.appendChild(make("small", "text-muted", "Verbunden als " + ALLOWED_USER));
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
                    return notesList(list, slug, true);
                }
                if (r.status === 401 || r.status === 403) {
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

        notesList(list, slug, true);
    }

    function notesList(list, slug, canDelete) {
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
                if (canDelete) {
                    var del = make("button", "btn btn-sm btn-link notes-delete", "Löschen");
                    del.type = "button";
                    del.addEventListener("click", function () {
                        del.disabled = true;
                        deleteNote(note.number).then(function () {
                            notesList(list, slug, true);
                        });
                    });
                    meta.appendChild(del);
                }
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

    function renderLoginPage(container) {
        var token = localStorage.getItem(TOKEN_KEY);
        if (token) {
            getCurrentUser().then(function (login) {
                if (login === ALLOWED_USER) {
                    renderLoginConnected(container);
                    return;
                }
                localStorage.removeItem(TOKEN_KEY);
                if (!login) {
                    renderLoginForm(container, true);
                } else {
                    renderLoginDenied(container, login);
                }
            });
        } else {
            renderLoginForm(container, false);
        }
    }

    function renderLoginForm(container, invalid) {
        container.innerHTML = "";

        var status = make("div", "notes-status");
        if (invalid) {
            status.textContent = "Das gespeicherte Token ist ungültig oder abgelaufen – neues Token eingeben.";
            status.className += " notes-error";
        }
        var intro = make("p", "notes-hint",
            "Token anlegen: GitHub → Settings → Developer settings → Personal access tokens → " +
            "Fine-grained (nur Repo „rezepte\", Permission „Issues: Read and write\").");
        var input = document.createElement("input");
        input.type = "password";
        input.className = "form-control";
        input.autocomplete = "off";
        input.placeholder = "GitHub-Token (ghp_… oder github_pat_…)";
        var submit = make("button", "btn btn-primary", "Verbinden");
        submit.type = "submit";
        var row = make("div", "notes-token-row");

        var form = make("form", "notes-token-form");
        form.appendChild(status);
        form.appendChild(intro);
        row.appendChild(input);
        row.appendChild(submit);
        form.appendChild(row);

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            var token = input.value.trim();
            if (!token) {
                return;
            }
            input.disabled = true;
            submit.disabled = true;
            status.textContent = "Prüfe Token …";
            localStorage.setItem(TOKEN_KEY, token);
            getCurrentUser().then(function (login) {
                if (login === ALLOWED_USER) {
                    renderLoginConnected(container);
                    return;
                }
                localStorage.removeItem(TOKEN_KEY);
                if (!login) {
                    status.textContent = "Token ungültig – bitte prüfen und erneut versuchen.";
                    status.className += " notes-error";
                } else {
                    renderLoginDenied(container, login);
                    return;
                }
                input.disabled = false;
                submit.disabled = false;
            }).catch(function () {
                localStorage.removeItem(TOKEN_KEY);
                status.textContent = "Verbindung zu GitHub fehlgeschlagen.";
                status.className += " notes-error";
                input.disabled = false;
                submit.disabled = false;
            });
        });

        container.appendChild(make("h2", null, "Anmelden"));
        container.appendChild(form);
    }

    function renderLoginConnected(container) {
        container.innerHTML = "";
        var ok = make("p", "notes-hint", "Verbunden als " + ALLOWED_USER + " – du kannst jetzt auf den Rezeptseiten Anmerkungen schreiben.");
        var logout = make("button", "btn btn-outline-secondary", "Abmelden");
        logout.type = "button";
        logout.addEventListener("click", function () {
            localStorage.removeItem(TOKEN_KEY);
            renderLoginPage(container);
        });
        var back = make("a", "btn btn-primary", "Zu den Rezepten");
        back.href = basePath() + "/";
        container.appendChild(ok);
        container.appendChild(logout);
        container.appendChild(back);
    }

    function renderLoginDenied(container, login) {
        container.innerHTML = "";
        container.appendChild(make("p", "notes-hint notes-error",
            "Dieser GitHub-Account (" + login + ") ist nicht freigegeben – nur der Besitzer kann Anmerkungen schreiben."));
        var logout = make("button", "btn btn-outline-secondary", "Abmelden");
        logout.type = "button";
        logout.addEventListener("click", function () {
            localStorage.removeItem(TOKEN_KEY);
            renderLoginPage(container);
        });
        container.appendChild(logout);
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
        },
        initLogin: function () {
            var container = document.getElementById("login-container");
            if (container) {
                renderLoginPage(container);
            }
        }
    };
})();