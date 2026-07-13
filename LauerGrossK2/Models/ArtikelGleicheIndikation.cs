using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LauerGrossK2.Models
{
    /// <summary>
    /// Repräsentiert die Zuordnung von Artikeln gleicher therapeutischer Indikation.
    /// Entspricht der Tabelle <c>dbo.Artikel_GleicheIndikation</c> in der ABDA-Datenbank.
    /// Wird für die Aut-idem-Prüfung und Rabattvertragssubstitution verwendet.
    /// </summary>
    [Table("Artikel_GleicheIndikation", Schema = "dbo")]
    public class ArtikelGleicheIndikation
    {
        /// <summary>PZN des Einstiegsartikels (geprüfter Artikel).</summary>
        [Key]
        [Column(Order = 0)]
        public int PZN { get; set; }

        /// <summary>PZN des Artikels mit gleicher Indikation (möglicher Substitutionsartikel).</summary>
        [Key]
        [Column(Order = 1)]
        public int PZNGleicheIndikation { get; set; }

        /// <summary>Datum, ab dem die Indikationsgleichheit gültig ist.</summary>
        public DateTime GueltigVon { get; set; }

        /// <summary>Datum, bis zu dem die Indikationsgleichheit gültig ist.</summary>
        public DateTime GueltigBis { get; set; }
    }
}
