using LauerGrossK2.Models;

namespace LauerGrossK2.Services
{
    /// <summary>
    /// Definiert Operationen zum Abrufen von Rabattvertragsdaten aus der ABDA-Datenbank.
    /// </summary>
    public interface IRabattVertragService
    {
        /// <summary>
        /// Gibt alle zum Stichtag gültigen Rabattverträge einer Krankenkasse zurück.
        /// </summary>
        /// <param name="kassenIK">Institutionskennzeichen (IK) der Krankenkasse.</param>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns>Auflistung der gültigen <see cref="RabattVertrag"/>-Datensätze.</returns>
        Task<IEnumerable<RabattVertrag>> GetVertraegeByKasseAsync(int kassenIK, DateTime stichtag);

        /// <summary>
        /// Gibt alle zum Stichtag gültigen Rabattvertragsartikel zurück.
        /// </summary>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns>Auflistung aller gültigen <see cref="RabattVertragArtikel"/>-Datensätze.</returns>
        Task<IEnumerable<RabattVertragArtikel>> GetVertragsArtikelAsync(DateTime stichtag);

        /// <summary>
        /// Gibt alle Artikel gleicher Indikation zu einem gegebenen Einstiegsartikel zurück.
        /// </summary>
        /// <param name="pzn">PZN des Einstiegsartikels.</param>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns>Auflistung der <see cref="ArtikelGleicheIndikation"/>-Datensätze.</returns>
        Task<IEnumerable<ArtikelGleicheIndikation>> GetGleicheIndikationAsync(int pzn, DateTime stichtag);
    }
}
