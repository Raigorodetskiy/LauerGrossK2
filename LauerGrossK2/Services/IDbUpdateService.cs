namespace LauerGrossK2.Services
{
    /// <summary>
    /// Definiert Operationen zum Erstellen des Artikel-Stamm-Exports.
    /// Entspricht der Delphi-Methode <c>Update2DBErzeugen</c> aus <c>LauerGrossK2Unit.pas</c>.
    /// </summary>
    public interface IDbUpdateService
    {
        /// <summary>
        /// Erstellt den Artikel-Stamm-Export für den angegebenen Stichtag und schreibt
        /// die Ergebnisse als CSV- und DBL-Dateien in den lokalen Pfad sowie in den Ausgabepfad.
        /// </summary>
        /// <param name="stichtag">
        /// Stichtag für die Datenauswahl. Der Tag des Datums bestimmt den Gültigkeitszeitraum:
        /// Tag 1–14 → erste Monatshälfte (1. bis 14.);
        /// Tag 15+ → zweite Monatshälfte (15. bis letzter Tag des Monats).
        /// </param>
        /// <returns><c>true</c> bei Erfolg; <c>false</c> bei Fehler.</returns>
        Task<bool> ErstelleArtikelStammAsync(DateTime stichtag);
    }
}
