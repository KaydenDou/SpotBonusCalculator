namespace SpotBonusCalculator.Models;

public class ShiftSchedule
{
    public string Name { get; set; } = "";
    public HashSet<DayOfWeek> WorkDays { get; set; } = new();
    public double ScheduledPaidHours { get; set; }
    public double UnpaidLunchHours { get; set; }

    public static ShiftSchedule ThirdShift = new()
    {
        Name = "3rd shift",
        WorkDays = new HashSet<DayOfWeek>
        {
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        },
        ScheduledPaidHours = 9.0,
        UnpaidLunchHours = 0.5
    };
}