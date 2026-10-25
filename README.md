<div align="center">
  <img src="assets/icon.png" width="100" height="100" alt="Dienstplaner Icon">
  <h1>Dienstplaner 2.0</h1>
  <p><b>Intelligente Schichtplan-Erstellung, PDF-Bearbeitung &amp; Monatsstatistik</b></p>
</div>

![Dienstplaner Screenshot](assets/screenshot.png)

## Über das Projekt
Der **Dienstplaner 2.0** ist eine moderne Desktop-Anwendung (.NET 8 WPF), mit der Dienst- und Schichtpläne interaktiv bearbeitet, vollautomatisch nach Prioritäts- und Ruhezeitregeln generiert und als druckfertige PDFs oder Word-Vorlagen (`.doc`) exportiert werden können.

## Neue Features in Version 2.0
- ⚡ **Automatischer Schicht-Generator (Solver):**
  - Besetzt Schichten unter Einhaltung des Mindestbedarfs und der Arbeitszeitregeln.
  - Berücksichtigt Prioritäten der Stammkräfte (**Osman Demir** > **Martin Sitter** > **Anton Steffens**) sowie Aushilfen.
  - Erzwingt Ruhezeitregeln: *Nach Nachtschicht (N) kein Frühdienst direkt (S / T)*.
  - Schützt manuell eingetragene Urlaube (`U`) und freie Tage (`FT`).
- 📅 **Interaktives Monatsraster:**
  - Monat und Jahr frei wählbar mit automatischer Erkennung bayerischer Feiertage.
  - Direkte Schichtzuweisung per Mausklick oder Kontextmenü.
  - Schloss-Funktion (`🔒`) zum Sperren von Zellen vor dem Generator.
- 📄 **PDF & Word Import:**
  - Liest bestehende Dienstplan-PDFs (z. B. *Constantia Pirk*) mit Spalten- und Mitarbeitererkennung ein.
  - Unterstützt auch klassische Word-Vorlagen (`.doc`).
- 💾 **Export in Profi-Qualität:**
  - **Druckfertiger PDF-Export:** Erzeugt ein sauberes A4-Querformat mit Monatsübersicht, Tages-Besetzungsprüfung, Legende und Stundenstatistik via *QuestPDF*.
  - **Word DOC-Export:** Schreibt direkt in die offizielle Pfortendienst-Wordvorlage (`.doc`).
- ⏱ **Schichtmodell:**
  - **Montag bis Freitag:**
    - `S`: 00:00 bis 08:15 Uhr (8h 15m)
    - `N`: 16:00 bis 00:00 Uhr (8h 00m)
  - **Wochenenden & Feiertage:**
    - Zusätzlich `T`: 07:15 bis 16:00 Uhr (8h 45m)
  - **Sondercodes:** `U` (Urlaub), `FT` (Freier Tag), `K` (Krank). Hellgraue Fremdschichten (`STR`, `SO`, `TS`) werden ignoriert.

## Download & Start
Lade dir die neueste, autarke `Dienstplaner.exe` unter den [GitHub Releases](../../releases) herunter.
Das Tool benötigt keine Administratorrechte oder Vorinstallationen, da alle Abhängigkeiten bereits in der Single-File EXE enthalten sind.

## Entwicklung
Das Projekt nutzt **C# / .NET 8 / WPF / MVVM**.
```bash
# Projekt klonen
git clone https://github.com/K0pfnicker/Dienstplaner.git

# Tests ausführen
dotnet test

# Standalone Single-File Release kompilieren
dotnet publish ./src/Dienstplaner/Dienstplaner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o release_output
```
