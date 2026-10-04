namespace SubTracker.Api.Common;

/// <summary>
/// Appen är svensk, så "idag" och månadsgränser följer svensk tid oavsett vilken tidszon servern går i
/// (App Service kör UTC). Övrigt är systemets klocka, så tiden i sig är oförändrad.
/// </summary>
public sealed class SwedishTimeProvider : TimeProvider
{
    private const string ZoneId = "Europe/Stockholm";

    public SwedishTimeProvider()
    {
        try
        {
            LocalTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Minimala containrar kan sakna tidszonsdatabasen. Då går appen vidare i UTC och Program.cs loggar en varning.
            LocalTimeZone = TimeZoneInfo.Utc;
            UsesFallbackZone = true;
        }
    }

    public override TimeZoneInfo LocalTimeZone { get; }

    public bool UsesFallbackZone { get; }
}
