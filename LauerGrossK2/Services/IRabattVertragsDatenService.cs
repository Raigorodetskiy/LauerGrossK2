using System;
using System.Threading.Tasks;

namespace LauerGrossK2.Services
{
    /// <summary>
    /// Interface für den RabattVertragsDatenService.
    /// </summary>
    public interface IRabattVertragsDatenService
    {
        /// <summary>
        /// Erstellt alle Rabattvertragsdaten für einen Stichtag.
        /// </summary>
        /// <param name="stichtag">Der Referenz-Stichtag, aus dem die Gültigkeitsperiode berechnet wird.</param>
        /// <returns><c>true</c> bei Erfolg, <c>false</c> bei einem Fehler.</returns>
        Task<bool> ErstelleRabattVertragsDatenAsync(DateTime stichtag);
    }
}
