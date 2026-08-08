# Rezepte

Persönliche Rezeptsammlung als Blazor-Web-App (.NET 10, Server-Side Rendering).

## Rezepte hinzufügen

Ein neues Rezept ist eine neue `*.md`-Datei im Ordner `Content/Recipes/`. Einheitliches Format:

- Erste Zeile: Titel als `# Überschrift`
- Danach Fakten als Bullets `* **Schlüssel:** Wert` (Menge, Zeiten, Temperatur …)
- Zutaten-Tabelle mit Spalten `1x / 2x / 3x`
- `## Anleitungen` mit Abschnitten als `### Unterüberschriften`

Titel und Fakten werden automatisch geparst; PDF-Umbruch-Marker (`<div class="page"/>`) werden beim Rendern ignoriert.

## Ausführen

```bash
dotnet run
```

Die Seite ist dann unter http://localhost:5005 erreichbar (Port laut `Properties/launchSettings.json`).

## Suche

Volltextsuche über Titel und Inhalt aller Rezepte über das Suchfeld auf der Startseite.

## Konfiguration

Pfad zum Rezeptordner in `appsettings.json` (`Recipes:Path`, Standard: `Content/Recipes`).
