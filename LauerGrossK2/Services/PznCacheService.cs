using LauerGrossK2.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace LauerGrossK2.Services
{
    /// <summary>
    /// Service für die Verwaltung des PZN-Caches.
    /// Entspricht der PZNCache.TXT-Funktionalität aus der ursprünglichen Delphi-Implementierung.
    /// Der Cache erlaubt ein schnelles Nachschlagen von Artikeldaten ohne vollständigen DB-Zugriff.
    /// </summary>
    public class PznCacheService
    {
        private readonly ILogger<PznCacheService> _logger;

        /// <summary>
        /// Erstellt eine neue Instanz des <see cref="PznCacheService"/>.
        /// </summary>
        /// <param name="logger">Logger-Instanz für Fehler- und Diagnoseausgaben.</param>
        public PznCacheService(ILogger<PznCacheService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generiert eine Cache-Zeile im Textformat für einen einzelnen Artikel.
        /// Format: <c>PZN|GueltigVon|GueltigBis|LauerVK|LauerEK|Langname|...</c>
        /// </summary>
        /// <param name="artikel">Der Artikel, für den die Cache-Zeile erzeugt werden soll.</param>
        /// <returns>Eine formatierte Cache-Zeile als <see cref="string"/>.</returns>
        public Task<string> GeneratePznCacheLineAsync(ArtikelStamm artikel)
        {
            ArgumentNullException.ThrowIfNull(artikel);

            var sb = new StringBuilder();
            sb.Append(artikel.PZN);
            sb.Append('|');
            sb.Append(artikel.GueltigVon.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(artikel.GueltigBis.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(FormatDecimal(artikel.LauerVK));
            sb.Append('|');
            sb.Append(FormatDecimal(artikel.LauerEK));
            sb.Append('|');
            sb.Append(FormatDecimal(artikel.LauerFP));
            sb.Append('|');
            sb.Append(FormatDecimal(artikel.GrossHandelEK));
            sb.Append('|');
            sb.Append(artikel.Langname ?? string.Empty);
            sb.Append('|');
            sb.Append(artikel.KeyDAR ?? string.Empty);
            sb.Append('|');
            sb.Append(artikel.KeyWAR ?? string.Empty);
            sb.Append('|');
            sb.Append(artikel.IstFertigarzneimittel ? '1' : '0');
            sb.Append('|');
            sb.Append(artikel.IstGenerikum ? '1' : '0');
            sb.Append('|');
            sb.Append(artikel.IstKuehlKette ? '1' : '0');
            sb.Append('|');
            sb.Append(artikel.HerstellerKey?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            return Task.FromResult(sb.ToString());
        }

        /// <summary>
        /// Speichert eine Sammlung von Artikeln als PZN-Cache-Datei.
        /// Jeder Artikel wird als eine Zeile im Cache geschrieben.
        /// </summary>
        /// <param name="artikel">Die zu schreibenden Artikel.</param>
        /// <param name="outputPath">Pfad der Ausgabedatei (z. B. PZNCache.TXT).</param>
        public async Task SavePznCacheAsync(IEnumerable<ArtikelStamm> artikel, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(artikel);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            try
            {
                _logger.LogInformation("Schreibe PZN-Cache nach {Path}", outputPath);

                await using var writer = new StreamWriter(outputPath, append: false, encoding: Encoding.UTF8);

                foreach (var a in artikel)
                {
                    var line = await GeneratePznCacheLineAsync(a);
                    await writer.WriteLineAsync(line);
                }

                _logger.LogInformation("PZN-Cache erfolgreich geschrieben: {Path}", outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Schreiben des PZN-Caches nach {Path}", outputPath);
                throw;
            }
        }

        /// <summary>
        /// Lädt den PZN-Cache aus einer Textdatei und gibt ein Dictionary zurück,
        /// das PZN-Nummern auf ihre jeweiligen <see cref="ArtikelStamm"/>-Objekte abbildet.
        /// </summary>
        /// <param name="cachePath">Pfad der Cache-Datei (z. B. PZNCache.TXT).</param>
        /// <returns>
        /// Ein <see cref="Dictionary{TKey,TValue}"/> mit PZN als Schlüssel
        /// und <see cref="ArtikelStamm"/> als Wert.
        /// </returns>
        public async Task<Dictionary<int, ArtikelStamm>> LoadPznCacheAsync(string cachePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);

            var result = new Dictionary<int, ArtikelStamm>();

            try
            {
                _logger.LogInformation("Lade PZN-Cache von {Path}", cachePath);

                if (!File.Exists(cachePath))
                {
                    _logger.LogWarning("PZN-Cache-Datei nicht gefunden: {Path}", cachePath);
                    return result;
                }

                var lines = await File.ReadAllLinesAsync(cachePath, Encoding.UTF8);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var artikel = ParseCacheLine(line);
                    if (artikel is not null)
                        result[artikel.PZN] = artikel;
                }

                _logger.LogInformation("PZN-Cache geladen: {Count} Einträge", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des PZN-Caches von {Path}", cachePath);
                throw;
            }
        }

        // ── Private Hilfsmethoden ──────────────────────────────────────────────

        private static string FormatDecimal(decimal? value)
            => value.HasValue
                ? value.Value.ToString("F2", CultureInfo.InvariantCulture)
                : string.Empty;

        private static ArtikelStamm? ParseCacheLine(string line)
        {
            var parts = line.Split('|');
            // Mindestanzahl: PZN + GueltigVon + GueltigBis = 3 Felder
            if (parts.Length < 3)
                return null;

            if (!int.TryParse(parts[0], out var pzn))
                return null;

            if (!DateTime.TryParseExact(parts[1], "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var gueltigVon))
                return null;

            if (!DateTime.TryParseExact(parts[2], "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var gueltigBis))
                return null;

            var artikel = new ArtikelStamm
            {
                PZN = pzn,
                GueltigVon = gueltigVon,
                GueltigBis = gueltigBis
            };

            if (parts.Length > 3 && decimal.TryParse(parts[3], NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var lauerVk))
                artikel.LauerVK = lauerVk;

            if (parts.Length > 4 && decimal.TryParse(parts[4], NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var lauerEk))
                artikel.LauerEK = lauerEk;

            if (parts.Length > 5 && decimal.TryParse(parts[5], NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var lauerFp))
                artikel.LauerFP = lauerFp;

            if (parts.Length > 6 && decimal.TryParse(parts[6], NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var ghEk))
                artikel.GrossHandelEK = ghEk;

            if (parts.Length > 7)
                artikel.Langname = parts[7];

            if (parts.Length > 8)
                artikel.KeyDAR = parts[8];

            if (parts.Length > 9)
                artikel.KeyWAR = parts[9];

            if (parts.Length > 10)
                artikel.IstFertigarzneimittel = parts[10] == "1";

            if (parts.Length > 11)
                artikel.IstGenerikum = parts[11] == "1";

            if (parts.Length > 12)
                artikel.IstKuehlKette = parts[12] == "1";

            if (parts.Length > 13 && int.TryParse(parts[13], out var herstellerKey))
                artikel.HerstellerKey = herstellerKey;

            return artikel;
        }
    }
}
