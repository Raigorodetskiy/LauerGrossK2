using LauerGrossK2.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace LauerGrossK2.Services
{
    // ── Interne DTOs ──────────────────────────────────────────────────────────

    /// <summary>Eine Zeile aus der Quelltabelle RabattVertragArtikel (AbdaDaten).</summary>
    internal sealed class RabattVertragArtikelRow
    {
        /// <summary>Pharmazentralnummer.</summary>
        public int PZN { get; set; }
        /// <summary>Interne Vertragsnummer.</summary>
        public string Vertragsnummer { get; set; } = string.Empty;
        /// <summary>Gültigkeitsbeginn.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Gültigkeitsende.</summary>
        public DateTime GueltigBis { get; set; }
        /// <summary>Mehrkostenverzicht vereinbart.</summary>
        public bool IstMehrkostenverzicht { get; set; }
        /// <summary>Zuzahlungsfaktor.</summary>
        public decimal? Zuzahlungsfaktor { get; set; }
        /// <summary>Aut-idem-Auswahlkennzeichen.</summary>
        public byte AutIdemAuswahl { get; set; }
    }

    /// <summary>Eine Zeile aus der Quelltabelle Rabattvertrag (AbdaDaten).</summary>
    internal sealed class RabattVertragRow
    {
        /// <summary>Institutionskennzeichen der Krankenkasse.</summary>
        public int KassenIK { get; set; }
        /// <summary>Interne Vertragsnummer.</summary>
        public string Vertragsnummer { get; set; } = string.Empty;
        /// <summary>Gültigkeitsbeginn.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Gültigkeitsende.</summary>
        public DateTime GueltigBis { get; set; }
        /// <summary>Bezeichnung der Krankenkasse.</summary>
        public string? Kassenbezeichnung { get; set; }
    }

    /// <summary>Eine Zeile aus AE.Verknuepfung_PAC_INB (Paket-Indikations-Verknüpfung).</summary>
    internal sealed class ArtikelIndikationRow
    {
        /// <summary>Packungsschlüssel (entspricht PZN).</summary>
        public int Key_PAC { get; set; }
        /// <summary>Indikationsschlüssel.</summary>
        public string Key_INB { get; set; } = string.Empty;
        /// <summary>Gültigkeitsbeginn.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Gültigkeitsende.</summary>
        public DateTime GueltigBis { get; set; }
    }

    /// <summary>Eine Zeile aus dbo.Artikel_GleicheIndikation (AbdaDaten).</summary>
    internal sealed class ArtikelGleicheIndikationRow
    {
        /// <summary>PZN des Einstiegsartikels.</summary>
        public int PZN { get; set; }
        /// <summary>PZN des Artikels mit gleicher Indikation.</summary>
        public int PZNGleicheIndikation { get; set; }
        /// <summary>Gültigkeitsbeginn.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Gültigkeitsende.</summary>
        public DateTime GueltigBis { get; set; }
    }

    // ── Service ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Implementierung von <see cref="IRabattVertragsDatenService"/>.
    /// Entspricht der Delphi-Methode <c>RabattVertragsDatenErstellen</c> in <c>LauerGrossK2Unit.pas</c>.
    /// Liest Daten aus der AbdaDaten-Datenbank und schreibt sie in dbpStamm / dbpStammNeu sowie CSV-Dateien.
    /// </summary>
    public class RabattVertragsDatenService : IRabattVertragsDatenService
    {
        private readonly AbdaDbContext _context;
        private readonly ILogger<RabattVertragsDatenService> _logger;
        private readonly string _lokalPfad;
        private readonly string _ausgabePfad;

        /// <summary>
        /// Erstellt eine neue Instanz des <see cref="RabattVertragsDatenService"/>.
        /// </summary>
        /// <param name="context">EF-Core-DbContext für die AbdaDaten-Datenbank.</param>
        /// <param name="logger">Logger-Instanz für Fehler- und Diagnoseausgaben.</param>
        /// <param name="lokalPfad">Lokales Arbeitsverzeichnis für temporäre CSV-Dateien.</param>
        /// <param name="ausgabePfad">Zielverzeichnis, in das fertige Dateien kopiert werden.</param>
        public RabattVertragsDatenService(
            AbdaDbContext context,
            ILogger<RabattVertragsDatenService> logger,
            string lokalPfad,
            string ausgabePfad)
        {
            _context = context;
            _logger = logger;
            _lokalPfad = lokalPfad;
            _ausgabePfad = ausgabePfad;
        }

        // ── Öffentliche Hauptmethode ──────────────────────────────────────────

        /// <inheritdoc />
        public async Task<bool> ErstelleRabattVertragsDatenAsync(DateTime stichtag)
        {
            try
            {
                _logger.LogInformation(
                    "Starte Erstellung der Rabattvertragsdaten für Stichtag {Stichtag}", stichtag);

                var (von, bis) = BerechneStichtagPeriode(stichtag);

                _logger.LogInformation(
                    "Berechnete Periode: {Von} – {Bis}", von, bis);

                await ErstelleRabattVertragArtikelAsync(von, bis);
                await ErstelleRabattVertragAsync(von, bis);
                await ErstelleArtikelIndikationenAsync(von, bis);
                await ErstelleArtikelGleicheIndikationAsync(von, bis);
                await AktualisiereRabattvertragZuordnungAsync(von, bis);

                _logger.LogInformation(
                    "Rabattvertragsdaten erfolgreich erstellt für Periode {Von} – {Bis}", von, bis);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler bei der Erstellung der Rabattvertragsdaten für Stichtag {Stichtag}", stichtag);
                return false;
            }
        }

        // ── Interne Methoden ──────────────────────────────────────────────────

        /// <summary>
        /// Liest RabattVertragArtikel aus AbdaDaten, schreibt die Datensätze per
        /// INSERT … SELECT in dbpStamm.dbo.RabattVertragArtikel und erzeugt eine CSV-Datei.
        /// </summary>
        /// <param name="von">Periodenanfang.</param>
        /// <param name="bis">Periodenende.</param>
        internal async Task ErstelleRabattVertragArtikelAsync(DateTime von, DateTime bis)
        {
            try
            {
                _logger.LogDebug(
                    "Erstelle RabattVertragArtikel für {Von} – {Bis}", von, bis);

                // Quelldaten aus AbdaDaten lesen (benötigt für CSV-Erstellung)
                var rows = await _context.Database
                    .SqlQueryRaw<RabattVertragArtikelRow>(
                        "SELECT PZN, Vertragsnummer, GueltigVon, GueltigBis, " +
                        "IstMehrkostenverzicht, Zuzahlungsfaktor, AutIdemAuswahl " +
                        "FROM dbo.RabattVertragArtikel " +
                        "WHERE GueltigVon >= {0} AND GueltigBis <= {1}",
                        von, bis)
                    .ToListAsync();

                // Zieldaten in dbpStamm per Bulk-INSERT...SELECT aktualisieren
                await _context.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM dbpStamm.dbo.RabattVertragArtikel
                    WHERE GueltigVon >= {0} AND GueltigBis <= {1};

                    INSERT INTO dbpStamm.dbo.RabattVertragArtikel
                        (PZN, Vertragsnummer, GueltigVon, GueltigBis,
                         IstMehrkostenverzicht, Zuzahlungsfaktor, AutIdemAuswahl)
                    SELECT PZN, Vertragsnummer, GueltigVon, GueltigBis,
                           IstMehrkostenverzicht, Zuzahlungsfaktor, AutIdemAuswahl
                    FROM dbo.RabattVertragArtikel
                    WHERE GueltigVon >= {0} AND GueltigBis <= {1};",
                    von, bis);

                // CSV erzeugen und speichern
                var (csv, count) = BaueRabattVertragArtikelCsv(rows);
                await SpeichereUndKopiereAsync("RabattVertragArtikel", csv, count);

                _logger.LogInformation(
                    "RabattVertragArtikel: {Count} Datensätze verarbeitet", rows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler bei ErstelleRabattVertragArtikelAsync ({Von} – {Bis})", von, bis);
                throw;
            }
        }

        /// <summary>
        /// Liest Rabattverträge aus AbdaDaten, schreibt sie per INSERT … SELECT in
        /// dbpStamm.dbo.Rabattvertrag und erzeugt eine CSV-Datei.
        /// </summary>
        /// <param name="von">Periodenanfang.</param>
        /// <param name="bis">Periodenende.</param>
        internal async Task ErstelleRabattVertragAsync(DateTime von, DateTime bis)
        {
            try
            {
                _logger.LogDebug(
                    "Erstelle Rabattvertrag für {Von} – {Bis}", von, bis);

                var rows = await _context.Database
                    .SqlQueryRaw<RabattVertragRow>(
                        "SELECT KassenIK, Vertragsnummer, GueltigVon, GueltigBis, Kassenbezeichnung " +
                        "FROM dbo.Rabattvertrag " +
                        "WHERE GueltigVon >= {0} AND GueltigBis <= {1}",
                        von, bis)
                    .ToListAsync();

                await _context.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM dbpStamm.dbo.Rabattvertrag
                    WHERE GueltigVon >= {0} AND GueltigBis <= {1};

                    INSERT INTO dbpStamm.dbo.Rabattvertrag
                        (KassenIK, Vertragsnummer, GueltigVon, GueltigBis, Kassenbezeichnung)
                    SELECT KassenIK, Vertragsnummer, GueltigVon, GueltigBis, Kassenbezeichnung
                    FROM dbo.Rabattvertrag
                    WHERE GueltigVon >= {0} AND GueltigBis <= {1};",
                    von, bis);

                var (csv, count) = BaueRabattVertragCsv(rows);
                await SpeichereUndKopiereAsync("Rabattvertrag", csv, count);

                _logger.LogInformation(
                    "Rabattvertrag: {Count} Datensätze verarbeitet", rows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler bei ErstelleRabattVertragAsync ({Von} – {Bis})", von, bis);
                throw;
            }
        }

        /// <summary>
        /// Liest Artikel-Indikations-Verknüpfungen aus AE.Verknuepfung_PAC_INB und
        /// erzeugt eine CSV-Datei (kein Schreiben in dbpStamm).
        /// </summary>
        /// <param name="von">Periodenanfang.</param>
        /// <param name="bis">Periodenende.</param>
        internal async Task ErstelleArtikelIndikationenAsync(DateTime von, DateTime bis)
        {
            try
            {
                _logger.LogDebug(
                    "Erstelle ArtikelIndikationen für {Von} – {Bis}", von, bis);

                var rows = await _context.Database
                    .SqlQueryRaw<ArtikelIndikationRow>(
                        "SELECT Key_PAC, Key_INB, GueltigVon, GueltigBis " +
                        "FROM AE.Verknuepfung_PAC_INB " +
                        "WHERE GueltigVon >= {0} AND GueltigBis <= {1}",
                        von, bis)
                    .ToListAsync();

                var (csv, count) = BaueArtikelIndikationCsv(rows);
                await SpeichereUndKopiereAsync("ArtikelIndikationen", csv, count);

                _logger.LogInformation(
                    "ArtikelIndikationen: {Count} Datensätze verarbeitet", rows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler bei ErstelleArtikelIndikationenAsync ({Von} – {Bis})", von, bis);
                throw;
            }
        }

        /// <summary>
        /// Kopiert Artikel_GleicheIndikation per INSERT … SELECT aus AbdaDaten nach dbpStamm,
        /// erzeugt eine CSV-Datei und reorganisiert den Index.
        /// </summary>
        /// <param name="von">Periodenanfang.</param>
        /// <param name="bis">Periodenende.</param>
        internal async Task ErstelleArtikelGleicheIndikationAsync(DateTime von, DateTime bis)
        {
            try
            {
                _logger.LogDebug(
                    "Erstelle ArtikelGleicheIndikation für {Von} – {Bis}", von, bis);

                var rows = await _context.Database
                    .SqlQueryRaw<ArtikelGleicheIndikationRow>(
                        "SELECT PZN, PZNGleicheIndikation, GueltigVon, GueltigBis " +
                        "FROM dbo.Artikel_GleicheIndikation " +
                        "WHERE GueltigVon >= {0} AND GueltigBis <= {1}",
                        von, bis)
                    .ToListAsync();

                await _context.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM dbpStamm.dbo.Artikel_GleicheIndikation
                    WHERE GueltigVon >= {0} AND GueltigBis <= {1};

                    INSERT INTO dbpStamm.dbo.Artikel_GleicheIndikation
                        (PZN, PZNGleicheIndikation, GueltigVon, GueltigBis)
                    SELECT PZN, PZNGleicheIndikation, GueltigVon, GueltigBis
                    FROM dbo.Artikel_GleicheIndikation
                    WHERE GueltigVon >= {0} AND GueltigBis <= {1};",
                    von, bis);

                // Index reorganisieren
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER INDEX ALL ON dbpStamm.dbo.Artikel_GleicheIndikation REORGANIZE");

                var (csv, count) = BaueArtikelGleicheIndikationCsv(rows);
                await SpeichereUndKopiereAsync("ArtikelGleicheIndikation", csv, count);

                _logger.LogInformation(
                    "ArtikelGleicheIndikation: {Count} Datensätze verarbeitet, Index reorganisiert",
                    rows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler bei ErstelleArtikelGleicheIndikationAsync ({Von} – {Bis})", von, bis);
                throw;
            }
        }

        /// <summary>
        /// Führt die komplexe Rabattvertrag-Zuordnungslogik durch.
        /// Verwendet temporäre Tabellen (#TmpArtikel, #TmpHauptKassenIK, #TmpRabattvertrag,
        /// #TmpRabattvertragArtikel, #TmpRV), prüft Biotech-Anlage1-Zugehörigkeit und
        /// schreibt das Ergebnis in dbpStammNeu.dbo.RabattvertragZuordnung.
        /// </summary>
        /// <param name="von">Periodenanfang.</param>
        /// <param name="bis">Periodenende.</param>
        internal async Task AktualisiereRabattvertragZuordnungAsync(DateTime von, DateTime bis)
        {
            try
            {
                _logger.LogDebug(
                    "Aktualisiere RabattvertragZuordnung für {Von} – {Bis}", von, bis);

                // Gesamter Batch in einem einzigen Datenbankaufruf ausführen,
                // damit Temp-Tabellen über alle Schritte hinweg erhalten bleiben.
                const string sql = @"
-- Schritt 1: Relevante Artikel sammeln
SELECT DISTINCT
    rva.PZN,
    a.Langname,
    a.HerstellerKey,
    CAST(CASE WHEN ba.PZN IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IstBiotechAnlage1
INTO #TmpArtikel
FROM dbo.RabattVertragArtikel rva
INNER JOIN AE.Packungsinfos a
    ON a.PZN = rva.PZN
    AND a.GueltigVon <= {0} AND a.GueltigBis >= {1}
LEFT JOIN dbo.Biotech_Anlage1 ba
    ON ba.PZN = rva.PZN
WHERE rva.GueltigVon >= {0} AND rva.GueltigBis <= {1};

-- Schritt 2: Haupt-Kassen-IKs ermitteln
SELECT DISTINCT rv.KassenIK
INTO #TmpHauptKassenIK
FROM dbo.Rabattvertrag rv
WHERE rv.GueltigVon >= {0} AND rv.GueltigBis <= {1};

-- Schritt 3: Rabattvertragsdaten aufbereiten
SELECT rv.KassenIK, rv.Vertragsnummer, rv.GueltigVon, rv.GueltigBis, rv.Kassenbezeichnung
INTO #TmpRabattvertrag
FROM dbo.Rabattvertrag rv
INNER JOIN #TmpHauptKassenIK hk ON hk.KassenIK = rv.KassenIK
WHERE rv.GueltigVon >= {0} AND rv.GueltigBis <= {1};

-- Schritt 4: RabattVertragArtikel mit Vertragsinfos verknüpfen
SELECT rva.PZN, rv.KassenIK, rv.Vertragsnummer, rva.GueltigVon, rva.GueltigBis,
       rva.IstMehrkostenverzicht, rva.Zuzahlungsfaktor, rva.AutIdemAuswahl
INTO #TmpRabattvertragArtikel
FROM #TmpRabattvertrag rv
INNER JOIN dbo.RabattVertragArtikel rva
    ON rva.Vertragsnummer = rv.Vertragsnummer
    AND rva.GueltigVon >= {0} AND rva.GueltigBis <= {1}
INNER JOIN #TmpArtikel ta ON ta.PZN = rva.PZN;

-- Schritt 5: Zuordnungsdaten finalisieren (Biotech-Anlage1-Prüfung)
SELECT
    rva.PZN,
    rva.KassenIK,
    rva.Vertragsnummer,
    rva.GueltigVon,
    rva.GueltigBis,
    rva.IstMehrkostenverzicht,
    rva.Zuzahlungsfaktor,
    rva.AutIdemAuswahl,
    ta.IstBiotechAnlage1
INTO #TmpRV
FROM #TmpRabattvertragArtikel rva
INNER JOIN #TmpArtikel ta ON ta.PZN = rva.PZN;

-- Schritt 6: Veraltete Einträge in dbpStammNeu löschen
DELETE FROM dbpStammNeu.dbo.RabattvertragZuordnung
WHERE GueltigVon >= {0} AND GueltigBis <= {1};

-- Schritt 7: Neue Einträge einfügen
INSERT INTO dbpStammNeu.dbo.RabattvertragZuordnung
    (PZN, KassenIK, Vertragsnummer, GueltigVon, GueltigBis,
     IstMehrkostenverzicht, Zuzahlungsfaktor, AutIdemAuswahl, IstBiotechAnlage1)
SELECT
    PZN, KassenIK, Vertragsnummer, GueltigVon, GueltigBis,
    IstMehrkostenverzicht, Zuzahlungsfaktor, AutIdemAuswahl, IstBiotechAnlage1
FROM #TmpRV;

-- Temp-Tabellen aufräumen
DROP TABLE IF EXISTS #TmpRV;
DROP TABLE IF EXISTS #TmpRabattvertragArtikel;
DROP TABLE IF EXISTS #TmpRabattvertrag;
DROP TABLE IF EXISTS #TmpHauptKassenIK;
DROP TABLE IF EXISTS #TmpArtikel;
";
                await _context.Database.ExecuteSqlRawAsync(sql, von, bis);

                _logger.LogInformation(
                    "RabattvertragZuordnung für {Von} – {Bis} erfolgreich aktualisiert", von, bis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler bei AktualisiereRabattvertragZuordnungAsync ({Von} – {Bis})", von, bis);
                throw;
            }
        }

        /// <summary>
        /// Schreibt den CSV-Inhalt als Datei in den lokalen Pfad, erzeugt eine
        /// DBL-Steuerdatei und kopiert beide Dateien in den Ausgabepfad.
        /// </summary>
        /// <param name="basisDateiname">Dateiname ohne Erweiterung.</param>
        /// <param name="csvInhalt">Vollständiger CSV-Inhalt inkl. Kopfzeile.</param>
        /// <param name="zeilenAnzahl">Anzahl der Datenzeilen (ohne Kopfzeile).</param>
        internal async Task SpeichereUndKopiereAsync(string basisDateiname, string csvInhalt, int zeilenAnzahl)
        {
            try
            {
                Directory.CreateDirectory(_lokalPfad);
                Directory.CreateDirectory(_ausgabePfad);

                var csvPfad = Path.Combine(_lokalPfad, basisDateiname + ".csv");
                var dblPfad = Path.Combine(_lokalPfad, basisDateiname + ".dbl");

                // CSV speichern
                await File.WriteAllTextAsync(csvPfad, csvInhalt, Encoding.UTF8);

                // DBL-Steuerdatei erzeugen; DateTime.Now einmalig erfassen
                var jetzt = DateTime.Now;
                var dblInhalt =
                    $"Datei={basisDateiname}.csv{Environment.NewLine}" +
                    $"Datum={jetzt:dd.MM.yyyy}{Environment.NewLine}" +
                    $"Uhrzeit={jetzt:HH:mm:ss}{Environment.NewLine}" +
                    $"Saetze={zeilenAnzahl}{Environment.NewLine}";
                await File.WriteAllTextAsync(dblPfad, dblInhalt, Encoding.UTF8);

                // Dateien in Ausgabepfad kopieren
                File.Copy(csvPfad, Path.Combine(_ausgabePfad, basisDateiname + ".csv"), overwrite: true);
                File.Copy(dblPfad, Path.Combine(_ausgabePfad, basisDateiname + ".dbl"), overwrite: true);

                _logger.LogDebug(
                    "Datei {Dateiname} gespeichert und nach {Ausgabe} kopiert",
                    basisDateiname, _ausgabePfad);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler bei SpeichereUndKopiereAsync (Datei={Dateiname})", basisDateiname);
                throw;
            }
        }

        /// <summary>
        /// Berechnet den Periodenanfang und das Periodenende aus einem Stichtag.
        /// <list type="bullet">
        /// <item>Tag 1–14 → 1. bis 14. des Monats</item>
        /// <item>Tag 15–Monatsende → 15. bis letzter Tag des Monats</item>
        /// </list>
        /// </summary>
        /// <param name="datum">Referenz-Stichtag.</param>
        /// <returns>Tupel aus <c>Von</c> (Periodenanfang) und <c>Bis</c> (Periodenende).</returns>
        internal static (DateTime Von, DateTime Bis) BerechneStichtagPeriode(DateTime datum)
        {
            if (datum.Day < 15)
            {
                return (
                    new DateTime(datum.Year, datum.Month, 1),
                    new DateTime(datum.Year, datum.Month, 14)
                );
            }
            else
            {
                return (
                    new DateTime(datum.Year, datum.Month, 15),
                    new DateTime(datum.Year, datum.Month,
                        DateTime.DaysInMonth(datum.Year, datum.Month))
                );
            }
        }

        // ── CSV-Hilfsmethoden ─────────────────────────────────────────────────

        private static (string Csv, int ZeilenAnzahl) BaueRabattVertragArtikelCsv(
            IEnumerable<RabattVertragArtikelRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                "PZN;Vertragsnummer;GueltigVon;GueltigBis;" +
                "IstMehrkostenverzicht;Zuzahlungsfaktor;AutIdemAuswahl");

            int count = 0;
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(";",
                    r.PZN,
                    r.Vertragsnummer,
                    r.GueltigVon.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    r.GueltigBis.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    r.IstMehrkostenverzicht ? "1" : "0",
                    r.Zuzahlungsfaktor?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    r.AutIdemAuswahl));
                count++;
            }

            return (sb.ToString(), count);
        }

        private static (string Csv, int ZeilenAnzahl) BaueRabattVertragCsv(
            IEnumerable<RabattVertragRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                "KassenIK;Vertragsnummer;GueltigVon;GueltigBis;Kassenbezeichnung");

            int count = 0;
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(";",
                    r.KassenIK,
                    r.Vertragsnummer,
                    r.GueltigVon.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    r.GueltigBis.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    r.Kassenbezeichnung ?? string.Empty));
                count++;
            }

            return (sb.ToString(), count);
        }

        private static (string Csv, int ZeilenAnzahl) BaueArtikelIndikationCsv(
            IEnumerable<ArtikelIndikationRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Key_PAC;Key_INB;GueltigVon;GueltigBis");

            int count = 0;
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(";",
                    r.Key_PAC,
                    r.Key_INB,
                    r.GueltigVon.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    r.GueltigBis.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)));
                count++;
            }

            return (sb.ToString(), count);
        }

        private static (string Csv, int ZeilenAnzahl) BaueArtikelGleicheIndikationCsv(
            IEnumerable<ArtikelGleicheIndikationRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PZN;PZNGleicheIndikation;GueltigVon;GueltigBis");

            int count = 0;
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(";",
                    r.PZN,
                    r.PZNGleicheIndikation,
                    r.GueltigVon.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    r.GueltigBis.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)));
                count++;
            }

            return (sb.ToString(), count);
        }
    }
}
