using LauerGrossK2.Models;

namespace LauerGrossK2.Services
{
    /// <summary>
    /// Definiert Operationen zum Abrufen von Arzneimittel-Stammdaten aus der ABDA-Datenbank.
    /// </summary>
    public interface IArtikelService
    {
        /// <summary>
        /// Gibt alle zum angegebenen Stichtag gültigen Artikel zurück.
        /// </summary>
        /// <param name="stichtag">Referenzdatum; es werden nur Datensätze geliefert,
        /// für die gilt: <c>GueltigVon &lt;= stichtag &lt;= GueltigBis</c>.</param>
        /// <returns>Auflistung aller zum Stichtag gültigen <see cref="ArtikelStamm"/>-Datensätze.</returns>
        Task<IEnumerable<ArtikelStamm>> GetArtikelByStichtagAsync(DateTime stichtag);

        /// <summary>
        /// Gibt den Artikel mit der angegebenen PZN zum Stichtag zurück.
        /// </summary>
        /// <param name="pzn">Pharmazentralnummer des gesuchten Artikels.</param>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns>Der gefundene <see cref="ArtikelStamm"/>, oder <c>null</c> wenn nicht gefunden.</returns>
        Task<ArtikelStamm?> GetArtikelByPznAsync(int pzn, DateTime stichtag);

        /// <summary>
        /// Gibt alle zum Stichtag gültigen Artikel eines Herstellers zurück.
        /// </summary>
        /// <param name="herstellerKey">Hersteller-Schlüsselnummer.</param>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns>Auflistung der Artikel des angegebenen Herstellers.</returns>
        Task<IEnumerable<ArtikelStamm>> GetArtikelByHerstellerAsync(int herstellerKey, DateTime stichtag);

        /// <summary>
        /// Gibt alle zum Stichtag gültigen Fertigarzneimittel zurück.
        /// </summary>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns>Auflistung der Fertigarzneimittel.</returns>
        Task<IEnumerable<ArtikelStamm>> GetFertigarzneimittelAsync(DateTime stichtag);

        /// <summary>
        /// Gibt alle Rabattvertragsartikel einer bestimmten Krankenkasse zum Stichtag zurück.
        /// </summary>
        /// <param name="kassenIK">Institutionskennzeichen (IK) der Krankenkasse.</param>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns>Auflistung der Rabattvertragsartikel für die angegebene Kasse.</returns>
        Task<IEnumerable<ArtikelStamm>> GetRabattvertragsArtikelAsync(int kassenIK, DateTime stichtag);

        /// <summary>
        /// Prüft, ob ein Artikel mit der angegebenen PZN zum Stichtag existiert.
        /// </summary>
        /// <param name="pzn">Pharmazentralnummer des zu prüfenden Artikels.</param>
        /// <param name="stichtag">Referenzdatum für die Gültigkeitsprüfung.</param>
        /// <returns><c>true</c>, wenn der Artikel existiert; andernfalls <c>false</c>.</returns>
        Task<bool> ArtikelExistiertAsync(int pzn, DateTime stichtag);
    }
}
