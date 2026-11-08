using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Tests;

public sealed class SchedulingRulesTests
{
    [Theory]
    [InlineData("2027-03-08", 167)]
    [InlineData("2027-11-01", 169)]
    public void LocalWeeksAccountForDaylightSaving(string monday, int hours)
    {
        var interval = ScheduleTime.Week(monday, "America/Toronto");
        Assert.Equal(hours, (interval.End - interval.Start).TotalHours);
    }
    [Fact]
    public void LocationTimeIsConvertedToUtcAndNotTheServersLocalZone()
    {
        Assert.Equal(DateTimeOffset.Parse("2026-11-09T13:00:00Z"), ScheduleTime.ToUtc("2026-11-09T08:00", "America/Toronto"));
    }

    [Theory]
    [InlineData("2027-03-14T02:30")]
    [InlineData("2027-11-07T01:30")]
    [InlineData("2026-11-09T08:00Z")]
    [InlineData("")]
    public void InvalidAmbiguousAndNonLocalInputIsRejected(string local)
    {
        Assert.Throws<ScheduleRuleException>(() => ScheduleTime.ToUtc(local, "America/Toronto"));
    }
}
