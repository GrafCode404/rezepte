# Rezepte

Persönliche Rezeptsammlung als Blazor-WebAssembly-App. Laufend über GitHub Pages unter <https://grafcode404.github.io/rezepte/>.

## Rezepte verwalten

Die Rezepte liegen als Markdown-Dateien im separaten Repo [`GrafCode404/rezepte-content`](https://github.com/GrafCode404/rezepte-content). Sie werden zur Laufzeit von dort geladen – Rezept-Änderungen erfordern keinen Rebuild der App.

Neue Rezepte lassen sich direkt auf der Webseite anlegen (`/neu`) und bearbeiten. Zum Speichern ist ein GitHub-Login über `Zugang` nötig.

Rezept-Format:

- Erste Zeile: Titel als `# Überschrift`
- Danach Fakten als Bullets `* **Schlüssel:** Wert` (Menge, Zeiten, Temperatur …)
- Zutaten-Tabelle mit Spalten `1x / 2x / 3x`
- `## Anleitungen` mit Abschnitten als `### Unterüberschriften`

Titel und Fakten werden automatisch geparst; PDF-Umbruch-Marker (`<div class="page"/>`) werden beim Rendern ignoriert. `index.json` im Content-Repo wird beim Speichern und per Workflow automatisch erzeugt.

## Veröffentlichen

Jeder `git push` auf `main` baut die App und stellt sie über GitHub Pages bereit (Workflow `.github/workflows/deploy.yml`, keine weiteren Schritte nötig). Lokal geht es ebenso mit `git push`.

## Lokal ausführen

```bash
dotnet run
```

Die Seite ist dann unter http://localhost:5005 erreichbar (Port laut `Properties/launchSettings.json`).

## Suche

Volltextsuche über Titel und Inhalt aller Rezepte über das Suchfeld auf der Startseite.

## Als App installieren

Die Seite ist eine Progressive Web App (Manifest + Service Worker in `wwwroot/`). Auf Android/iOS lässt sie sich über „Installieren" bzw. „Zum Home-Bildschirm hinzufügen" wie eine App installieren und funktioniert teilweise offline.

Hinweis zum Cache: Wenn eine neue App-Version bereitsteht, erscheint unten ein Banner mit „Aktualisieren" – der Klick aktiviert die neue Version und startet die App neu. Die Cache-Version (`cacheName`) in `wwwroot/service-worker.js` wird bei großen Änderungen erhöht, damit alte Cache-Inhalte verworfen werden.

## QR-Code

Jede Rezeptseite zeigt einen QR-Code mit dem Link zum Rezept (zum Teilen auf andere Geräte).
