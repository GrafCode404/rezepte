# Rezepte

Persönliche Rezeptsammlung als Blazor-WebAssembly-App. Laufend über GitHub Pages unter <https://jigby.github.io/rezepte/>.

## Rezepte hinzufügen

Ein neues Rezept ist eine neue `*.md`-Datei im Ordner `wwwroot/recipes/`. Einheitliches Format:

- Erste Zeile: Titel als `# Überschrift`
- Danach Fakten als Bullets `* **Schlüssel:** Wert` (Menge, Zeiten, Temperatur …)
- Zutaten-Tabelle mit Spalten `1x / 2x / 3x`
- `## Anleitungen` mit Abschnitten als `### Unterüberschriften`

Titel und Fakten werden automatisch geparst; PDF-Umbruch-Marker (`<div class="page"/>`) werden beim Rendern ignoriert. Beim Build wird `wwwroot/recipes/index.json` automatisch erzeugt (im Repo ausgeklammert).

## Veröffentlichen

Jeder `git push` auf `main` baut die App und stellt sie über GitHub Pages bereit (Workflow `.github/workflows/deploy.yml`, keine weiteren Schritte nötig). Lokal geht es ebenso mit `git push`.

## Lokal ausführen

```bash
dotnet run
```

Die Seite ist dann unter http://localhost:5005 erreichbar (Port laut `Properties/launchSettings.json`).

## Suche

Volltextsuche über Titel und Inhalt aller Rezepte über das Suchfeld auf der Startseite.