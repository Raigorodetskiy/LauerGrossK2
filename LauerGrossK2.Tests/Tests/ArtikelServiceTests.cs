using LauerGrossK2.Data;
using LauerGrossK2.Models;
using LauerGrossK2.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LauerGrossK2.Tests.Tests
{
    /// <summary>
    /// Unit Tests für <see cref="ArtikelService"/>.
    /// Verwendet EF Core InMemory-Datenbank, um Datenbankabhängigkeiten zu isolieren.
    /// </summary>
    public class ArtikelServiceTests : IDisposable
    {
        private readonly AbdaDbContext _context;
        private readonly ArtikelService _sut;
        private readonly DateTime _stichtag = new DateTime(2024, 6, 1);

        public ArtikelServiceTests()
        {
            var options = new DbContextOptionsBuilder<AbdaDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AbdaDbContext(options);
            _sut = new ArtikelService(_context, NullLogger<ArtikelService>.Instance);

            SeedTestData();
        }

        public void Dispose() => _context.Dispose();

        // ── GetArtikelByPznAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetArtikelByPznAsync_WhenArtikelExists_ReturnsArtikel()
        {
            // Act
            var result = await _sut.GetArtikelByPznAsync(1234567, _stichtag);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1234567, result.PZN);
            Assert.Equal("Aspirin 500mg", result.Langname);
        }

        [Fact]
        public async Task GetArtikelByPznAsync_WhenArtikelNotExists_ReturnsNull()
        {
            // Act
            var result = await _sut.GetArtikelByPznAsync(9999999, _stichtag);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetArtikelByPznAsync_WhenArtikelOutsideGueltigkeitszeitraum_ReturnsNull()
        {
            // Stichtag liegt VOR GueltigVon
            var result = await _sut.GetArtikelByPznAsync(1234567, new DateTime(2020, 1, 1));

            Assert.Null(result);
        }

        // ── GetArtikelByStichtagAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetArtikelByStichtagAsync_ReturnsOnlyGueltigeArtikel()
        {
            // Act
            var result = (await _sut.GetArtikelByStichtagAsync(_stichtag)).ToList();

            // Assert: nur Artikel, deren Gültigkeitszeitraum den Stichtag einschließt
            Assert.All(result, a =>
            {
                Assert.True(a.GueltigVon <= _stichtag);
                Assert.True(_stichtag <= a.GueltigBis);
            });
        }

        [Fact]
        public async Task GetArtikelByStichtagAsync_DoesNotReturnAbgelaufeneArtikel()
        {
            var result = await _sut.GetArtikelByStichtagAsync(_stichtag);

            // Artikel mit PZN 7777777 ist abgelaufen (GueltigBis = 2023-12-31)
            Assert.DoesNotContain(result, a => a.PZN == 7777777);
        }

        // ── GetRabattvertragsArtikelAsync ──────────────────────────────────────

        [Fact]
        public async Task GetRabattvertragsArtikelAsync_ReturnsArtikelForKasse()
        {
            // Act
            var result = (await _sut.GetRabattvertragsArtikelAsync(100695012, _stichtag)).ToList();

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains(result, a => a.PZN == 1234567);
        }

        [Fact]
        public async Task GetRabattvertragsArtikelAsync_DoesNotReturnArtikelForAndereKasse()
        {
            // Abfrage mit einem IK, für den kein Vertrag existiert
            var result = await _sut.GetRabattvertragsArtikelAsync(999999999, _stichtag);

            Assert.Empty(result);
        }

        // ── ArtikelExistiertAsync ─────────────────────────────────────────────

        [Fact]
        public async Task ArtikelExistiertAsync_WhenExists_ReturnsTrue()
        {
            var result = await _sut.ArtikelExistiertAsync(1234567, _stichtag);
            Assert.True(result);
        }

        [Fact]
        public async Task ArtikelExistiertAsync_WhenNotExists_ReturnsFalse()
        {
            var result = await _sut.ArtikelExistiertAsync(9999999, _stichtag);
            Assert.False(result);
        }

        // ── Testdaten ────────────────────────────────────────────────────────

        private void SeedTestData()
        {
            // Gültiger Artikel (Stichtag 2024-06-01 liegt innerhalb des Zeitraums)
            _context.Packungsinfos.AddRange(
                new ArtikelStamm
                {
                    PZN = 1234567,
                    GueltigVon = new DateTime(2023, 1, 1),
                    GueltigBis = new DateTime(2025, 12, 31),
                    Langname = "Aspirin 500mg",
                    IstFertigarzneimittel = true,
                    HerstellerKey = 1001,
                    LauerVK = 5.99m,
                    LauerEK = 4.50m
                },
                new ArtikelStamm
                {
                    PZN = 2345678,
                    GueltigVon = new DateTime(2023, 1, 1),
                    GueltigBis = new DateTime(2025, 12, 31),
                    Langname = "Ibuprofen 400mg",
                    IstFertigarzneimittel = true,
                    HerstellerKey = 1002,
                    LauerVK = 8.49m,
                    LauerEK = 6.20m
                },
                // Abgelaufener Artikel (GueltigBis liegt vor Stichtag)
                new ArtikelStamm
                {
                    PZN = 7777777,
                    GueltigVon = new DateTime(2020, 1, 1),
                    GueltigBis = new DateTime(2023, 12, 31),
                    Langname = "Veraltetes Medikament",
                    IstFertigarzneimittel = false,
                    HerstellerKey = 1003
                }
            );

            // Rabattvertrag für Kasse 100695012
            _context.RabattVertrag.Add(new RabattVertrag
            {
                KassenIK = 100695012,
                Vertragsnummer = "GRP001",
                GueltigVon = new DateTime(2023, 1, 1),
                GueltigBis = new DateTime(2025, 12, 31),
                Kassenbezeichnung = "AOK Bayern"
            });

            // Rabattvertragsartikel: PZN 1234567 gehört zu GRP001
            _context.RabattVertragArtikel.Add(new RabattVertragArtikel
            {
                PZN = 1234567,
                Vertragsnummer = "GRP001",
                GueltigVon = new DateTime(2023, 1, 1),
                GueltigBis = new DateTime(2025, 12, 31),
                IstMehrkostenverzicht = false,
                AutIdemAuswahl = 1
            });

            _context.SaveChanges();
        }
    }
}
