# Erweiterungsplan: Dynamische Rezeptverwaltung

> Stand: 2026-08-15
> Status: **Entwurf** – zur Umsetzung in Phasen
> Basis: RezepteWeb (Blazor-WebAssembly-PWA auf GitHub Pages), Git-basierter Markdown-Storage

---

## 1. Ziele & Anforderungen

| Anforderung | Detail |
|---|---|
| Rezepte **direkt auf der Webseite editierbar** | Anlegen, Bearbeiten, Löschen, Kommentieren |
| **Lesen ohne Login** | Für alle Besucher |
| **Schreiben nur nach sicherem Login** | Edit, Delete, Comment |
| **Datenformat: Markdown** | Jederzeit ohne Webseite nutzbar (Dateien liegen im Repo) |
| **Storage: Git** | Revisionen, Wiederherstellung |
| **Kostenlos / sehr günstig** | Hosting + Storage |
| **Daten-Sicherung** | Gegen versehentliches Löschen, Revisionen halten |
| **Softdelete + Restore** | Löschen = ausblenden, nicht entfernen; Wiederherstellbar |
| **Download** | Rezept als Markdown oder PDF |
| **Kategorien & erweiterte Filter** | Brot + andere Kochrezepte, Suche/Filter ausbauen |
| **Keine Bilder, nur Text** | Sehr geringe Datenmenge |
| **Zutaten-Skalierung (Editor)** | Mehrere Mengen-Varianten (1x / 2x / 3x) direkt im Rezept hinterlegen, Beträge automatisch berechnen |
| **Zutaten-Skalierung (Anzeige)** | Menge im Rezept live skalieren (z. B. auf 0,5x / 2x / 2,5x) |

---

## 2. Recherche-Ergebnis: Proton (nicht geeignet)

Geprüft, ob Proton einen kostenlosen Speicher mit API bietet:

- **Proton Drive Free**: 2 GB (bis 5 GB durch Setup-Aktionen) **kostenlos**, end-to-end-verschlüsselt.
- **Aber**: kein Git; **keine öffentliche Drittanbieter-API** (nur SDK für eigene Proton-Clients, "not yet officially supported for use by third parties"); **Dateiversionsverlauf nur auf kostenpflichtigen Plänen**.
- Verschlüsselung macht direkte HTTP-API-Zugriffe aus einer Web-App praktisch unmöglich.

**Fazit: Proton ist für dieses Vorhaben ungeeignet.** GitHub erfüllt alle Anforderungen (Git, Revisionen, Markdown, kostenlos) bereits besser.

Strato Cloud wäre technisch möglich (SFTP/git), bräuchte aber eigenes Backend-Hosting und ist nicht nötig.

---

## 3. Architektur-Entscheidungen

Auf Basis der Ziele und der bestehenden Codebasis:

### 3.1 Storage & Hosting: **GitHub beibehalten + API-Write**

- **Hosting bleibt GitHub Pages** (kostenlos). Die Seite ist bereits eine statische Blazor-WASM-PWA.
- **Storage bleibt ein Git-Repo** (`GrafCode404/rezepte`) mit `wwwroot/recipes/*.md`.
- **Neue Schreibfunktionen** (Anlegen/Bearbeiten/Löschen) laufen über die **GitHub Content API** direkt aus der App – genau wie die bestehenden Anmerkungen bereits über die **GitHub Issues API** laufen (`recipe-notes.js`).
- **Vorteile**: kein neuer Server, keine Kosten, Markdown-Dateien bleiben Source of Truth, jede Änderung = Git-Commit = Revision.

### 3.2 Login: **Personal Access Token (bestehendes Modell erweitert)**

- Beibehaltung des Token-Modells aus `recipe-notes.js` (Token in `localStorage`).
- Der Token braucht künftig mehr Permissions: **Contents: Read+Write** (für Rezept-Edit) zusätzlich zu **Issues: Read+Write** (für Kommentare).
- **Wichtig (Security-Limit):** GitHub-Auth via PAT + Web = es gibt **keinen** serverseitigen Schutz; der Token liegt im Browser. Sicherheit beruht darauf, dass der Token nur dem Besitzer gehört (GitHub-Login-Check wie bisher `ALLOWED_USER`). Für einen Einzelbenutzer akzeptabel, aber **nicht** für mehrere Benutzer mit verliehenen Tokens.
- Lesen bleibt ohne Token möglich.

---

## 4. Datenmodell-Erweiterung

### 4.1 Kategorien & Metadaten über YAML-Frontmatter

Bestehende Rezepte nutzen nur Markdown. Für Kategorien, Tags, Koch-/Backtyp, Portionen etc. bietet sich **YAML-Frontmatter** am Dateianfang an (standardisiert, von vielen Tools lesbar, bleibt Text):

```markdown
---
title: Hefezopf (Osterzopf)
category: Brot
tags: [hefe, süß, osterzopf]
servings: 1
---
```

- `ParseTitle` / `ParseFacts` in `RecipeService.cs` um Frontmatter ergänzen.
- Alte Dateien bleiben kompatibel (Frontmatter optional, Fallback auf bisherige Parsing-Logik).
- Neue Model-Felder: `Category`, `Tags`, `Servings` (in `Models/Recipe.cs`).

### 4.2 Softdelete-Ansatz

Kein echtes Löschen der Datei (das würde Git-Historie nur über Commits behalten). Stattdessen:

- **Datei im Repo behalten**, im Frontmatter `deleted: true` setzen.
- Die App **filtert** `deleted: true` beim Laden aus (`RecipeService`).
- Der Dateiname bleibt stabil → einfach zu restoren (Flag entfernen).
- **Git bleibt zusätzliche Sicherungsebene** (frühere Versionen jederzeit per Commit abrufbar).
- Alternativ/ergänzend: Löschen = Umbenennen nach `wwwroot/recipes/.deleted/<name>.md` (Datei aus dem sichtbaren Ordner, aber im Repo/Backup erhalten).

**Empfehlung: Frontmatter-Flag `deleted: true`** – am einfachsten, Datei bleibt am Platz, Restore trivial.

---

## 5. Phasenplan

### Phase 1 – Grundlagen: Schreibzugriff über GitHub Content API – ✅ UMGESETZT

**Ziel:** Rezepte direkt aus der App anlegen und bearbeiten können.

- [x] GitHub-Token-Berechtigung erweitert (Contents Read+Write) + Zugang-Seite (`Login.razor`) angepasst
- [x] JS-Module `recipe-edit.js` + `recipe-edit-lib.js` (Contents API gegen `GrafCode404/rezepte-content`, sha-Konfliktprüfung, index.json-Regeneration)
- [x] Blazor-Komponenten: Bearbeiten-Button auf `RecipeDetail.razor`, Editor-Seite (`EditRecipe.razor`, mit Live-Vorschau), „Neues Rezept"-Seite (`NewRecipe.razor`, mit lokalem Entwurf + Login-Gate)
- [x] `RecipeService.Reset()` nach Bearbeitung
- [x] Rezepte in eigenes Repo `GrafCode404/rezepte-content` ausgelagert – die App lädt `index.json` zur Laufzeit (CDN mit jsDelivr-Fallback), Rezept-Änderungen erfordern keinen Rebuild
- [x] Unit-Tests (xUnit, C#) + JS-Tests (Node) + CI-Workflow
- [x] Softdelete-Vorarbeit: Anmerkungen (Issues) ebenfalls nach `rezepte-content` umgezogen

### Phase 2 – Softdelete, Restore & Revisionen

**Ziel:** Löschen ohne Datenverlust, Wiederherstellung.

- [ ] Löschen = Frontmatter `deleted: true` setzen (Commit über Content API)
- [ ] Ausblenden in `RecipeService.LoadAsync` (filtert gelöschte)
- [ ] Verwaltungskonzept für gelöschte Rezepte (Liste auf "Zugang"/Admin-Seite, Restore-Button)
- [ ] Rev.-Anzeige optional: Liste der letzten Commits einer Datei (über GitHub API) zur Info

### Phase 3 – Download (Markdown & PDF)

**Ziel:** Rezepte exportierbar.

- [ ] Markdown-Download: einfach (Dateiinhalt als Blob aus `index.json`/Content-API)
- [ ] PDF-Download: Client-seitige Generierung, z. B. `jsPDF` oder Druck-Ansicht (Browser-Print → "Als PDF speichern") – **kostenlos, keine Server nötig**. Empfehlung: dedizierte Druck-Ansicht + Print-CSS, damit nicht extra eine PDF-Bibliothek eingebunden werden muss.

### Phase 4 – Kategorien & Filter

**Ziel:** Brot + andere Rezepte, erweiterte Filter.

- [ ] Frontmatter einführen (Kategorie, Tags)
- [ ] Migration bestehender 10 Rezepte auf Frontmatter
- [ ] `Home.razor`: Filterleiste (Kategorie, Tag, ggf. kombinierte Volltextsuche)
- [ ] `RecipeService.SearchAsync` erweitern (nach Kategorie/Tag filtern)
- [ ] Kategorie-Badge auf Karten/Detail

### Phase 5 – Kommentare erweitern & Abschluss

**Ziel:** Kommentare zu Rezepten + Gesamtpolish.

- [ ] Bestehende Anmerkungen (Issues) zu Rezepten verknüpfen/erweitern (z. B. pro Rezept, nicht nur global)
- [ ] UX-Polish, Fehlerbehandlung, Ladezustände
- [ ] PWA-Cache-Bump (`cacheName` in `service-worker.js` erhöhen) bei größeren Releases
- [ ] Tests/Doku aktualisieren

### Phase 6 – Zutaten-Skalierung (automatisch)

**Ziel:** Mengen automatisch skalieren – beim Erfassen und beim Anzeigen.

- [ ] **Editor-seitig (Variante A):** Beim Anlegen/Bearbeiten aus einer Basis-Menge (1x) die weiteren Spalten (2x, 3x, …) automatisch berechnen, damit mehrere Varianten direkt im Rezept hinterlegt werden. Mengen (Zahl + Einheit) aus der Zutaten-Tabelle erkennen und umrechnen.
- [ ] **Anzeige-seitig (Variante B):** Im Rezept (`RecipeDetail`) einen Skalierungs-Regler/Faktor (z. B. 0,5x / 1x / 2x / 2,5x), der die Zutatenmengen in der Tabelle live umrechnet – ohne das Rezept zu verändern.
- [ ] Parser-Erweiterung: Zutaten-Mengen parsen und als skalierbare Einheit (Menge + Einheit) bereitstellen.

### Phase 7 – Skalierung: Metadaten-Index + Lazy-Load

**Ziel:** Auch bei vielen Rezepten (200+) schnellen Start und schlanke Daten.

- [ ] `index.json` auf **Metadaten** reduzieren (Titel, Slug, Dateiname, Fakten, Zutaten, Kategorie, Tags, …) statt Vollinhalt.
- [ ] Workflow (`generate_index.py`) extrahiert die Metadaten aus Frontmatter/Markdown statt den Vollinhalt zu speichern.
- [ ] Volles Markdown **lazy laden**: `RecipeDetail` holt die einzelne `.md` vom CDN (raw/jsDelivr) und rendert sie on demand.
- [ ] `RecipeService` hält nur Metadaten im Speicher; Markdown→HTML-Rendering nur noch für das aktuell angezeigte Rezept.

### Phase 8 – Erweiterte Filter & Suche

**Ziel:** Filterbare Suche über erweiterte Metadaten.

- [ ] Frontmatter-Felder definieren (Kategorie, Tags, Zubereitungszeit, Backtemperatur, Portionen, vegetarisch/vegan, …).
- [ ] Filter-UI auf `Home.razor` (Kategorie, Tags, Zeiten, …) – kombinierbar mit der Volltextsuche.
- [ ] `RecipeService.SearchAsync` / Filterlogik um die neuen Felder erweitern.
- [ ] Optional: Fuzzy-/Relevanzsuche client-seitig über Lunr.js / FlexSearch / Fuse.js.

> **Hinweis – Kein Backend nötig:** Alle Filter und Suchen laufen client-seitig über den Metadaten-Index. Das skaliert auf tausende Rezepte. Ein Backend lohnt sich erst bei Mehrbenutzer-Echtzeit-Kollaboration oder 100k+ Rezepten – für diese persönliche Sammlung bleibt es bei Markdown + Git (statisch, kostenlos, portabel).

---

## 6. Technische Umsetzungshinweise

### 6.1 GitHub Content API (Kern)

```
# Datei lesen
GET  /repos/GrafCode404/rezepte/contents/wwwroot/recipes/{file}.md

# Datei schreiben/ändern (sha = aktueller Blob für Konfliktprüfung)
PUT  /repos/GrafCode404/rezepte/contents/wwwroot/recipes/{file}.md
  body: { message, content: <base64>, sha }
  header: Authorization: Bearer <token>

# Neue Datei
PUT  /repos/GrafCode404/rezepte/contents/wwwroot/recipes/{file}.md
  body: { message, content: <base64> }

# Commits einer Datei (Revisionen)
GET  /repos/GrafCode404/rezepte/commits?path=wwwroot/recipes/{file}.md
```

> Hinweis: Nach jedem Push läuft der GitHub-Pages-Deploy-Workflow automatisch. Edits via Content API lösen **keinen** Workflow aus (kein `git push` im klassischen Sinn). Lösung: Entweder Workflow zusätzlich per `repository_dispatch`/Workflow-Trigger anstoßen, oder die Seite liest Rezepte nicht nur aus `index.json` (Build-Artefakt), sondern zusätzlich per Content API live nach. → **Design-Entscheidung in Phase 1 klären** (siehe Abschnitt 7).

### 6.2 index.json vs. Live-Abruf – wichtige Design-Frage

Aktuell baut `BuildRecipesIndex` beim Build `index.json` aus allen `.md`. Damit werden **nur Build-Stände** ausgeliefert – Änderungen über die Content API sind erst nach dem nächsten Deploy sichtbar. Zwei Optionen:

- **A) Deploy bei jeder Änderung triggern:** Nach erfolgreichem Edit per GitHub `workflow_dispatch`-API den Pages-Build anstoßen. Einfach, aber Edits sind erst nach Build live.
- **B) Live-Lesen über Content API:** Die App holt Rezept-Markdown direkt per Content API (mit Cache), `index.json` nur als Offline-Fallback. Edits sofort live, aber mehr API-Aufrufe + Token nötig zum Lesen (privates Repo).
  - **Lösung für öffentliches Lesen:** Repo public machen → Content API ohne Token für Lesezugriff. Schreiben weiter nur mit Token.

**Empfehlung: Option B** (Live-Lesen für sofortige Sichtbarkeit, `index.json` als Offline-Fallback, Schreiben per Token).

> Hinweis: Das Repo `GrafCode404/rezepte` ist **bereits public** (Stand 2026-08-15). Öffentliches Lesen über die Content API ist damit ohne Token möglich; Schreiben bleibt token-geschützt. Muss in Phase 1 final entschieden werden.

---

## 7. Offene Entscheidungen (vor/nach Phase 1)

1. ~~Repo public oder private?~~ **Bereits public** – öffentliches Lesen ohne Token ist möglich. (Schreiben weiterhin nur mit Token.)
2. **Live-Lesen (B) vs. Deploy-Trigger (A)** – betrifft wie schnell Edits sichtbar sind.
3. **Frontmatter einführen** sofort oder erst in Phase 4 (beeinflusst Parser).
4. **Mehrbenutzer-Support** relevant? PAT-Modell ist nur für Einzelbesitzer gedacht; für mehrere Benutzer wäre GitHub-OAuth/Backend nötig.

---

## 8. Risiken & Hinweise

- **PAT-Sicherheit:** Token liegt im Browser. Kein serverseitiger Schutz. Nur für Single-User geeignet; bei höheren Ansprüchen auf OAuth/Backend wechseln.
- **Konflikte:** Gleichzeitige Edits → GitHub API wirft Konflikt bei falscher `sha`; saubere Fehlermeldung einbauen (wie beim Rebase in Git).
- **API-Rate-Limits:** Unauthentifizierte Content-API-Requests sind auf 60/Std. begrenzt → Caching wichtig (Option B).
- **PWA-Caching:** Nach Funktionsänderungen `cacheName` erhöhen, sonst sehen Clients alte Version (Erfahrung aus dem letzten Release).
- **Kein Bild-Upload** nötig (nur Text) – hält alles schlank.

---

## 9. Zusammenfassung der Tech-Stack-Wahl

| Bereich | Wahl | Kosten |
|---|---|---|
| Hosting | GitHub Pages (bestehend) | 0 € |
| Storage | Git-Repo auf GitHub | 0 € |
| Datenformat | Markdown (+ optional YAML-Frontmatter) | – |
| Schreiben | GitHub Content API (Blazor/JS) | 0 € |
| Kommentare | GitHub Issues API (bestehend) | 0 € |
| Auth | PAT (localStorage) | 0 € |
| Download | Blob (MD) + Druck-Ansicht/Print-CSS (PDF) | 0 € |

**Gesamtkosten: 0 €. Kein neues Hosting nötig.**
