using vkine.Models;

namespace vkine.Services;

public interface IPremiereService
{
    /// <summary>
    /// Returns upcoming premieres (premiere_date >= today), ordered by date ascending.
    /// </summary>
    Task<List<PremiereDocument>> GetUpcomingPremieresAsync();
}
