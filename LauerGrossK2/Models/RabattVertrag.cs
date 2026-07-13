using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LauerGrossK2.Models
{
    /// <summary>
    /// Repräsentiert einen Rabattvertrag zwischen einer Krankenkasse und einem Hersteller.
    /// Entspricht der Tabelle <c>dbo.Rabattvertrag</c> in der ABDA-Datenbank.
    /// </summary>
    [Table("Rabattvertrag", Schema = "dbo")]
    public class RabattVertrag
    {
        /// <summary>Institutionskennzeichen (IK) der Krankenkasse.</summary>
        [Key]
        [Column(Order = 0)]
        public int KassenIK { get; set; }

        /// <summary>Interne Vertragsnummer / Gruppenkey (Key_GRU).</summary>
        [Key]
        [Column(Order = 1)]
        public string Vertragsnummer { get; set; } = string.Empty;

        /// <summary>Datum, ab dem der Vertrag gültig ist.</summary>
        public DateTime GueltigVon { get; set; }

        /// <summary>Datum, bis zu dem der Vertrag gültig ist.</summary>
        public DateTime GueltigBis { get; set; }

        /// <summary>Bezeichnung der Krankenkasse.</summary>
        public string? Kassenbezeichnung { get; set; }
    }
}
