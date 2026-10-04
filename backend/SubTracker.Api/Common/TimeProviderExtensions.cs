namespace SubTracker.Api.Common;

public static class TimeProviderExtensions
{
    // Dagens datum i providerns lokala tidszon. För appen är det svensk tid (se SwedishTimeProvider).
    public static DateOnly GetToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
