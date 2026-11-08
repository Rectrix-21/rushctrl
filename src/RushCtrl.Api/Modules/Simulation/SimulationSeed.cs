using System.Text.Json;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Modules.Simulation;

public static class SimulationSeed
{
    public const string Zone = "America/Toronto";
    public static DateOnly FutureMonday(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, Zone).DateTime);
        return today.AddDays(7 - (((int)today.DayOfWeek + 6) % 7));
    }

    public static DateTimeOffset At(SimulationWorkspace lab, int day, string time) =>
        ScheduleTime.ToUtc(lab.Week.AddDays(day).ToString("yyyy-MM-dd") + "T" + time, Zone);

    public static void Restore(RushCtrlDbContext db, SimulationWorkspace lab)
    {
        var members = JsonSerializer.Deserialize<Guid[]>(lab.MemberIds)!;
        Shift Shift(int day, string start, string end, Guid? person, bool other = false)
        {
            var shift = new Shift { OrganizationId = lab.OrganizationId, LocationId = other ? lab.OtherLocationId : lab.LocationId,
                SkillId = lab.SkillId, StartsAt = At(lab, day, start), EndsAt = At(lab, day, end), AssigneeUserId = person };
            db.Shifts.Add(shift);
            return shift;
        }
        lab.AbsenceShiftId = Shift(4, "14:00", "18:00", members[0]).Id;
        Shift(4, "14:00", "18:00", members[1]);
        lab.StandbyShiftId = Shift(4, "14:00", "18:00", null).Id;
        Shift(0, "08:00", "12:00", members[2]);
        Shift(0, "08:00", "16:00", members[3]);
        Shift(4, "14:00", "18:00", members[6], other: true);
        for (var day = 0; day < 4; day++) Shift(day, "08:00", "17:30", members[7]);
        for (var i = 0; i < members.Length; i++)
            db.AvailabilityWindows.Add(new AvailabilityWindow { OrganizationId = lab.OrganizationId, LocationId = lab.LocationId,
                UserId = members[i], StartsAt = At(lab, 4, "14:00"), EndsAt = At(lab, 4, i == 5 ? "16:00" : "18:00") });
        var target = new StaffingRequirement { OrganizationId = lab.OrganizationId, LocationId = lab.LocationId, SkillId = lab.SkillId,
            StartsAt = At(lab, 4, "14:00"), EndsAt = At(lab, 4, "18:00"), RequiredCount = 2 };
        db.StaffingRequirements.Add(target);
        lab.TargetId = target.Id;
        lab.Applied = false;
        lab.Pristine = true;
    }
}
