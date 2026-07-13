using LauerGrossK2.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LauerGrossK2.Data
{
    /// <summary>
    /// Entity Framework DbContext für die ABDA-Datenbank (AbdaDaten).
    /// Enthält alle relevanten DbSets für die Artikel- und Rabattvertragsverarbeitung.
    /// </summary>
    public class AbdaDbContext : DbContext
    {
        /// <inheritdoc />
        public AbdaDbContext(DbContextOptions<AbdaDbContext> options) : base(options) { }

        /// <summary>Packungsinfos aus dem AE-Schema (Stammdaten der Fertigarzneimittel).</summary>
        public DbSet<ArtikelStamm> Packungsinfos { get; set; } = null!;

        /// <summary>Packungsgrößen aus dem AE-Schema.</summary>
        public DbSet<PackungsgroesseEntity> Packungsgroessen { get; set; } = null!;

        /// <summary>Erweiterte Packungsgrößen aus dem AE-Schema.</summary>
        public DbSet<PackungsgroesseEntity2> Packungsgroessen2 { get; set; } = null!;

        /// <summary>Adressdaten aus dem AE-Schema (Hersteller, Großhandel etc.).</summary>
        public DbSet<AdresseEntity> Adressen { get; set; } = null!;

        /// <summary>Rabattverträge (dbo-Schema).</summary>
        public DbSet<RabattVertrag> RabattVertrag { get; set; } = null!;

        /// <summary>Rabattvertragsartikel (dbo-Schema).</summary>
        public DbSet<RabattVertragArtikel> RabattVertragArtikel { get; set; } = null!;

        /// <summary>Zuordnung von Artikeln gleicher therapeutischer Indikation (dbo-Schema).</summary>
        public DbSet<ArtikelGleicheIndikation> ArtikelGleicheIndikation { get; set; } = null!;

        /// <summary>Importdaten / Artikelimporte (dbo-Schema).</summary>
        public DbSet<ArtikelImportEntity> ArtikelImporte { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── AE-Schema ────────────────────────────────────────────────────
            // Table name and schema are provided via [Table] Data Annotations on the entity classes.
            // Here we configure composite keys and property constraints via Fluent API.

            modelBuilder.Entity<ArtikelStamm>(entity =>
            {
                entity.HasKey(e => new { e.PZN, e.GueltigVon });
                entity.Property(e => e.PZN).IsRequired();
                entity.Property(e => e.GueltigVon).IsRequired();
                entity.Property(e => e.GueltigBis).IsRequired();
                entity.Property(e => e.Langname).HasMaxLength(100);
                entity.Property(e => e.KeyDAR).HasMaxLength(3);
                entity.Property(e => e.KeyWAR).HasMaxLength(8);
                entity.Property(e => e.Normgroesse).HasMaxLength(2);
            });

            modelBuilder.Entity<PackungsgroesseEntity>(entity =>
            {
                entity.HasKey(e => new { e.PZN, e.GueltigVon });
            });

            modelBuilder.Entity<PackungsgroesseEntity2>(entity =>
            {
                entity.HasKey(e => new { e.PZN, e.GueltigVon });
            });

            modelBuilder.Entity<AdresseEntity>(entity =>
            {
                entity.HasKey(e => new { e.Key_ADR, e.GueltigVon });
            });

            // ── dbo-Schema ────────────────────────────────────────────────────

            modelBuilder.Entity<RabattVertrag>(entity =>
            {
                entity.HasKey(e => new { e.KassenIK, e.Vertragsnummer, e.GueltigVon });
                entity.Property(e => e.Vertragsnummer).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Kassenbezeichnung).HasMaxLength(100);
            });

            modelBuilder.Entity<RabattVertragArtikel>(entity =>
            {
                entity.HasKey(e => new { e.PZN, e.Vertragsnummer, e.GueltigVon });
                entity.Property(e => e.Vertragsnummer).HasMaxLength(50).IsRequired();
            });

            modelBuilder.Entity<ArtikelGleicheIndikation>(entity =>
            {
                entity.HasKey(e => new { e.PZN, e.PZNGleicheIndikation, e.GueltigVon });
            });

            modelBuilder.Entity<ArtikelImportEntity>(entity =>
            {
                entity.HasKey(e => new { e.PZN, e.GueltigVon });
            });
        }
    }

    // ── Hilfs-Entitäten für DbSets ohne dediziertes Model ────────────────────

    /// <summary>Packungsgröße (AE-Schema).</summary>
    [Table("Packungsgroessen", Schema = "AE")]
    public class PackungsgroesseEntity
    {
        /// <summary>Pharmazentralnummer.</summary>
        public int PZN { get; set; }
        /// <summary>Datum, ab dem der Datensatz gültig ist.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Datum, bis zu dem der Datensatz gültig ist.</summary>
        public DateTime GueltigBis { get; set; }
        /// <summary>Normgröße (N1, N2, N3 etc.).</summary>
        public string? Normgroesse { get; set; }
        /// <summary>Packungsgröße (Menge der Einzeleinheiten).</summary>
        public decimal? Menge { get; set; }
    }

    /// <summary>Erweiterte Packungsgröße (AE-Schema).</summary>
    [Table("Packungsgroessen2", Schema = "AE")]
    public class PackungsgroesseEntity2
    {
        /// <summary>Pharmazentralnummer.</summary>
        public int PZN { get; set; }
        /// <summary>Datum, ab dem der Datensatz gültig ist.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Datum, bis zu dem der Datensatz gültig ist.</summary>
        public DateTime GueltigBis { get; set; }
        /// <summary>Erweiterte Packungsgröße.</summary>
        public decimal? Menge2 { get; set; }
    }

    /// <summary>Adresse (Hersteller, Großhandel etc.) aus dem AE-Schema.</summary>
    [Table("Adressen", Schema = "AE")]
    public class AdresseEntity
    {
        /// <summary>Adress-Schlüssel.</summary>
        public int Key_ADR { get; set; }
        /// <summary>Datum, ab dem der Datensatz gültig ist.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Datum, bis zu dem der Datensatz gültig ist.</summary>
        public DateTime GueltigBis { get; set; }
        /// <summary>Name der Firma / des Herstellers.</summary>
        public string? Name { get; set; }
        /// <summary>Ort der Firma.</summary>
        public string? Ort { get; set; }
    }

    /// <summary>Artikelimport-Datensatz (dbo-Schema).</summary>
    [Table("ArtikelImporte", Schema = "dbo")]
    public class ArtikelImportEntity
    {
        /// <summary>Pharmazentralnummer.</summary>
        public int PZN { get; set; }
        /// <summary>Datum, ab dem der Datensatz gültig ist.</summary>
        public DateTime GueltigVon { get; set; }
        /// <summary>Datum, bis zu dem der Datensatz gültig ist.</summary>
        public DateTime GueltigBis { get; set; }
        /// <summary>Importgruppennummer.</summary>
        public int? Importgruppennr { get; set; }
        /// <summary>Importland-Kürzel.</summary>
        public string? Importland { get; set; }
    }
}
