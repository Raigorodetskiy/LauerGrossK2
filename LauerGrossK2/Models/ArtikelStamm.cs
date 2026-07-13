using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LauerGrossK2.Models
{
    /// <summary>
    /// Repräsentiert einen Arzneimittel-Stammdatensatz aus der ABDA-Datenbank.
    /// Entspricht den Feldern aus dem DB-Export (Update2DBErzeugen-Methode) der Delphi-Quelle.
    /// </summary>
    [Table("Packungsinfos", Schema = "AE")]
    public class ArtikelStamm
    {
        /// <summary>Pharmazentralnummer (PZN) des Artikels.</summary>
        public int PZN { get; set; }

        /// <summary>Datum, ab dem der Datensatz gültig ist.</summary>
        public DateTime GueltigVon { get; set; }

        /// <summary>Datum, bis zu dem der Datensatz gültig ist.</summary>
        public DateTime GueltigBis { get; set; }

        // ── Preise ──────────────────────────────────────────────────────────

        /// <summary>Lauer-Verkaufspreis.</summary>
        public decimal? LauerVK { get; set; }

        /// <summary>Lauer-Einkaufspreis.</summary>
        public decimal? LauerEK { get; set; }

        /// <summary>Lauer-Festpreis.</summary>
        public decimal? LauerFP { get; set; }

        /// <summary>Alter Lauer-Verkaufspreis.</summary>
        public decimal? LauerVKAlt { get; set; }

        /// <summary>Großhandels-Einkaufspreis.</summary>
        public decimal? GrossHandelEK { get; set; }

        /// <summary>Klinik-Einkaufspreis.</summary>
        public decimal? KlinikEK { get; set; }

        /// <summary>Hersteller-Abgabepreis.</summary>
        public decimal? HerstAbgabePreis { get; set; }

        /// <summary>Unverbindliche Verkaufspreisempfehlung.</summary>
        public decimal? UVP { get; set; }

        // ── Kennzeichen (bool-Felder) ────────────────────────────────────────

        /// <summary>Kennzeichen: Artikel ist rezeptpflichtig.</summary>
        public bool IstRezeptpflichtig { get; set; }

        /// <summary>Kennzeichen: Artikel ist OTC (Over The Counter).</summary>
        public bool IstOTC { get; set; }

        /// <summary>Kennzeichen: Artikel ist ein Generikum.</summary>
        public bool IstGenerikum { get; set; }

        /// <summary>Kennzeichen: Artikel ist ein Fertigarzneimittel.</summary>
        public bool IstFertigarzneimittel { get; set; }

        /// <summary>Kennzeichen: Artikel ist betäubungsmittelpflichtig.</summary>
        public bool IstBtmPflichtig { get; set; }

        /// <summary>Kennzeichen: Artikel ist verschreibungspflichtig.</summary>
        public bool IstVerschreibungspflichtig { get; set; }

        /// <summary>Kennzeichen: Artikel ist apothekenpflichtig.</summary>
        public bool IstApothekenpflichtig { get; set; }

        /// <summary>Kennzeichen: Artikel ist ein Importartikel.</summary>
        public bool IstImport { get; set; }

        /// <summary>Kennzeichen: Artikel ist ein Biotech-Arzneimittel.</summary>
        public bool IstBiotech { get; set; }

        /// <summary>Kennzeichen: Artikel ist ein biosimilares Arzneimittel.</summary>
        public bool IstBiosimilar { get; set; }

        /// <summary>Kennzeichen: Artikel unterliegt dem Preismoratorium.</summary>
        public bool IstPreismoratorium { get; set; }

        /// <summary>Kennzeichen: Artikel ist aus dem Vertrieb genommen.</summary>
        public bool IstAusserVertrieb { get; set; }

        // ── Rabatte ──────────────────────────────────────────────────────────

        /// <summary>Herstellerrabatt (Standardrabatt).</summary>
        public decimal? HerstRabatt { get; set; }

        /// <summary>Herstellerrabatt für Generika.</summary>
        public decimal? HerstRabattGenerika { get; set; }

        /// <summary>Herstellerrabatt Preismoratorium.</summary>
        public decimal? HerstRabattPreismoratorium { get; set; }

        /// <summary>Rabattwert gemäß §130a SGB V (Pflichtrabatt).</summary>
        public decimal? Rabattwert130a { get; set; }

        /// <summary>Rabattwert gemäß §130b SGB V (Herstellerrabatt für patentgeschützte Arzneimittel).</summary>
        public decimal? Rabattwert130b { get; set; }

        // ── Logistik ──────────────────────────────────────────────────────────

        /// <summary>Breite der Packung in mm.</summary>
        public decimal? Packungsbreite { get; set; }

        /// <summary>Höhe der Packung in mm.</summary>
        public decimal? Packungshoehe { get; set; }

        /// <summary>Länge der Packung in mm.</summary>
        public decimal? Packungslaenge { get; set; }

        /// <summary>Gewicht der Packung in Gramm.</summary>
        public decimal? Packungsgewicht { get; set; }

        /// <summary>Kennzeichen: Artikel ist kühlkettenrelevant.</summary>
        public bool IstKuehlKette { get; set; }

        /// <summary>Kennzeichen: Artikel ist feuchtigkeitsempfindlich.</summary>
        public bool IstFeuchteEmpfindlich { get; set; }

        /// <summary>Kennzeichen: Artikel ist lichtempfindlich.</summary>
        public bool IstLichtEmpfindlich { get; set; }

        /// <summary>Kennzeichen: Artikel ist bruchempfindlich.</summary>
        public bool IstBruchEmpfindlich { get; set; }

        // ── Verweise ──────────────────────────────────────────────────────────

        /// <summary>PZN des Nachfolgeartikels.</summary>
        public int? VerweisNachfolgePZN { get; set; }

        /// <summary>PZN der kleinsten Packung desselben Arzneimittels.</summary>
        public int? VerweisKleinePackungPZN { get; set; }

        // ── Datum-Felder ──────────────────────────────────────────────────────

        /// <summary>Datum der Verkehrszulassung.</summary>
        public DateTime? VerkehrDatum { get; set; }

        /// <summary>Datum der Vertriebsaufnahme.</summary>
        public DateTime? VertriebDatum { get; set; }

        /// <summary>Datum der letzten Preisänderung.</summary>
        public DateTime? PreisDatum { get; set; }

        // ── Weitere Stammdaten ────────────────────────────────────────────────

        /// <summary>Langname / Bezeichnung des Arzneimittels.</summary>
        public string? Langname { get; set; }

        /// <summary>Hersteller-Key (Herstellernummer).</summary>
        public int? HerstellerKey { get; set; }

        /// <summary>Darreichungsform-Schlüssel (Key_DAR).</summary>
        public string? KeyDAR { get; set; }

        /// <summary>Wirkstoffgruppen-Schlüssel (Key_WAR / ATC-Code).</summary>
        public string? KeyWAR { get; set; }

        /// <summary>Normgröße.</summary>
        public string? Normgroesse { get; set; }

        /// <summary>Vertriebsstatus (0 = normal, 1 = außer Vertrieb).</summary>
        public int? Vertriebsstatus { get; set; }

        /// <summary>Importgruppennummer für Import-/Reimportartikel.</summary>
        public int? Importgruppennr { get; set; }

        /// <summary>Ausnahme-Ersetzungskennzeichen (Biotech-Regelung).</summary>
        public byte? Ausnahme_Ersetzung { get; set; }
    }
}
