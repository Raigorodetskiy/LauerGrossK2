using LauerGrossK2.Data;
using LauerGrossK2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LauerGrossK2.Services
{
    /// <summary>
    /// Implementierung von <see cref="IRabattVertragService"/>.
    /// Kapselt alle Datenbankzugriffe auf Rabattvertrags-Stammdaten via Entity Framework Core 8.
    /// </summary>
    public class RabattVertragService : IRabattVertragService
    {
        private readonly AbdaDbContext _context;
        private readonly ILogger<RabattVertragService> _logger;

        /// <summary>
        /// Erstellt eine neue Instanz des <see cref="RabattVertragService"/>.
        /// </summary>
        /// <param name="context">EF-Core-DbContext für die ABDA-Datenbank.</param>
        /// <param name="logger">Logger-Instanz für Fehler- und Diagnoseausgaben.</param>
        public RabattVertragService(AbdaDbContext context, ILogger<RabattVertragService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RabattVertrag>> GetVertraegeByKasseAsync(int kassenIK, DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade Rabattverträge für KassenIK={KassenIK}, Stichtag {Stichtag}", kassenIK, stichtag);

                return await _context.RabattVertrag
                    .Where(rv => rv.KassenIK == kassenIK
                              && rv.GueltigVon <= stichtag
                              && stichtag <= rv.GueltigBis)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Rabattverträge für KassenIK={KassenIK}", kassenIK);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RabattVertragArtikel>> GetVertragsArtikelAsync(DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade alle Rabattvertragsartikel für Stichtag {Stichtag}", stichtag);

                return await _context.RabattVertragArtikel
                    .Where(rva => rva.GueltigVon <= stichtag && stichtag <= rva.GueltigBis)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Rabattvertragsartikel für Stichtag {Stichtag}", stichtag);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ArtikelGleicheIndikation>> GetGleicheIndikationAsync(int pzn, DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade Artikel gleicher Indikation für PZN={PZN}, Stichtag {Stichtag}", pzn, stichtag);

                return await _context.ArtikelGleicheIndikation
                    .Where(ag => ag.PZN == pzn
                              && ag.GueltigVon <= stichtag
                              && stichtag <= ag.GueltigBis)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Artikel gleicher Indikation für PZN={PZN}", pzn);
                throw;
            }
        }
    }
}
