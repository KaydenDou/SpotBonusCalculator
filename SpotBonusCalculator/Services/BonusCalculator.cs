using System.Globalization;
using System.Text.RegularExpressions;
using SpotBonusCalculator.Models;

namespace SpotBonusCalculator.Services;

public class BonusCalculator
{
    // Block can be 30 minutes short and still count because of lunch.
    private const double CreditToleranceHours = 0.5;

    // Shifts must be 8 hours apart. Gave some gap because someone might not follow.
    // Past this gap means new shift.
    private const double NewShiftGapHours = 4;

    public static List<DayBonus> Calculate(List<List<string>> rows, int year, ShiftSchedule schedule,
        List<BonusPeriod> periods)
    {
        var header = rows[0];
        var dailyCol = header.IndexOf("Daily");

        // In/Out columns
        var timeCols = new List<int>();
        for (var i = 0; i < header.Count; i++)
            if (header[i] == "In" || header[i] == "Out")
                timeCols.Add(i);

        var punches = new List<DateTime>();
        var dailyHours = new Dictionary<DateTime, double>();
        DateTime? date = null;

        foreach (var row in rows.Skip(1))
        {
            // Rows without a date belong to the date above.
            var match = Regex.Match(row[0], @"(\d{1,2})/(\d{1,2})");
            if (match.Success)
            {
                var month = int.Parse(match.Groups[1].Value);
                var day = int.Parse(match.Groups[2].Value);
                if (date != null && date.Value.Month == 12 && month == 1)
                    // Pay period crosses new year.
                    year++;
                date = new DateTime(year, month, day);
            }

            if (date == null)
                continue;

            var previous = date.Value;
            foreach (var col in timeCols)
            {
                if (col >= row.Count)
                    continue;
                if (!DateTime.TryParseExact(row[col], new[] { "h:mmtt", "h:mm tt" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    continue;

                // Time earlier than the one before it is past midnight.
                var punch = date.Value + time.TimeOfDay;
                while (punch < previous)
                    punch = punch.AddDays(1);

                punches.Add(punch);
                previous = punch;
            }

            if (dailyCol < row.Count &&
                double.TryParse(row[dailyCol], NumberStyles.Float, CultureInfo.InvariantCulture, out var daily))
                dailyHours[date.Value] = dailyHours.GetValueOrDefault(date.Value) + daily;
        }

        punches.Sort();
        var results = new List<DayBonus>();

        for (var i = 0; i < punches.Count; i++)
        {
            var isNewShift = i == 0 || (punches[i] - punches[i - 1]).TotalHours > NewShiftGapHours;
            if (!isNewShift)
                continue;

            var start = punches[i];
            if (results.Any(r => r.ShiftStart.Date == start.Date))
                // One Shift Per Date, ADP puts daily on start date.
                continue;

            var paid = dailyHours.GetValueOrDefault(start.Date);
            var scheduled = schedule.WorkDays.Contains(start.DayOfWeek);

            // Scheduled day: hours beyond a normal shift.
            // Day off: whole shift, plus the unpaid lunch added back.
            var overtime = scheduled
                ? Math.Max(0, paid - schedule.ScheduledPaidHours)
                : paid + schedule.UnpaidLunchHours;

            var blocks = (int)Math.Floor((overtime + CreditToleranceHours) / 2);
            var minutesShort = (int)Math.Round(Math.Max(0, blocks * 2 - overtime) * 60);

            // Shift has to start in bonus period.
            var period = periods.FirstOrDefault(p => p.Contains(start));
            var rate = period?.AmountPerBlock ?? 0;

            results.Add(new DayBonus
            {
                ShiftStart = start,
                Scheduled = scheduled,
                PaidHours = paid,
                OvertimeHours = Math.Round(overtime, 2),
                Blocks = blocks,
                Rate = rate,
                MinutesShort = minutesShort
            });
        }

        return results;
    }
}

public class DayBonus
{
    public DateTime ShiftStart { get; set; }
    public bool Scheduled { get; set; }
    public double PaidHours { get; set; }
    public double OvertimeHours { get; set; }
    public int Blocks { get; set; }
    public decimal Rate { get; set; }
    public int MinutesShort { get; set; }

    public decimal Bonus => Blocks * Rate;
    public bool NeedsReview => MinutesShort > 5;
}