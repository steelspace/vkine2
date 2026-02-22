using System.Globalization;

namespace vkine.Services;

/// <summary>
/// Provides lookups for country names by their ISO 3166-1 alpha-2 codes using RegionInfo.
/// </summary>
public interface ICountryLookupService
{
    /// <summary>
    /// Gets the full name of a country by its ISO code.
    /// </summary>
    /// <param name="code">The two-letter ISO country code (e.g., "US", "CZ").</param>
    /// <returns>The country name if found; otherwise, the code itself.</returns>
    string GetCountryName(string code);

    /// <summary>
    /// Gets the full names for a list of ISO country codes.
    /// </summary>
    /// <param name="codes">A list of ISO country codes.</param>
    /// <returns>A list of country names.</returns>
    List<string> GetCountryNames(IEnumerable<string> codes);
}

public class CountryLookupService : ICountryLookupService
{
    // Mapping for historical codes not supported by modern RegionInfo
    private static readonly Dictionary<string, string> _historicalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "CS", "Československo" },   // Czechoslovakia (1918–1992) - often confused with CS (Serbia & Montenegro)
        { "DD", "Východní Německo" }, // East Germany (DDR)
        { "SU", "SSSR" },             // Soviet Union
        { "YU", "Jugoslávie" },       // Yugoslavia
        { "ZR", "Zair" },             // Zaire (now Congo, Dem. Rep.)
        { "TP", "Východní Timor" },   // East Timor (retired code, now TL)
        { "BU", "Barma" },            // Burma (now Myanmar, MM)
        { "DY", "Dahome" }            // Dahomey (now Benin, BJ)
    };

    public string GetCountryName(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        
        var upperCode = code.Trim().ToUpperInvariant();

        // CHECK HISTORICAL MAP FIRST
        // Modern .NET interpretation of "CS" is "Serbia and Montenegro" (the transition period 
        // before RS/ME splitting), which is likely why it's showing "Srbsko" or similar.
        if (_historicalMap.TryGetValue(upperCode, out var historicalName))
        {
            return historicalName;
        }

        try
        {
            var currentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cultureName = $"{currentLanguage}-{upperCode}";
            var culture = new CultureInfo(cultureName);
            var displayName = culture.DisplayName;

            var start = displayName.IndexOf('(');
            var end = displayName.LastIndexOf(')');

            if (start != -1 && end > start)
            {
                return displayName.Substring(start + 1, end - start - 1);
            }

            return new RegionInfo(upperCode).DisplayName;
        }
        catch (ArgumentException)
        {
            return upperCode;
        }
    }

    public List<string> GetCountryNames(IEnumerable<string> codes)
    {
        if (codes == null) return new List<string>();
        return codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(GetCountryName)
            .ToList();
    }
}
