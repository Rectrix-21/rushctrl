using System.Globalization;

namespace RushCtrl.Api.Modules.Scheduling;

public sealed class ScheduleRuleException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public static class ScheduleTime
{
    public static DateTimeOffset ToUtc(string? local, string zone)
    {
        if (!DateTime.TryParseExact(local, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw new ScheduleRuleException("Enter a local date and time in yyyy-MM-ddTHH:mm format.");
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(value) || timezone.IsAmbiguousTime(value))
            throw new ScheduleRuleException("That local time is skipped or repeated by daylight saving. Choose an unambiguous time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(value, timezone));
    }

    public static (DateTimeOffset Start, DateTimeOffset End) Interval(string? start, string? end, string zone)
    {
        var a = ToUtc(start, zone);
        var b = ToUtc(end, zone);
        if (b <= a) throw new ScheduleRuleException("End time must be after start time.");
        if (b - a > TimeSpan.FromDays(31)) throw new ScheduleRuleException("Use an interval of 31 days or less.");
        return (a, b);
    }

    public static (DateTimeOffset Start, DateTimeOffset End) Week(string? week, string zone)
    {
        if (!DateOnly.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.DayOfWeek != DayOfWeek.Monday || date.Year is < 2000 or > 2100)
            throw new ScheduleRuleException("Choose a Monday between 2000 and 2100 for the schedule week.");
        return (ToUtc(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00", zone), ToUtc(date.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00", zone));
    }
}

