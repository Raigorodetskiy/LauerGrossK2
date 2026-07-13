using System.Globalization;
using System.Text;
using LauerGrossK2.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LauerGrossK2.Services
{
    /// <summary>
    /// Implementierung von <see cref="IDbUpdateService"/>.
    /// Konvertiert die Delphi-Methode <c>Update2DBErzeugen</c> aus <c>LauerGrossK2Unit.pas</c>
    /// nach C# / .NET 8 mit Entity Framework Core 8.
    /// </summary>
    public class DbUpdateService : IDbUpdateService
    {
        private readonly AbdaDbContext _context;
        private readonly ILogger<DbUpdateService> _logger;
        private readonly string _lokalPfad;
        private readonly string _ausgabePfad;

        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;

        // ── Konstruktor ───────────────────────────────────────────────────────

        /// <summary>
        /// Erstellt eine neue Instanz des <see cref="DbUpdateService"/>.
        /// </summary>
        /// <param name="context">EF-Core-DbContext für die ABDA-Datenbank.</param>
        /// <param name="logger">Logger-Instanz für Fehler- und Diagnoseausgaben.</param>
        /// <param name="lokalPfad">
        /// Lokales Arbeitsverzeichnis für temporäre Dateien (z. B. <c>C:\DATEN\LAUERGRK2\</c>).
        /// </param>
        /// <param name="ausgabePfad">
        /// Zielverzeichnis für den fertigen Export
        /// (z. B. <c>\\ars-entwicklung\progs\update\staemme.XX\DBLOAD\</c>).
        /// </param>
        public DbUpdateService(
            AbdaDbContext context,
            ILogger<DbUpdateService> logger,
            string lokalPfad,
            string ausgabePfad)
        {
            _context = context;
            _logger = logger;
            _lokalPfad = lokalPfad;
            _ausgabePfad = ausgabePfad;

            // OEM-Codepage für Dateiausgabe registrieren (CODEPAGE = 'OEM' im BULK INSERT)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // ── Öffentliche Methode ───────────────────────────────────────────────

        /// <inheritdoc />
        public async Task<bool> ErstelleArtikelStammAsync(DateTime stichtag)
        {
            try
            {
                // 1. Zeitraum berechnen (1. Hälfte oder 2. Hälfte des Monats)
                var (von, bis) = BerechneZeitraum(stichtag);

                var sVon = von.ToString("yyyyMMdd", Inv);
                var sBis = bis.ToString("yyyyMMdd", Inv);

                _logger.LogInformation(
                    "Erstelle ArtikelStamm für Zeitraum {Von}–{Bis}", sVon, sBis);

                // 2. SQL-Abfrage ausführen
                var sql = ErstelleSql(sVon, sBis);

                var rows = await _context.Database
                    .SqlQueryRaw<ArtikelStammRow>(sql)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.LogInformation("{Count} Datensätze geladen", rows.Count);

                // 3. Dateinamen festlegen
                var csvName     = $"ArtikelStamm{sVon}.txt";
                var csvAvwlName = $"ArtikelStammAVWL{sVon}.txt";
                var dblName     = $"ArtikelStamm{sVon}.dbl";
                var dblAvwlName = $"ArtikelStammAVWL{sVon}.dbl";

                Directory.CreateDirectory(_lokalPfad);

                var csvPfad     = Path.Combine(_lokalPfad, csvName);
                var csvAvwlPfad = Path.Combine(_lokalPfad, csvAvwlName);
                var dblPfad     = Path.Combine(_lokalPfad, dblName);
                var dblAvwlPfad = Path.Combine(_lokalPfad, dblAvwlName);

                // OEM 850 (Western European DOS) – passend zum BULK INSERT CODEPAGE='OEM'
                Encoding enc;
                try
                {
                    enc = Encoding.GetEncoding(850);
                }
                catch (NotSupportedException)
                {
                    enc = Encoding.UTF8;
                    _logger.LogWarning("OEM-Codepage 850 nicht verfügbar, verwende UTF-8.");
                }

                // 4. CSV-Dateien schreiben
                await using var csvWriter     = new StreamWriter(csvPfad,     append: false, enc);
                await using var csvAvwlWriter = new StreamWriter(csvAvwlPfad, append: false, enc);

                foreach (var row in rows)
                {
                    BerechneZuzahlung(row);
                    BerechneAbgabeKennz(row);
                    BerechneZuzahlungsKennz(row);

                    var zeile = FormatCsvZeile(row, bis);

                    await csvWriter.WriteLineAsync(zeile);
                    await csvAvwlWriter.WriteLineAsync(zeile);
                }

                // 5. DBL-Steuerdateien schreiben
                await SchreibeDblDateiAsync(dblPfad,     "dbpStamm",    "ArtikelStamm", csvName);
                await SchreibeDblDateiAsync(dblAvwlPfad, "dbpClrStamm", "ArtikelStamm", csvAvwlName);

                // 6. Alle 4 Dateien in den Ausgabepfad kopieren
                if (!string.IsNullOrWhiteSpace(_ausgabePfad))
                {
                    Directory.CreateDirectory(_ausgabePfad);
                    File.Copy(csvPfad,     Path.Combine(_ausgabePfad, csvName),     overwrite: true);
                    File.Copy(csvAvwlPfad, Path.Combine(_ausgabePfad, csvAvwlName), overwrite: true);
                    File.Copy(dblPfad,     Path.Combine(_ausgabePfad, dblName),     overwrite: true);
                    File.Copy(dblAvwlPfad, Path.Combine(_ausgabePfad, dblAvwlName), overwrite: true);
                    _logger.LogInformation("Dateien nach {Pfad} kopiert.", _ausgabePfad);
                }

                _logger.LogInformation("ArtikelStamm erfolgreich erstellt: {Name}", csvName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler beim Erstellen des ArtikelStamms für Stichtag {Stichtag}", stichtag);
                return false;
            }
        }

        // ── Private Hilfsmethoden ─────────────────────────────────────────────

        /// <summary>Berechnet den Gültigkeitszeitraum aus dem Stichtag (exakt wie in Delphi).</summary>
        private static (DateTime von, DateTime bis) BerechneZeitraum(DateTime stichtag)
        {
            if (stichtag.Day <= 14)
            {
                // Erste Monatshälfte: 1.–14.
                return (new DateTime(stichtag.Year, stichtag.Month, 1),
                        new DateTime(stichtag.Year, stichtag.Month, 14));
            }
            else
            {
                // Zweite Monatshälfte: 15.–letzter Tag
                return (new DateTime(stichtag.Year, stichtag.Month, 15),
                        new DateTime(stichtag.Year, stichtag.Month,
                            DateTime.DaysInMonth(stichtag.Year, stichtag.Month)));
            }
        }

        /// <summary>
        /// Berechnet die Zuzahlung aus LauerVK und LauerFP (exakt wie in Delphi).
        /// </summary>
        private static void BerechneZuzahlung(ArtikelStammRow r)
        {
            var currLauerVK         = r.LauerVK;
            var currLauerFestbetrag = r.LauerFP;

            // Festbetrag als Basis verwenden, wenn er kleiner und positiv ist
            if (currLauerFestbetrag < currLauerVK && currLauerFestbetrag > 0m)
                currLauerVK = currLauerFestbetrag;

            if (currLauerVK == 0m || r.IstZuzahlungsBefreit == 1)
            {
                r.Zuzahlung = 0m;
            }
            else if (r.IstHilfsmittelverbrauch == 1)
            {
                // Hilfsmittelverbrauch: 10% oder max. 10 Euro
                r.Zuzahlung = currLauerVK >= 100m
                    ? 10m
                    : Runden(currLauerVK / 10m);
            }
            else
            {
                // Standard-Zuzahlungsberechnung (Reihenfolge exakt wie Delphi)
                if (currLauerVK < 5m)
                    r.Zuzahlung = currLauerVK;
                if (currLauerVK >= 100m)
                    r.Zuzahlung = 10m;
                if (currLauerVK >= 5m && currLauerVK <= 50m)
                    r.Zuzahlung = 5m;
                if (currLauerVK > 50m && currLauerVK < 100m)
                    r.Zuzahlung = Runden(currLauerVK / 10m);
            }
        }

        /// <summary>
        /// Berechnet den AbgabeKennz-Wert (exakt wie in Delphi, Reihenfolge beachten!).
        /// </summary>
        private static void BerechneAbgabeKennz(ArtikelStammRow r)
        {
            string sZwi;

            if (r.IstApopflichtig == 1)  sZwi = "0";
            else                          sZwi = "2";

            if (r.IstFertigArzneimittel == 0) sZwi = "3";
            if (r.IstRezeptpflichtig    == 1) sZwi = "1";

            if (r.IstApopflichtig    == 1 && r.IstAMPV == 0) sZwi = "4";
            if (r.IstRezeptpflichtig == 1 && r.IstAMPV == 0) sZwi = "5";

            if (r.IstApopflichtig    == 1 && r.IstAMPVRezepturZuschlag == 1) sZwi = "7";
            if (r.IstRezeptpflichtig == 1 && r.IstAMPVRezepturZuschlag == 1) sZwi = "6";

            if (r.IstRezeptpflichtigMitAusnahme == 1) sZwi = "9";
            if (r.IstRezeptpflichtigMitAusnahme == 1 && r.IstAMPV == 0) sZwi = "1";

            if (r.IstDrogenChemikalie == 1) sZwi = "8";

            r.AbgabeKennz = int.Parse(sZwi, Inv);
        }

        /// <summary>
        /// Berechnet den ZuzahlungsKennz-Wert (exakt wie in Delphi).
        /// </summary>
        private static void BerechneZuzahlungsKennz(ArtikelStammRow r)
        {
            var sZwi = " ";

            if ((r.AundVGruppe?.StartsWith("3006", StringComparison.Ordinal) ?? false)
                || r.ApvGruppe == "04"
                || r.ApvGruppe == "35")
            {
                sZwi = "V";
            }

            r.ZuzahlungsKennz = sZwi;
        }

        /// <summary>
        /// Formatiert eine CSV-Zeile mit allen 136 Feldern (Semikolon-getrennt, exakt wie Delphi).
        /// </summary>
        private static string FormatCsvZeile(ArtikelStammRow r, DateTime bis)
        {
            var sb = new StringBuilder(1024);

            // Hilfsfunktionen
            static string D(decimal? v)  => v.HasValue ? v.Value.ToString("F2", Inv) : "";
            static string I(int? v)      => v.HasValue ? v.Value.ToString(Inv) : "0";
            static string Iq(int? v)     => v.HasValue ? v.Value.ToString(Inv) : "";
            static string L(long v)      => v.ToString(Inv);
            static string S(string? v)   => v ?? "";

            // ApvGruppe mit Impfstoff-Sonderfall (Feld 47)
            var apvGruppeAusgabe = (r.ApvGruppe == "0" &&
                                    (r.AtcCode?.StartsWith("J07", StringComparison.OrdinalIgnoreCase) ?? false))
                ? "1"
                : S(r.ApvGruppe);

            // AundVGruppe mit Padding-Logik (Feld 112)
            var aundVGruppeAusgabe = (r.AundVGruppe != null && r.AundVGruppe != "0")
                ? r.AundVGruppe.PadRight(30, '0')
                : S(r.AundVGruppe);

            // Datum GueltigVon / GueltigBis
            var gueltigVon = r.GueltigVon.ToString("yyyyMMdd", Inv);
            var gueltigBis = bis.ToString("yyyyMMdd", Inv);

            // Helper: Datum-Integer-Feld → 8-stellig
            static string DatumInt(int? v) => v.HasValue ? v.Value.ToString(Inv) : "19010101";

            // ── 136 Felder in SQL-SELECT-Reihenfolge ──────────────────────────

            sb.Append(r.PZN.ToString(Inv));           sb.Append(';'); //  1 PZN
            sb.Append(gueltigVon);                    sb.Append(';'); //  2 gueltigvon
            sb.Append(gueltigBis);                    sb.Append(';'); //  3 gueltigbis
            sb.Append(I(r.Hersteller));               sb.Append(';'); //  4 Hersteller
            sb.Append(S(r.HerstellerName));           sb.Append(';'); //  5 HerstellerName
            sb.Append(D(r.LauerVK));                  sb.Append(';'); //  6 LauerVK
            sb.Append(D(r.LauerVKAlt));               sb.Append(';'); //  7 LauerVKAlt
            sb.Append(D(r.LauerEK));                  sb.Append(';'); //  8 LauerEK
            sb.Append(D(r.LauerFP));                  sb.Append(';'); //  9 LauerFP
            sb.Append(S(r.ArtikelBezeichnung));       sb.Append(';'); // 10 ArtikelBezeichnung
            sb.Append(S(r.Darreichungsform));         sb.Append(';'); // 11 Darreichungsform
            sb.Append(D(r.MengenEinheit));            sb.Append(';'); // 12 MengenEinheit
            sb.Append(S(r.MengenEinheitBezeichnung)); sb.Append(';'); // 13 MengenEinheitBezeichnung
            sb.Append(I(r.ArtikelOriginal));          sb.Append(';'); // 14 ArtikelOriginal
            sb.Append(D(r.Zuzahlung));                sb.Append(';'); // 15 Zuzahlung (berechnet)
            sb.Append(I(r.Negativliste));             sb.Append(';'); // 16 Negativliste
            sb.Append(I(r.RoteListe));                sb.Append(';'); // 17 RoteListe
            sb.Append(I(r.MehrwertsteuerProzent));    sb.Append(';'); // 18 MehrwertsteuerProzent
            sb.Append(S(r.AtcCode));                  sb.Append(';'); // 19 AtcCode
            sb.Append(S(r.ZuzahlungsKennz));          sb.Append(';'); // 20 ZuzahlungsKennz (berechnet)
            sb.Append(I(r.IstBtm));                   sb.Append(';'); // 21 IstBtm
            sb.Append(I(r.AbgabeKennz));              sb.Append(';'); // 22 AbgabeKennz (berechnet)
            sb.Append(Iq(r.VerkehrsKennz));           sb.Append(';'); // 23 VerkehrsKennz
            sb.Append(I(r.GrosshandelKennz));         sb.Append(';'); // 24 GrosshandelKennz
            sb.Append(I(r.IstNettoArtikel));          sb.Append(';'); // 25 IstNettoArtikel
            sb.Append(I(r.IstKlinikPackung));         sb.Append(';'); // 26 IstKlinikPackung
            sb.Append(I(r.IstKrankenhausApo));        sb.Append(';'); // 27 IstKrankenhausApo
            sb.Append(I(r.IstVerbandstoff));          sb.Append(';'); // 28 IstVerbandstoff
            sb.Append(I(r.IstHilfsmittel));           sb.Append(';'); // 29 IstHilfsmittel
            sb.Append(I(r.IstImpfstoff));             sb.Append(';'); // 30 IstImpfstoff
            sb.Append(I(r.IstAutIdem));               sb.Append(';'); // 31 IstAutIdem
            sb.Append(I(r.IstAvArtikel));             sb.Append(';'); // 32 IstAvArtikel
            sb.Append(D(r.HerstRabatt));              sb.Append(';'); // 33 HerstRabatt
            sb.Append(D(r.HerstRabattGenerika));      sb.Append(';'); // 34 HerstRabattGenerika
            sb.Append(D(r.HerstRabattPreismoratorium)); sb.Append(';'); // 35 HerstRabattPreismoratorium
            sb.Append(I(r.GrhdlRabatt));              sb.Append(';'); // 36 GrhdlRabatt
            sb.Append(D(r.HerstAbgabePreis));         sb.Append(';'); // 37 HerstAbgabePreis
            sb.Append(I(r.IstHerstRabatt));           sb.Append(';'); // 38 IstHerstRabatt
            sb.Append(I(r.IstGrhdlRabatt));           sb.Append(';'); // 39 IstGrhdlRabatt
            sb.Append(I(r.IstApoRabatt));             sb.Append(';'); // 40 IstApoRabatt
            sb.Append(I(r.IstFertigArzneimittel));    sb.Append(';'); // 41 IstFertigArzneimittel
            sb.Append(I(r.IstAMPV));                  sb.Append(';'); // 42 IstAMPV
            sb.Append(I(r.IstFestbetrag));            sb.Append(';'); // 43 IstFestbetrag
            sb.Append(I(r.IstNurDirektbezug));        sb.Append(';'); // 44 IstNurDirektbezug
            sb.Append(I(r.IstApopflichtig));          sb.Append(';'); // 45 IstApopflichtig
            sb.Append(I(r.IstRezeptpflichtig));       sb.Append(';'); // 46 IstRezeptpflichtig
            sb.Append(apvGruppeAusgabe);              sb.Append(';'); // 47 ApvGruppe (Impfstoff-Sonderfall)
            sb.Append(S(r.Langtext));                 sb.Append(';'); // 48 Langtext
            sb.Append(I(r.HilfsmittelNr));            sb.Append(';'); // 49 HilfsmittelNr
            sb.Append(L(r.EAN13));                    sb.Append(';'); // 50 EAN13
            sb.Append(I(r.TaxeHersteller));           sb.Append(';'); // 51 TaxeHersteller
            sb.Append(I(r.IstRezeptpflichtigMitAusnahme)); sb.Append(';'); // 52
            sb.Append(I(r.IstCEGekennzeichnet));      sb.Append(';'); // 53 IstCEGekennzeichnet
            sb.Append(I(r.IstDrogenChemikalie));      sb.Append(';'); // 54 IstDrogenChemikalie
            sb.Append(I(r.IstReimport));              sb.Append(';'); // 55 IstReimport
            sb.Append(I(r.IstMedizinProdukt));        sb.Append(';'); // 56 IstMedizinProdukt
            sb.Append(I(r.IstAMPVRezepturZuschlag));  sb.Append(';'); // 57 IstAMPVRezepturZuschlag
            sb.Append(I(r.IstVeterinaerArznei));      sb.Append(';'); // 58 IstVeterinaerArznei
            sb.Append(I(r.IstTFG));                   sb.Append(';'); // 59 IstTFG
            sb.Append(D(r.GrossHandelEK));            sb.Append(';'); // 60 GrossHandelEK
            sb.Append(D(r.KlinikEK));                 sb.Append(';'); // 61 KlinikEK
            sb.Append(I(r.ArtikelNr));                sb.Append(';'); // 62 ArtikelNr
            sb.Append(Iq(r.ArtikelTyp));              sb.Append(';'); // 63 ArtikelTyp
            sb.Append(I(r.KeyFestbetrag));            sb.Append(';'); // 64 KeyFestbetrag
            sb.Append(I(r.InfoZuzahlung));            sb.Append(';'); // 65 InfoZuzahlung
            sb.Append(I(r.IstPreisAngabeVerordnung)); sb.Append(';'); // 66 IstPreisAngabeVerordnung
            sb.Append(I(r.NotfallDepotApotheke));     sb.Append(';'); // 67 NotfallDepotApotheke
            sb.Append(I(r.NotfallDepotArzneimittel)); sb.Append(';'); // 68 NotfallDepotArzneimittel
            sb.Append(I(r.KeyAuswahlTab));            sb.Append(';'); // 69 KeyAuswahlTab
            sb.Append(D(r.Packungsbreite));           sb.Append(';'); // 70 Packungsbreite
            sb.Append(D(r.Packungshoehe));            sb.Append(';'); // 71 Packungshoehe
            sb.Append(D(r.Packungslaenge));           sb.Append(';'); // 72 Packungslaenge
            sb.Append(D(r.Packungsgewicht));          sb.Append(';'); // 73 Packungsgewicht
            sb.Append(I(r.IstFeuchteEmpfindlich));    sb.Append(';'); // 74 IstFeuchteEmpfindlich
            sb.Append(I(r.IstKuehlKette));            sb.Append(';'); // 75 IstKuehlKette
            sb.Append(Iq(r.VerpackungsArt));          sb.Append(';'); // 76 VerpackungsArt
            sb.Append(Iq(r.Lageempfindlich));         sb.Append(';'); // 77 Lageempfindlich
            sb.Append(D(r.LagerTemperaturMax));       sb.Append(';'); // 78 LagerTemperaturMax
            sb.Append(D(r.LagerTemperaturMin));       sb.Append(';'); // 79 LagerTemperaturMin
            sb.Append(Iq(r.Lichtempfindlich));        sb.Append(';'); // 80 Lichtempfindlich
            sb.Append(I(r.IstEichung));               sb.Append(';'); // 81 IstEichung
            sb.Append(I(r.LaufzeitEichung));          sb.Append(';'); // 82 LaufzeitEichung
            sb.Append(I(r.LaufzeitVerfall));          sb.Append(';'); // 83 LaufzeitVerfall
            sb.Append(Iq(r.VerfallKennz));            sb.Append(';'); // 84 VerfallKennz
            sb.Append(I(r.MindestBestellmenge));      sb.Append(';'); // 85 MindestBestellmenge
            sb.Append(I(r.IstBruch));                 sb.Append(';'); // 86 IstBruch
            sb.Append(I(r.IstMGDA));                  sb.Append(';'); // 87 IstMGDA
            sb.Append(I(r.IstAusserVertrieb));        sb.Append(';'); // 88 IstAusserVertrieb
            sb.Append(I(r.IstVertriebApotheke));      sb.Append(';'); // 89 IstVertriebApotheke
            sb.Append(I(r.IstVertriebGrosshandel));   sb.Append(';'); // 90 IstVertriebGrosshandel
            sb.Append(I(r.IstVertriebKrankenhaus));   sb.Append(';'); // 91 IstVertriebKrankenhaus
            sb.Append(I(r.IstVertriebEinzelhandel));  sb.Append(';'); // 92 IstVertriebEinzelhandel
            sb.Append(I(r.VerweisNachfolgePZN));      sb.Append(';'); // 93 VerweisNachfolgePZN
            sb.Append(I(r.VerweisKleinePackungPZN));  sb.Append(';'); // 94 VerweisKleinePackungPZN
            sb.Append(DatumInt(r.VerkehrDatum));      sb.Append(';'); // 95 VerkehrDatum
            sb.Append(DatumInt(r.VertriebDatum));     sb.Append(';'); // 96 VertriebDatum
            sb.Append(DatumInt(r.PreisDatum));        sb.Append(';'); // 97 PreisDatum
            sb.Append(I(r.ZuzahlungAida));            sb.Append(';'); // 98 ZuzahlungAida
            sb.Append(S(r.ZuzahlungAidaInfoTab));     sb.Append(';'); // 99 ZuzahlungAidaInfoTab
            sb.Append(I(r.ReferenzPZN));              sb.Append(';'); // 100 ReferenzPZN
            sb.Append(I(r.IstHilfsmittelverbrauch));  sb.Append(';'); // 101 IstHilfsmittelverbrauch
            sb.Append(I(r.IstAMPREISV_SGB));          sb.Append(';'); // 102 IstAMPREISV_SGB
            sb.Append(I(r.IstBedingteErstattung));    sb.Append(';'); // 103 IstBedingteErstattung
            sb.Append(D(r.PackungsGroesseTab));       sb.Append(';'); // 104 PackungsGroesseTab
            sb.Append(D(r.VKEmpfohlen));              sb.Append(';'); // 105 VKEmpfohlen
            sb.Append(I(r.IstZuzahlungsBefreit));     sb.Append(';'); // 106 IstZuzahlungsBefreit
            sb.Append(Iq(r.LifestyleKennz));          sb.Append(';'); // 107 LifestyleKennz
            sb.Append(I(r.IstAusnahme51AMG));         sb.Append(';'); // 108 IstAusnahme51AMG
            sb.Append(I(r.IstSicherheitDatBlatt));    sb.Append(';'); // 109 IstSicherheitDatBlatt
            sb.Append(I(r.IstZuzahlFestbBefreit));    sb.Append(';'); // 110 IstZuzahlFestbBefreit
            sb.Append(S(r.TaxeWarengruppe));          sb.Append(';'); // 111 TaxeWarengruppe
            sb.Append(aundVGruppeAusgabe);            sb.Append(';'); // 112 AundVGruppe (mit Padding)
            sb.Append(Iq(r.Diaetetikum));             sb.Append(';'); // 113 Diaetetikum
            sb.Append(I(r.IstLebensmittel));          sb.Append(';'); // 114 IstLebensmittel
            sb.Append(I(r.IstNahrungsergaenzungsmittel)); sb.Append(';'); // 115
            sb.Append(I(r.IstBZalsGenerikum));        sb.Append(';'); // 116 IstBZalsGenerikum
            sb.Append(I(r.IstBiotechFAM));            sb.Append(';'); // 117 IstBiotechFAM
            sb.Append(I(r.IstMitteilung_47_1cAMG));   sb.Append(';'); // 118 IstMitteilung_47_1cAMG
            sb.Append(I(r.IstBiozid));                sb.Append(';'); // 119 IstBiozid
            sb.Append(I(r.IstPflanzenschutzmittel));  sb.Append(';'); // 120 IstPflanzenschutzmittel
            sb.Append(I(r.IstGrosshandelsAbschlag));  sb.Append(';'); // 121 IstGrosshandelsAbschlag
            sb.Append(D(r.Rabattwert_130a_2_SGB));    sb.Append(';'); // 122 Rabattwert_130a_2_SGB
            sb.Append(D(r.Rabattwert_130b_SGB));      sb.Append(';'); // 123 Rabattwert_130b_SGB
            sb.Append(I(r.IstFamFuerImport));         sb.Append(';'); // 124 IstFamFuerImport
            sb.Append(S(r.ImportKennz));              sb.Append(';'); // 125 ImportKennz
            sb.Append(S(r.TA3ImportKennz));           sb.Append(';'); // 126 TA3ImportKennz
            sb.Append(I(r.IstApuMitAbzug130b));       sb.Append(';'); // 127 IstApuMitAbzug130b
            sb.Append(D(r.LauerVKBeiApuMitAbzug130b)); sb.Append(';'); // 128 LauerVKBeiApuMitAbzug130b
            sb.Append(I(r.ApBetrO_15_1));             sb.Append(';'); // 129 ApBetrO_15_1
            sb.Append(I(r.ApBetrO_15_2));             sb.Append(';'); // 130 ApBetrO_15_2
            sb.Append(Iq(r.Ausnahme_52b_2_AMG));      sb.Append(';'); // 131 Ausnahme_52b_2_AMG
            sb.Append(I(r.AusnahmeErsetzung));        sb.Append(';'); // 132 AusnahmeErsetzung
            sb.Append(Iq(r.EU_Bio_Logo));             sb.Append(';'); // 133 EU_Bio_Logo
            sb.Append(Iq(r.Kosmetikum_EG_VO));        sb.Append(';'); // 134 Kosmetikum_EG_VO
            sb.Append(Iq(r.Steril));                  sb.Append(';'); // 135 Steril
            sb.Append(Iq(r.TRezept));                 sb.Append(';'); // 136 TRezept
            sb.Append(D(r.UVP));                      sb.Append(';'); // 137 UVP
            sb.Append(Iq(r.Verifikationspflicht));                    // 138 Verifikationspflicht (kein abschließendes ';')

            return sb.ToString();
        }

        /// <summary>Schreibt eine DBL-Steuerdatei für den SQL Server BULK INSERT.</summary>
        private static async Task SchreibeDblDateiAsync(
            string pfad, string db, string tabelle, string ladeдатei)
        {
            var inhalt =
                $"[LOAD]{Environment.NewLine}" +
                $"DB={db}{Environment.NewLine}" +
                $"TABLE={tabelle}{Environment.NewLine}" +
                $"LOADFILE={ladeдатei}{Environment.NewLine}" +
                $"OPTIONS=( CODEPAGE = 'OEM', DATAFILETYPE = 'CHAR', FIELDTERMINATOR = ';', KEEPNULLS, TABLOCK ){Environment.NewLine}";

            await File.WriteAllTextAsync(pfad, inhalt, Encoding.ASCII);
        }

        /// <summary>Kaufmännisches Runden auf 2 Dezimalstellen (wie Delphi RoundTo).</summary>
        private static decimal Runden(decimal wert)
            => Math.Round(wert, 2, MidpointRounding.AwayFromZero);

        // ── SQL-Abfrage ───────────────────────────────────────────────────────

        /// <summary>
        /// Erstellt die SQL-Abfrage mit den ersetzten Platzhaltern &lt;von&gt; und &lt;bis&gt;.
        /// Entspricht exakt der Delphi-Abfrage in <c>Update2DBErzeugen</c>.
        /// Die doppelten Placeholder-Felder (IstFAMFuerImport, ImportKennz, TA3ImportKennz)
        /// werden durch die echten ArtImp-Werte ersetzt.
        /// </summary>
        private static string ErstelleSql(string sVon, string sBis)
        {
            // Die Platzhalter '<von>' und '<bis>' werden direkt ersetzt.
            // Hinweis: Die Datumswerte kommen ausschließlich aus der internen Berechnung
            // (BerechneZeitraum), nicht aus Benutzereingaben – kein SQL-Injection-Risiko.
            return $@"
select pinf.PZN,
       pinf.[gueltigvon],
       Key_ADR_Anbieter as Hersteller,
       Substring(Sortiername,1,7) as HerstellerName,
       Apo_Vk /100.0 as LauerVK,
       isnull((Select TOP 1 p2.Apo_Vk /100.0 from ABDADaten.AE.Packungsinfos p2
        where p2.pzn = pinf.pzn
          and p2.gueltigbis < '{sBis}'
          and p2.Apo_Vk <> pinf.Apo_Vk
          and p2.Kurzname = pinf.Kurzname
        order by p2.gueltigbis desc),
         isnull((Select TOP 1 a1.LauerVK from dbpStamm.dbo.artikelstamm a1
           where a1.pzn = pinf.pzn
             and a1.gueltigbis < '{sBis}'
             and a1.LauerVK * 100 <> pinf.Apo_Vk
             and a1.ArtikelBezeichnung = pinf.Kurzname
           order by a1.gueltigbis desc),Apo_Vk /100.0))
             as LauerVKAlt,
       Apo_Ek / 100.0 as LauerEK,
       isnull(Festbetrag/ 100.0,0.0) as LauerFP,
       isnull(Kurzname,'') as ArtikelBezeichnung,
       isnull(Key_DAR,'') as Darreichungsform,
       isnull(pinf.Menge,pack2.Zahl) as MengenEinheit,
       isnull(Upper(pinf.Einheit),Upper(pack2.Einheit)) as MengenEinheitBezeichnung,
       isnull(PZN_Original,0) as ArtikelOriginal,
       9999 as Zuzahlung,
       isnull(Negativliste - 1,0) as Negativliste,
       9999 as RoteListe,
       (case when MwSt = 1 then 19 when MwSt = 2 then 7 else 0 end) as MehrwertsteuerProzent,
       isnull((case when Substring(Key_WAR,1,1) = 'A' then Substring(Key_WAR,2,7) else '' end),'') as AtcCode,
       '?' as ZuzahlungsKennz,
       (case when BTM = 2 then 1 else 0 end) as IstBtm,
       9 as AbgabeKennz,
       Verkehrsstatus as VerkehrsKennz,
       (case when Vw_Grosshandel > 1 then 1 else 0 end) as GrosshandelKennz,
       0 as IstNettoArtikel,
       (case when Artikeltyp = 2 then 1 else 0 end) as IstKlinikPackung,
       (case when Vw_Krankenhausapo > 1 then 1 else 0 end) as IstKrankenhausApo,
       0 as IstVerbandstoff,
       0 as IstHilfsmittel,
       (case when isnull(SubString(Key_GRU,1,4),'') = '0110' then 1 else 0 end) as IstImpfstoff,
       (case when Key_AUS > 0 then 1 else 0 end) as IstAutIdem,
       (case when isnull(Key_GRU,'') = '' then 0 else 1 end) as IstAvArtikel,
       isnull(Rabwert_Anbieter / 100.0,0) as HerstRabatt,
       isnull(Rabwert_Generikum / 100.0,0) as HerstRabattGenerika,
       isnull(Rabwert_Preismora / 100.0,0) as HerstRabattPreismoratorium,
       0 as GrhdlRabatt,
       (case when ApU = 0 and Vw_Grosshandel = 1 then Apo_Ek / 100.0 else ApU / 100.0 end) as HerstAbgabePreis,
       (case when (Rabwert_Anbieter > 0 or Rabwert_Generikum > 0 or Rabwert_Preismora > 0) then 1 else 0 end) as IstHerstRabatt,
       0 as IstGrhdlRabatt,
       (case when Rab_Apo = 1 then 1 else 0 end) as IstApoRabatt,
       (case when Arzneimittel > 1 then 1 else 0 end) as IstFertigArzneimittel,
       (case when AMPreisV_AMG = 2 then 1 else 0 end) as IstAMPV,
       (case when isnull(Festbetrag,0) > 0 then 1 else 0 end) as IstFestbetrag,
       (case when Vw_Grosshandel = 1 then 1 else 0 end) as IstNurDirektbezug,
       (case when Apopflicht > 1 then 1 else 0 end) as IstApopflichtig,
       (case when Rezeptpflicht > 1 then 1 else 0 end) as IstRezeptpflichtig,
       isnull(SubString(Key_GRU,1,2),'0') as ApvGruppe,
       Langname as Langtext,
       0 as HilfsmittelNr,
       cast(isnull(GTIN,0) as bigint) as EAN13,
       isnull(Key_ADR_Hersteller,0) as TaxeHersteller,
       (case when Rezeptpflicht = 3 then 1 else 0 end) as IstRezeptpflichtigMitAusnahme,
       0 as IstCEGekennzeichnet,
       (case when Droge_Chemikalie > 1 then 1 else 0 end) as IstDrogenChemikalie,
       (case when Import_Reimport = 1 then 1 else 0 end) as IstReimport,
       (case when Medizinprodukt > 1 then 1 else 0 end) as IstMedizinProdukt,
       (case when AMPreisV_AMG > 2 then 1 else 0 end) as IstAMPVRezepturZuschlag,
       (case when Tierarzneimittel > 1 then 1 else 0 end) as IstVeterinaerArznei,
       (case when TFG > 1 then 1 else 0 end) as IstTFG,
       ApU / 100.0 as GrossHandelEK,
       Krankenhaus_Ek / 100.0 as KlinikEK,
       Isnull(ArtikelNr,0) as ArtikelNr,
       ArtikelTyp,
       isnull(pinf.Key_FES,0) as KeyFestbetrag,
       0 as InfoZuzahlung,
       (case when PAngV > 1 then 1 else 0 end) as IstPreisAngabeVerordnung,
       isnull(Notfalldepot_Apo,0) as NotfallDepotApotheke,
       isnull(Notfalldepot_kbAm,0) as NotfallDepotArzneimittel,
       isnull(Key_AUS,0) as KeyAuswahlTab,
       isnull(Breite,0) as Packungsbreite,
       isnull(Hoehe,0) as Packungshoehe,
       isnull(Laenge,0) as Packungslaenge,
       isnull(Gewicht,0) as Packungsgewicht,
       (case when Feuchteempf > 1 then 1 else 0 end) as IstFeuchteEmpfindlich,
       (case when Kuehlkette > 1 then 1 else 0 end) as IstKuehlKette,
       Verpackungsart as VerpackungsArt,
       Lageempf as Lageempfindlich,
       isnull(Lagertemperatur_max,0) as LagerTemperaturMax,
       isnull(Lagertemperatur_min,0) as LagerTemperaturMin,
       Lichtempf as Lichtempfindlich,
       (case when Eichung > 1 then 1 else 0 end) as IstEichung,
       isnull(Laufzeit_Eichung,0) as LaufzeitEichung,
       isnull(Laufzeit_Verfall,0) as LaufzeitVerfall,
       Verfalldatum as VerfallKennz,
       isnull(Mindestbestellmenge,0) as MindestBestellmenge,
       (case when Bruchgefahr > 1 then 1 else 0 end) as IstBruch,
       (case when MGDA = 1 then 1 else 0 end) as IstMGDA,
       (case when Vertriebsstatus = 1 then 1 else 0 end) as IstAusserVertrieb,
       (case when Vw_Apo > 1 then 1 else 0 end) as IstVertriebApotheke,
       (case when Vw_Grosshandel > 1 then 1 else 0 end) as IstVertriebGrosshandel,
       (case when Vw_Krankenhausapo > 1 then 1 else 0 end) as IstVertriebKrankenhaus,
       (case when Vw_sonstEinzelhandel > 1 then 1 else 0 end) as IstVertriebEinzelhandel,
       isnull(PZN_Nachfolger,0) as VerweisNachfolgePZN,
       isnull(PZN_kleinere_Packung,0) as VerweisKleinePackungPZN,
       isnull(Gdat_Vertriebsinfo, isnull(Gdat_Verkehrsstatus, '19010101')) as VerkehrDatum,
       isnull(Gdat_Vertriebsinfo, isnull(Gdat_Vertriebsstatus, '19010101')) as VertriebDatum,
       Gdat_Preise as PreisDatum,
       0 as ZuzahlungAida,
       '' as ZuzahlungAidaInfoTab,
       0 as ReferenzPZN,
       (case when Hm_zum_Verbrauch > 1 then 1 else 0 end) as IstHilfsmittelverbrauch,
       (case when AMPreisV_SGB > 1 then 1 else 0 end) as IstAMPREISV_SGB,
       (case when Bedingte_Erstatt_FAM > 1 then 1 else 0 end) as IstBedingteErstattung,
       isnull(Pack.Einstufung * 1000000,0000000) as PackungsGroesseTab,
       isnull(Apo_Vk_empfohlen,0) / 100.0 as VKEmpfohlen,
       (case when Zuzfrei_31SGB_Tstr = 2 then 1 else 0 end) as IstZuzahlungsBefreit,
       Lifestyle as LifestyleKennz,
       (case when Ausnahme_51AMG = 2 then 1 else 0 end) as IstAusnahme51AMG,
       (case when SDB_erforderlich = 2 then 1 else 0 end) as IstSicherheitDatBlatt,
       (case when Zuzfrei_31SGB_Feb = 2 then 1 else 0 end) as IstZuzahlFestbBefreit,
       isnull((case when Substring(Key_WAR,1,1) = 'B' then Substring(Key_WAR,2,7) else ' ' end),'') as TaxeWarengruppe,
       isnull(Key_GRU,'0') as AundVGruppe,
       Diaetetikum as Diaetetikum,
       (case when Lebensmittel > 1 then 1 else 0 end) as IstLebensmittel,
       (case when NEM > 1 then 1 else 0 end) as IstNahrungsergaenzungsmittel,
       (case when Generikum > 1 then 1 else 0 end) as IstBZalsGenerikum,
       (case when Biotech_FAM = 1 then 1 else 0 end) as IstBiotechFAM,
       (case when Mitteilung_47_1cAMG > 1 then 1 else 0 end) as IstMitteilung_47_1cAMG,
       (case when Biozid > 1 then 1 else 0 end) as IstBiozid,
       (case when Pflanzenschutzmittel > 1 then 1 else 0 end) as IstPflanzenschutzmittel,
       (case when Grosshandelsabschlag > 1 then 1 else 0 end) as IstGrosshandelsAbschlag,
       isnull(Rabwert_130a_2_SGB / 100.0,0) as Rabattwert_130a_2_SGB,
       isnull(Rabwert_130b_SGB / 100.0,0) as Rabattwert_130b_SGB,
       case when isnull(ArtImp.TA3ImportKennz,' ') <> ' ' then 1 else 0 end as IstFamFuerImport,
       isnull(ArtImp.ImportKennz,' ') as ImportKennz,
       isnull(ArtImp.TA3ImportKennz,' ') as TA3ImportKennz,
       (case when ApU_mit_Abzug_130b = 1 then 1 else 0 end) as IstApuMitAbzug130b,
       LauerVKBeiApuMitAbzug130b = case when ApU_mit_Abzug_130b = 1 then
            ROUND((ROUND((apu + Rabwert_130b_SGB +
              ROUND(CASE WHEN (apu + Rabwert_130b_SGB) * 0.0315 > 3780 THEN 3780
                         ELSE (apu + Rabwert_130b_SGB) * 0.0315 END + 70, 2)) * 1.03, 0)
              + (isnull(ar.Festzuschlag,0) * 100))
              * (1.0 + (case when MwSt = 1 then 19 when MwSt = 2 then 7 else 0 END) / 100.0) / 100.0, 2)
            else 0.00 end,
       isnull(ApBetrO_15_1,0) as ApBetrO_15_1,
       isnull(ApBetrO_15_2,0) as ApBetrO_15_2,
       Ausnahme_52b_2_AMG as Ausnahme_52b_2_AMG,
       (case when Ausnahme_Ersetzung = 1 then 1 else 0 end) as AusnahmeErsetzung,
       EU_Bio_Logo as EU_Bio_Logo,
       Kosmetikum_EG_VO as Kosmetikum_EG_VO,
       Steril as Steril,
       T_Rezept as TRezept,
       (UVP / 100.0) as UVP,
       Verifikationspflicht as Verifikationspflicht
from ABDADaten.AE.Packungsinfos pinf
     left outer join dbpStammNeu.dbo.ApoRabatt ar on ('{sVon}' between ar.[gueltigvon] and ar.[gueltigbis] AND ar.AVArt = 5 AND ar.IstProzentwert = 0 AND ar.IstRezeptpflichtig = 1)
     left outer join ABDADaten.VU.Artikel PVArt on (PVArt.PZN = pinf.PZN and '{sVon}' between PVArt.[gueltigvon] and PVArt.[gueltigbis])
     left outer join ABDADaten.AE.Packungsgroessen Pack on (Pack.PZN = pinf.PZN and '{sVon}' between Pack.[gueltigvon] and Pack.[gueltigbis])
     left outer join ABDADaten.AE.Packungsgroessen_2 Pack2 on (Pack2.PZN = pinf.PZN and '{sVon}' between Pack2.[gueltigvon] and Pack2.[gueltigbis] and pack2.Zaehler = 1)
     left outer join ABDADaten.AE.Adressen adr on (adr.Key_ADR = pinf.Key_ADR_Anbieter and '{sVon}' between adr.[gueltigvon] and adr.[gueltigbis])
     left outer join ABDADaten.dbo.[Artikel_Importe] ArtImp on (ArtImp.PZN = pinf.PZN and '{sVon}' between ArtImp.[gueltigvon] and ArtImp.[gueltigbis])
where '{sVon}' between pinf.[gueltigvon] and pinf.[gueltigbis]
order by PZN
";
        }

        // ── Interne DTO-Klasse ────────────────────────────────────────────────

        /// <summary>
        /// Internes Datenobjekt für das Ergebnis der SQL-Abfrage (Update2DBErzeugen).
        /// Die Property-Namen entsprechen exakt den SQL-Spaltenaliassen (case-insensitiv).
        /// </summary>
        internal sealed class ArtikelStammRow
        {
            // ── Identifikation ─────────────────────────────────────────────────
            public int      PZN          { get; set; }
            public DateTime GueltigVon   { get; set; }

            // ── Hersteller ──────────────────────────────────────────────────────
            public int?    Hersteller    { get; set; }
            public string? HerstellerName { get; set; }

            // ── Preise ─────────────────────────────────────────────────────────
            public decimal  LauerVK      { get; set; }
            public decimal? LauerVKAlt   { get; set; }
            public decimal  LauerEK      { get; set; }
            public decimal  LauerFP      { get; set; }
            public decimal  GrossHandelEK { get; set; }
            public decimal  KlinikEK     { get; set; }
            public decimal  HerstAbgabePreis { get; set; }
            public decimal  VKEmpfohlen  { get; set; }
            public decimal  UVP          { get; set; }
            public decimal  LauerVKBeiApuMitAbzug130b { get; set; }

            // ── Rabatte ────────────────────────────────────────────────────────
            public decimal  HerstRabatt                { get; set; }
            public decimal  HerstRabattGenerika        { get; set; }
            public decimal  HerstRabattPreismoratorium { get; set; }
            public decimal  Rabattwert_130a_2_SGB      { get; set; }
            public decimal  Rabattwert_130b_SGB        { get; set; }

            // ── Bezeichnung / Klassifikation ────────────────────────────────────
            public string   ArtikelBezeichnung        { get; set; } = string.Empty;
            public string   Darreichungsform          { get; set; } = string.Empty;
            public decimal? MengenEinheit             { get; set; }
            public string?  MengenEinheitBezeichnung  { get; set; }
            public string   AtcCode                   { get; set; } = string.Empty;
            public string?  Langtext                  { get; set; }
            public string   TaxeWarengruppe           { get; set; } = string.Empty;
            public string   AundVGruppe               { get; set; } = "0";
            public string   ApvGruppe                 { get; set; } = "0";

            // ── Integer-Kennzeichen aus SQL ────────────────────────────────────
            public int  ArtikelOriginal   { get; set; }
            /// <summary>Zuzahlung in Euro (Placeholder 9999, wird neu berechnet).</summary>
            public decimal Zuzahlung      { get; set; }
            public int  Negativliste      { get; set; }
            public int  RoteListe         { get; set; }
            public int  MehrwertsteuerProzent { get; set; }
            /// <summary>ZuzahlungsKennz (Placeholder '?', wird neu berechnet).</summary>
            public string ZuzahlungsKennz { get; set; } = "?";
            public int  IstBtm            { get; set; }
            /// <summary>AbgabeKennz (Placeholder 9, wird neu berechnet).</summary>
            public int  AbgabeKennz       { get; set; }
            public int? VerkehrsKennz     { get; set; }
            public int  GrosshandelKennz  { get; set; }
            public int  IstNettoArtikel   { get; set; }
            public int  IstKlinikPackung  { get; set; }
            public int  IstKrankenhausApo { get; set; }
            public int  IstVerbandstoff   { get; set; }
            public int  IstHilfsmittel    { get; set; }
            public int  IstImpfstoff      { get; set; }
            public int  IstAutIdem        { get; set; }
            public int  IstAvArtikel      { get; set; }
            public int  GrhdlRabatt       { get; set; }
            public int  IstHerstRabatt    { get; set; }
            public int  IstGrhdlRabatt    { get; set; }
            public int  IstApoRabatt      { get; set; }
            public int  IstFertigArzneimittel { get; set; }
            public int  IstAMPV           { get; set; }
            public int  IstFestbetrag     { get; set; }
            public int  IstNurDirektbezug { get; set; }
            public int  IstApopflichtig   { get; set; }
            public int  IstRezeptpflichtig { get; set; }
            public int  HilfsmittelNr    { get; set; }
            public long EAN13             { get; set; }
            public int  TaxeHersteller    { get; set; }
            public int  IstRezeptpflichtigMitAusnahme { get; set; }
            public int  IstCEGekennzeichnet { get; set; }
            public int  IstDrogenChemikalie { get; set; }
            public int  IstReimport       { get; set; }
            public int  IstMedizinProdukt { get; set; }
            public int  IstAMPVRezepturZuschlag { get; set; }
            public int  IstVeterinaerArznei { get; set; }
            public int  IstTFG            { get; set; }
            public int  ArtikelNr         { get; set; }
            public int? ArtikelTyp        { get; set; }
            public int  KeyFestbetrag     { get; set; }
            public int  InfoZuzahlung     { get; set; }
            public int  IstPreisAngabeVerordnung { get; set; }
            public int  NotfallDepotApotheke    { get; set; }
            public int  NotfallDepotArzneimittel { get; set; }
            public int  KeyAuswahlTab     { get; set; }

            // ── Packungsdaten ──────────────────────────────────────────────────
            public decimal  Packungsbreite   { get; set; }
            public decimal  Packungshoehe    { get; set; }
            public decimal  Packungslaenge   { get; set; }
            public decimal  Packungsgewicht  { get; set; }
            public int      IstFeuchteEmpfindlich { get; set; }
            public int      IstKuehlKette    { get; set; }
            public int?     VerpackungsArt   { get; set; }
            public int?     Lageempfindlich  { get; set; }
            public decimal  LagerTemperaturMax { get; set; }
            public decimal  LagerTemperaturMin { get; set; }
            public int?     Lichtempfindlich { get; set; }
            public int      IstEichung       { get; set; }
            public int      LaufzeitEichung  { get; set; }
            public int      LaufzeitVerfall  { get; set; }
            public int?     VerfallKennz     { get; set; }
            public int      MindestBestellmenge { get; set; }
            public int      IstBruch         { get; set; }
            public int      IstMGDA          { get; set; }

            // ── Vertrieb ───────────────────────────────────────────────────────
            public int  IstAusserVertrieb       { get; set; }
            public int  IstVertriebApotheke     { get; set; }
            public int  IstVertriebGrosshandel  { get; set; }
            public int  IstVertriebKrankenhaus  { get; set; }
            public int  IstVertriebEinzelhandel { get; set; }
            public int  VerweisNachfolgePZN     { get; set; }
            public int  VerweisKleinePackungPZN { get; set; }

            // ── Datum-Felder (ABDA speichert als int YYYYMMDD) ─────────────────
            public int? VerkehrDatum { get; set; }
            public int? VertriebDatum { get; set; }
            public int? PreisDatum   { get; set; }

            // ── Zusatzinformationen ────────────────────────────────────────────
            public int    ZuzahlungAida        { get; set; }
            public string ZuzahlungAidaInfoTab { get; set; } = string.Empty;
            public int    ReferenzPZN          { get; set; }
            public int    IstHilfsmittelverbrauch { get; set; }
            public int    IstAMPREISV_SGB      { get; set; }
            public int    IstBedingteErstattung { get; set; }
            public decimal PackungsGroesseTab  { get; set; }
            public int    IstZuzahlungsBefreit { get; set; }
            public int?   LifestyleKennz       { get; set; }
            public int    IstAusnahme51AMG     { get; set; }
            public int    IstSicherheitDatBlatt { get; set; }
            public int    IstZuzahlFestbBefreit { get; set; }

            // ── Warengruppe / A&V ──────────────────────────────────────────────
            public int?  Diaetetikum                   { get; set; }
            public int   IstLebensmittel               { get; set; }
            public int   IstNahrungsergaenzungsmittel  { get; set; }
            public int   IstBZalsGenerikum             { get; set; }
            public int   IstBiotechFAM                 { get; set; }
            public int   IstMitteilung_47_1cAMG        { get; set; }
            public int   IstBiozid                     { get; set; }
            public int   IstPflanzenschutzmittel       { get; set; }
            public int   IstGrosshandelsAbschlag       { get; set; }

            // ── Import ─────────────────────────────────────────────────────────
            public int    IstFamFuerImport { get; set; }
            public string ImportKennz      { get; set; } = " ";
            public string TA3ImportKennz   { get; set; } = " ";

            // ── APU mit Abzug 130b ────────────────────────────────────────────
            public int IstApuMitAbzug130b { get; set; }

            // ── Apothekenbetriebsordnung ──────────────────────────────────────
            public int ApBetrO_15_1 { get; set; }
            public int ApBetrO_15_2 { get; set; }

            // ── Sonstige Kennzeichen ──────────────────────────────────────────
            public int?  Ausnahme_52b_2_AMG  { get; set; }
            public int   AusnahmeErsetzung   { get; set; }
            public int?  EU_Bio_Logo         { get; set; }
            public int?  Kosmetikum_EG_VO    { get; set; }
            public int?  Steril              { get; set; }
            public int?  TRezept             { get; set; }
            public int?  Verifikationspflicht { get; set; }
        }
    }
}
