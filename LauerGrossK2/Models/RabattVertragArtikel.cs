using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LauerGrossK2.Models
{
    /// <summary>
    /// Repräsentiert einen einzelnen Artikel, der unter einem Rabattvertrag geführt wird.
    /// Entspricht der Tabelle <c>dbo.RabattvertragArtikel</c> in der ABDA-Datenbank.
    /// </summary>
    [Table("RabattvertragArtikel", Schema = "dbo")]
    public class RabattVertragArtikel
    {
        /// <summary>Pharmazentralnummer (PZN) des Rabattvertragsartikels.</summary>
        [Key]
        [Column(Order = 0)]
        public int PZN { get; set; }

        /// <summary>Interne Vertragsnummer / Gruppenkey (Key_GRU).</summary>
        [Key]
        [Column(Order = 1)]
        public string Vertragsnummer { get; set; } = string.Empty;

        /// <summary>Datum, ab dem der Vertragsartikel gültig ist.</summary>
        public DateTime GueltigVon { get; set; }

        /// <summary>Datum, bis zu dem der Vertragsartikel gültig ist.</summary>
        public DateTime GueltigBis { get; set; }

        /// <summary>Gibt an, ob ein Mehrkostenverzicht vereinbart wurde.</summary>
        public bool IstMehrkostenverzicht { get; set; }

        /// <summary>Zuzahlungsfaktor gemäß Vertragsregelung.</summary>
        public decimal? Zuzahlungsfaktor { get; set; }

        /// <summary>
        /// Aut-idem-Auswahlkennzeichen.
        /// 0 = kein Aut-idem, 1 = Aut-idem möglich, 2 = Aut-idem obligatorisch.
        /// </summary>
        public byte AutIdemAuswahl { get; set; }
    }
}
