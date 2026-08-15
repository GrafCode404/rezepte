# AGENTS.md – Rezepte

Anweisungen für KI-Agenten, die in diesem Repository (RezepteWeb) Änderungen vornehmen.

## Projekt

Persönliche Rezeptsammlung als **Blazor-WebAssembly-App** (.NET 10), die als **Progressive Web App (PWA)** über **GitHub Pages** ausgeliefert wird.

- Live: `https://grafcode404.github.io/rezepte/`
- GitHub-Repo: `GrafCode404/rezepte` (ehemals `Jigby/rezepte` – Account umbenannt, alte URL leitet per 301 weiter)
- Deploy: automatisch per GitHub Actions bei jedem `push` auf `main` (`.github/workflows/deploy.yml`)

## Architektur / Aufbau

```
Program.cs                  Einstieg, DI-Registrierung (HttpClient, RecipeService)
RezepteWeb.csproj           SDK BlazorWebAssembly, net10.0; MSBuild-Task erzeugt recipes/index.json beim Build
Models/Recipe.cs            Datenmodell (Title, Slug, FileName, Html, Ingredients, Facts)
Services/RecipeService.cs   Lädt Rezepte aus index.json, parst Markdown (Markdig), Slugify, Volltextsuche
Components/
  App.razor / Routes.razor  Router-Setup
  Layout/                   MainLayout, NavMenu (+ CSS)
  Pages/
    Home.razor              Startseite: Suche + Rezeptkarten (Route "/")
    Uebersicht.razor        Offene Anmerkungen über GitHub Issues (Route "/uebersicht")
    Login.razor             GitHub-Token-Eingabe (Route "/zugang")
    Notizen.razor           Allgemeine Anmerkungen (Route "/notizen")
    RecipeDetail.razor      Rezeptdetail, Teilen, QR-Code (Route "/rezepte/{Slug}", "/{Slug}")
    NotFound.razor          404 (Route "/not-found")
wwwroot/
  recipes/*.md               Rezepte als Markdown (Quelle der Daten)
  service-worker.js          PWA-Caching (Stale-while-Revalidate + Netzwerk-first für Navigation)
  pwa-update.js              Update-Banner, erzwingt Reload bei neuer SW
  recipe-notes.js            Anmerkungs-Widget über GitHub-Issues-API
  share.js                   "Link kopieren"-Hilfsfunktion
  index.html, manifest, icons, lib/bootstrap
Properties/launchSettings.json  Lokal auf http://localhost:5005
```

## Konzepte, die wichtig zu verstehen sind

### Rezepte = Markdown-Dateien
Jedes Rezept ist eine `*.md`-Datei in `wwwroot/recipes/`. Kein Datenbank-/CMS-System. Einheitliches Format:
- Zeile 1: Titel als `# Überschrift`
- Danach Fakten als Bullets `* **Schlüssel:** Wert` (Menge, Zeiten, Temperatur …)
- Zutaten-Tabelle mit Spalten `1x / 2x / 3x` (erste Spalte Zutatenname)
- `## Anleitungen` mit `### Unterüberschriften`
- PDF-Umbruch-Marker `<div class="page"/>` wird beim Rendern ignoriert

Beim Build wird `wwwroot/recipes/index.json` per MSBuild-Task (`BuildRecipesIndex`) automatisch aus allen `.md`-Dateien erzeugt. **Diese Datei ist in `.gitignore` ausgeklammert und wird nie committet** – nur die `.md`-Quellen werden versioniert.

### Parsing-Logik (Services/RecipeService.cs)
- `ParseTitle`: erste Zeile mit `# `
- `ParseFacts`: Zeilen mit `* **`
- `ExtractIngredients`: Zutaten-Tabelle (erste Zellenspalte, ohne Menge)
- `Slugify`: Titel → Slug (Umlaute ä/ö/ü/ß → ae/oe/ue/ss, sonst `-`)
- Suche: `SearchAsync` – direkte vs. Teilwort-Treffer (Wortgrenzen beachten)

### Anmerkungen = GitHub Issues (wwwroot/recipe-notes.js)
Das Anmerkungs-Widget nutzt die **GitHub REST API** direkt aus dem Browser (kein Backend):
- `REPO = "GrafCode404/rezepte"`, `ALLOWED_USER = "GrafCode404"`, `LABEL = "anmerkung"`
- Token (feingranular, nur Repo, Issues Read+Write) wird im `localStorage` gespeichert (Key `rezepte.notes.token`)
- Anmerkungen sind offene Issues mit Label `anmerkung`; der Rezept-Slug steckt im Issue-Body als `<!-- slug=... -->`
- Nur `ALLOWED_USER` darf schreiben/löschen; andere sehen nur die Liste
- Beim Umbenennen eines Accounts müssen `REPO` und `ALLOWED_USER` mit umbenannt werden, sonst bricht POST/Löschen wegen Authorization-Verlust beim 301-Redirect

### PWA / Service Worker
- `service-worker.js`: Navigation = Netzwerk-first mit Cache-Fallback; statische Dateien = stale-while-revalidate
- `cacheName` (aktuell `rezepte-v4`) wird bei größeren Änderungen **manuell erhöht**, damit alte Cache-Inhalte auf Clients verworfen werden (siehe `.gitignore`-Hinweis nicht nötig, aber bewusstes Vorgehen)
- `pwa-update.js`: Banner „Aktualisieren" + `SKIP_WAITING`
- Achtung nach Domain-/Repo-Wechsel: alte Service Worker + Caches auf Client-Geräten liefern weiter den alten Stand; Cache-Bump + Client-seitiges Löschen der Site-Daten nötig

## Build / Test / Deploy

- Lokal: `dotnet run` → `http://localhost:5005`
- Build: `dotnet publish RezepteWeb.csproj -c Release -o publish`
- Deploy: `git push` auf `main` → GitHub Actions baut und veröffentlicht (kein manueller Schritt)
- Es gibt keine Test-Suite im Repo

## Arbeitsablauf für Änderungen (wichtig)

1. **Immer zuerst `git pull --rebase`** vor `git push` – der Remote kann durch direkte Edits über die GitHub-Weboberfläche (Rezept-Updates) oder eigene Pushes neue Commits haben. Bei Divergenz rebasen statt mergen, um die Historie linear zu halten.
2. Nach dem Push kurz den GitHub-Actions-Workflow im Auge behalten (`gh run watch`/`gh run list`), der Deploy läuft nicht sofort.
3. Neue Rezepte: nur `wwwroot/recipes/*.md` anlegen; `index.json` NICHT committen (wird beim Build erzeugt).

## Sonstige Hinweise aus der bisherigen Entwicklung

- QR-Code-Teilung je Rezeptseite über `Net.Codecrete.QrCodeGenerator`
- `gh`-CLI kann noch den alten Accountnamen anzeigen; funktioniert aber, gelegentlich `gh auth login` auffrischen
- PWA auf mobilen Geräten ist an die Origin gebunden: Nach einem Domain-Wechsel neu anmelden (Token erneut eintragen) und ggf. App neu installieren
