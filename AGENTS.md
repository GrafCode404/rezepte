# AGENTS.md – Rezepte

Anweisungen für KI-Agenten, die in diesem Repository (RezepteWeb) Änderungen vornehmen.

## Projekt

Persönliche Rezeptsammlung als **Blazor-WebAssembly-App** (.NET 10), die als **Progressive Web App (PWA)** über **GitHub Pages** ausgeliefert wird.

- Live: `https://grafcode404.github.io/rezepte/`
- **App-Repo**: `GrafCode404/rezepte` (diese Datei liegt hier)
- **Content-Repo**: `GrafCode404/rezepte-content` – enthält die Rezepte als Markdown (`recipes/*.md`) + generiertes `recipes/index.json`
- Deploy: automatisch per GitHub Actions bei jedem `push` auf `main` (`.github/workflows/deploy.yml`)

### Wichtige Architektur-Trennung (seit 2026-08)

Rezept-Daten liegen **nicht mehr** im App-Repo. Sie sind in ein separates Content-Repo ausgelagert:

- Die App liest Rezepte zur Laufzeit von `https://raw.githubusercontent.com/GrafCode404/rezepte-content/main/recipes/index.json`
- Rezept-Änderungen (Anlegen/Bearbeiten) gehen über die **GitHub Contents API** direkt ins Content-Repo – **ohne Rebuild** der App
- `recipes/index.json` im Content-Repo wird beim Speichern (Editor) und zusätzlich per Workflow (`regenerate-index.yml`) regeneriert
- Der frühere MSBuild-Task `BuildRecipesIndex` ist entfernt

## Architektur / Aufbau

```
Program.cs                  Einstieg, DI-Registrierung (HttpClient, RecipeService)
RezepteWeb.csproj           SDK BlazorWebAssembly, net10.0
Models/Recipe.cs            Datenmodell (Title, Slug, FileName, Markdown, Html, Ingredients, Facts)
Models/EditResult.cs        Ergebnis des Editors (RecipeEdit.save)
Services/RecipeService.cs   Lädt Rezepte aus dem Content-Repo (CDN), parst Markdown (Markdig), Slugify, Volltextsuche
Components/
  App.razor / Routes.razor  Router-Setup
  Layout/                   MainLayout, NavMenu (+ CSS)
  Pages/
    Home.razor              Startseite: Suche + Rezeptkarten (Route "/")
    Uebersicht.razor        Offene Anmerkungen über GitHub Issues (Route "/uebersicht")
    Login.razor             GitHub-Token-Eingabe (Route "/zugang")
    Notizen.razor           Allgemeine Anmerkungen (Route "/notizen")
    RecipeDetail.razor      Rezeptdetail, Teilen, QR-Code, Bearbeiten-Button (Route "/rezepte/{Slug}", "/{Slug}")
    EditRecipe.razor        Rezept bearbeiten (Route "/rezepte/{Slug}/edit", "/{Slug}/edit")
    NewRecipe.razor         Neues Rezept anlegen (Route "/neu")
    NotFound.razor          404 (Route "/not-found")
wwwroot/
  service-worker.js          PWA-Caching (Stale-while-Revalidate + Netzwerk-first für Navigation)
  pwa-update.js              Update-Banner, erzwingt Reload bei neuer SW
  recipe-notes.js            Anmerkungs-Widget über GitHub-Issues-API
  recipe-edit-lib.js         Pure Editor-Helfer (slugify, base64, buildIndex) – auch per Node testbar
  recipe-edit.js             Rezept-Editor über GitHub-Contents-API (RecipeEdit.save / isLoggedIn)
  share.js                   "Link kopieren"-Hilfsfunktion
  index.html, manifest, icons, lib/bootstrap
Properties/launchSettings.json  Lokal auf http://localhost:5005
Templates/Rezept-Template.md  Vorlage für neue Rezepte (wird NICHT veröffentlicht)
RezepteWeb.Tests/           xUnit-Tests (RecipeParserTests, RecipeServiceTests)
tests/                      Node-Tests für recipe-edit-lib.js
```

## Konzepte, die wichtig zu verstehen sind

### Rezepte = Markdown-Dateien (im Content-Repo)
Jedes Rezept ist eine `*.md`-Datei in `recipes/` im **Content-Repo** (`GrafCode404/rezepte-content`). Kein Datenbank-/CMS-System. Einheitliches Format:
- Zeile 1: Titel als `# Überschrift`
- Danach Fakten als Bullets `* **Schlüssel:** Wert` (Menge, Zeiten, Temperatur …)
- Zutaten-Tabelle mit Spalten `1x / 2x / 3x` (erste Spalte Zutatenname)
- `## Anleitungen` mit `### Unterüberschriften`
- PDF-Umbruch-Marker `<div class="page"/>` wird beim Rendern ignoriert

`recipes/index.json` im Content-Repo wird automatisch erzeugt (vom Editor beim Speichern und per Workflow `regenerate-index.yml`). Format: `[{"name":"...","content":"..."}]` (kompaktes JSON, keine Leerzeichen).

### Parsing-Logik (Services/RecipeService.cs)
- `ParseTitle`: erste Zeile mit `# `
- `ParseFacts`: Zeilen mit `* **`
- `ExtractIngredients`: Zutaten-Tabelle (erste Zellenspalte, ohne Menge)
- `Slugify`: Titel → Slug (Umlaute ä/ö/ü/ß → ae/oe/ue/ss, sonst `-`)
- Suche: `SearchAsync` – direkte vs. Teilwort-Treffer (Wortgrenzen beachten)

### Anmerkungen = GitHub Issues (wwwroot/recipe-notes.js)
Das Anmerkungs-Widget nutzt die **GitHub REST API** direkt aus dem Browser (kein Backend):
- `REPO = "GrafCode404/rezepte-content"`, `ALLOWED_USER = "GrafCode404"`, `LABEL = "anmerkung"`
- Token (feingranular, Repo `rezepte-content`, Issues + Contents Read+Write) wird im `localStorage` gespeichert (Key `rezepte.notes.token`)
- Anmerkungen sind offene Issues mit Label `anmerkung`; der Rezept-Slug steckt im Issue-Body als `<!-- slug=... -->`
- Nur `ALLOWED_USER` darf schreiben/löschen; andere sehen nur die Liste
- Beim Umbenennen eines Accounts müssen `REPO` und `ALLOWED_USER` mit umbenannt werden, sonst bricht POST/Löschen wegen Authorization-Verlust beim 301-Redirect

### Rezept-Editor = GitHub Contents API (wwwroot/recipe-edit.js)
- `REPO = "GrafCode404/rezepte-content"`, `ALLOWED_USER = "GrafCode404"`
- Nutzt **denselben Token** wie die Anmerkungen (`localStorage`-Key `rezepte.notes.token`)
- Token-Berechtigungen: `contents: read+write` + `issues: read+write` (beide auf `rezepte-content`)
- `RecipeEdit.save({fileName, markdown, title})`: schreibt `.md` + regeneriert `index.json` (beides über Contents API, `sha`-Konfliktprüfung mit einem Retry)
- `RecipeEdit.isLoggedIn()`: prüft Token + User (für bedingte UI wie den Bearbeiten-Button)
- `fileName = null` bei neuem Rezept → Dateiname wird aus dem Titel via Slugify erzeugt

### PWA / Service Worker
- `service-worker.js`: Navigation = Netzwerk-first mit Cache-Fallback; statische Dateien = stale-while-revalidate
- `cacheName` (aktuell `rezepte-v4`) wird bei größeren Änderungen **manuell erhöht**, damit alte Cache-Inhalte auf Clients verworfen werden (siehe `.gitignore`-Hinweis nicht nötig, aber bewusstes Vorgehen)
- `pwa-update.js`: Banner „Aktualisieren" + `SKIP_WAITING`
- Achtung nach Domain-/Repo-Wechsel: alte Service Worker + Caches auf Client-Geräten liefern weiter den alten Stand; Cache-Bump + Client-seitiges Löschen der Site-Daten nötig

## Build / Test / Deploy

- Lokal: `dotnet run` → `http://localhost:5005`
- Build: `dotnet publish RezepteWeb.csproj -c Release -o publish`
- **Tests**: `dotnet test RezepteWeb.Tests/RezepteWeb.Tests.csproj` (xUnit, läuft auch als CI in `.github/workflows/test.yml`)
  - `RecipeParserTests` – reine Parsing-/Slugify-Logik
  - `RecipeServiceTests` – Laden/Parsen/Suche über `index.json` (mit Fake-HttpHandler)
- **JS-Tests**: `node --test tests/*.test.js` (Node-Test-Runner, ohne Abhängigkeiten)
  - testet `wwwroot/recipe-edit-lib.js` (slugify, base64, buildIndex) – die pure Editor-Logik
- Deploy: `git push` auf `main` → GitHub Actions baut und veröffentlicht (kein manueller Schritt)

## Arbeitsablauf für Änderungen (wichtig)

1. **Immer zuerst `git pull --rebase`** vor `git push` – der Remote kann durch direkte Edits über die GitHub-Weboberfläche oder eigene Pushes neue Commits haben. Bei Divergenz rebasen statt mergen, um die Historie linear zu halten.
2. Nach dem Push kurz den GitHub-Actions-Workflow im Auge behalten (`gh run watch`/`gh run list`), der Deploy läuft nicht sofort.
3. **Neue Rezepte anlegen**: entweder über die Webseite (`/neu`) oder direkt als `recipes/*.md` im **Content-Repo** (`rezepte-content`). `recipes/index.json` NICHT manuell im Content-Repo editieren – wird automatisch erzeugt.

## Sonstige Hinweise aus der bisherigen Entwicklung

- QR-Code-Teilung je Rezeptseite über `Net.Codecrete.QrCodeGenerator`
- `gh`-CLI kann noch den alten Accountnamen anzeigen; funktioniert aber, gelegentlich `gh auth login` auffrischen
- PWA auf mobilen Geräten ist an die Origin gebunden: Nach einem Domain-Wechsel neu anmelden (Token erneut eintragen) und ggf. App neu installieren
