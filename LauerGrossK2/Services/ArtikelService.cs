using LauerGrossK2.Data;
using LauerGrossK2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LauerGrossK2.Services
{
    /// <summary>
    /// Implementierung von <see cref="IArtikelService"/>.
    /// Kapselt alle Datenbankzugriffe auf Arzneimittel-Stammdaten via Entity Framework Core 8.
    /// </summary>
    public class ArtikelService : IArtikelService
    {
        private readonly AbdaDbContext _context;
        private readonly ILogger<ArtikelService> _logger;

        /// <summary>
        /// Erstellt eine neue Instanz des <see cref="ArtikelService"/>.
        /// </summary>
        /// <param name="context">EF-Core-DbContext für die ABDA-Datenbank.</param>
        /// <param name="logger">Logger-Instanz für Fehler- und Diagnoseausgaben.</param>
        public ArtikelService(AbdaDbContext context, ILogger<ArtikelService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ArtikelStamm>> GetArtikelByStichtagAsync(DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade alle Artikel für Stichtag {Stichtag}", stichtag);

                return await _context.Packungsinfos
                    .Where(a => a.GueltigVon <= stichtag && stichtag <= a.GueltigBis)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden aller Artikel für Stichtag {Stichtag}", stichtag);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ArtikelStamm?> GetArtikelByPznAsync(int pzn, DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade Artikel PZN={PZN} für Stichtag {Stichtag}", pzn, stichtag);

                return await _context.Packungsinfos
                    .Where(a => a.PZN == pzn
                             && a.GueltigVon <= stichtag
                             && stichtag <= a.GueltigBis)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des Artikels PZN={PZN} für Stichtag {Stichtag}", pzn, stichtag);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ArtikelStamm>> GetArtikelByHerstellerAsync(int herstellerKey, DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade Artikel für HerstellerKey={HerstellerKey}, Stichtag {Stichtag}", herstellerKey, stichtag);

                return await _context.Packungsinfos
                    .Where(a => a.HerstellerKey == herstellerKey
                             && a.GueltigVon <= stichtag
                             && stichtag <= a.GueltigBis)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Artikel für HerstellerKey={HerstellerKey}", herstellerKey);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ArtikelStamm>> GetFertigarzneimittelAsync(DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade Fertigarzneimittel für Stichtag {Stichtag}", stichtag);

                return await _context.Packungsinfos
                    .Where(a => a.IstFertigarzneimittel
                             && a.GueltigVon <= stichtag
                             && stichtag <= a.GueltigBis)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Fertigarzneimittel für Stichtag {Stichtag}", stichtag);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ArtikelStamm>> GetRabattvertragsArtikelAsync(int kassenIK, DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Lade Rabattvertragsartikel für KassenIK={KassenIK}, Stichtag {Stichtag}", kassenIK, stichtag);

                var query =
                    from artikel in _context.Packungsinfos
                    join rva in _context.RabattVertragArtikel
                        on artikel.PZN equals rva.PZN
                    join rv in _context.RabattVertrag
                        on rva.Vertragsnummer equals rv.Vertragsnummer
                    where rv.KassenIK == kassenIK
                       && rv.GueltigVon <= stichtag && stichtag <= rv.GueltigBis
                       && rva.GueltigVon <= stichtag && stichtag <= rva.GueltigBis
                       && artikel.GueltigVon <= stichtag && stichtag <= artikel.GueltigBis
                    select artikel;

                return await query
                    .Distinct()
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Rabattvertragsartikel für KassenIK={KassenIK}", kassenIK);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ArtikelExistiertAsync(int pzn, DateTime stichtag)
        {
            try
            {
                _logger.LogDebug("Prüfe Existenz von PZN={PZN} für Stichtag {Stichtag}", pzn, stichtag);

                return await _context.Packungsinfos
                    .AnyAsync(a => a.PZN == pzn
                               && a.GueltigVon <= stichtag
                               && stichtag <= a.GueltigBis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Prüfen der Existenz von PZN={PZN}", pzn);
                throw;
            }
        }
    }
}
