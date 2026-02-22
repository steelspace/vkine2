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
        { "CS", "Československo" },
        { "SU", "SSSR" },
        { "YU", "Jugoslávie" }
    };

    public string GetCountryName(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        
        var upperCode = code.Trim().ToUpperInvariant();

        try
        {
            // .NET's RegionInfo.DisplayName often returns the NativeName on macOS/Linux.
            // To get a truly localized country name in the OS/Browser language without a mapping table,
            // we can leverage CultureInfo.DisplayName, which includes the localized region in parentheses.
            // Example: "pl-PL" in English is "Polish (Poland)", in Czech is "polština (Polsko)".
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

            // Fallback for cases where the parentheses format might differ or lookups fail
            return new RegionInfo(upperCode).DisplayName;
        }
        catch (ArgumentException)
        {
            // Fallback to historical map or the code itself
            return _historicalMap.TryGetValue(upperCode, out var name) ? name : upperCode;
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
