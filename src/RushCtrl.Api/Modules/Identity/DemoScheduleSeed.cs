using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Modules.Identity;

internal static class DemoScheduleSeed
{
    public static async Task RunAsync(RushCtrlDbContext db, DateTimeOffset now)
    {
        var org = DevelopmentSeed.OrganizationId;
        var location = DevelopmentSeed.LocationId;
        var employee = DevelopmentSeed.EmployeeId;
        var manager = DevelopmentSeed.ManagerId;
        var barista = Guid.Parse("10000000-0000-0000-0000-000000000005");
        var lead = Guid.Parse("10000000-0000-0000-0000-000000000006");
        if (!await db.Skills.IgnoreQueryFilters().AnyAsync(x => x.Id == barista)) db.Skills.Add(new Skill { Id = barista, OrganizationId = org, Name = "Barista" });
        if (!await db.Skills.IgnoreQueryFilters().AnyAsync(x => x.Id == lead)) db.Skills.Add(new Skill { Id = lead, OrganizationId = org, Name = "Shift lead" });
        foreach (var (person, skill) in new[] { (employee, barista), (manager, lead), (manager, barista) })
            if (!await db.EmployeeSkills.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == org && x.UserId == person && x.SkillId == skill))
                db.EmployeeSkills.Add(new EmployeeSkill { OrganizationId = org, UserId = person, SkillId = skill });
        await db.SaveChangesAsync();
        if (await db.Shifts.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == org)) return;
        var zone = "America/Toronto";
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7) + 7);
        DateTimeOffset At(int day, int hour) => ScheduleTime.ToUtc(monday.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + $"T{hour:00}:00", zone);
        foreach (var person in new[] { employee, manager })
            for (var day = 0; day < 7; day++)
                db.AvailabilityWindows.Add(new AvailabilityWindow { OrganizationId = org, LocationId = location, UserId = person, StartsAt = At(day, 8), EndsAt = At(day, 20) });
        db.Shifts.AddRange(
            new Shift { OrganizationId = org, LocationId = location, SkillId = barista, StartsAt = At(0, 8), EndsAt = At(0, 12), AssigneeUserId = employee },
            new Shift { OrganizationId = org, LocationId = location, SkillId = lead, StartsAt = At(0, 8), EndsAt = At(0, 12), AssigneeUserId = manager },
            new Shift { OrganizationId = org, LocationId = location, SkillId = barista, StartsAt = At(1, 8), EndsAt = At(1, 12) });
        db.StaffingRequirements.Add(new StaffingRequirement { OrganizationId = org, LocationId = location, SkillId = barista, StartsAt = At(0, 8), EndsAt = At(0, 16), RequiredCount = 2 });
        await db.SaveChangesAsync();
    }
}
