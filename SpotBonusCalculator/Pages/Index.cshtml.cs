using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotBonusCalculator.Models;
using SpotBonusCalculator.Services;

namespace SpotBonusCalculator.Pages;

public class IndexModel : PageModel
{
    [BindProperty] public List<IFormFile> Timecards { get; set; } = new();
    [BindProperty] public List<BonusPeriodInput> Periods { get; set; } = new();
    public List<EmployeeResult> Results { get; set; } = new();

    public void OnGet()
    {
        var today = DateTime.Today;
        Periods.Add(new BonusPeriodInput { Start = DateTime.Today, End = DateTime.Today });
    }

    public async Task OnPostAsync()
    {
        // turn the form inputs into bonus periods, skipping incomplete ones
        var periods = new List<BonusPeriod>();
        foreach (var p in Periods)
            if (p.Start != null && p.End != null && p.End > p.Start)
                periods.Add(new BonusPeriod
                {
                    Start = p.Start.Value,
                    End = p.End.Value,
                    AmountPerBlock = p.AmountPerBlock
                });

        // timecards have no year, so take it from the first period
        var year = periods.Count > 0 ? periods[0].Start.Year : DateTime.Now.Year;

        foreach (var file in Timecards)
        {
            var result = new EmployeeResult { Name = file.FileName };

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                var card = new PdfHandling().ReadTimecard(ms.ToArray());
                result.Name = card.EmployeeName;

                if (card.Rows.Count < 2)
                {
                    result.Error = "No timecard table found.";
                }
                else
                {
                    var days = BonusCalculator.Calculate(card.Rows, year, ShiftSchedule.ThirdShift, periods);
                    result.BonusDays = days.Where(d => d.Blocks > 0 && d.Rate > 0).ToList();
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            Results.Add(result);
        }
    }
}

public class BonusPeriodInput
{
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public decimal AmountPerBlock { get; set; } = 50;
}

public class EmployeeResult
{
    public string Name { get; set; } = "";
    public string? Error { get; set; }
    public List<DayBonus> BonusDays { get; set; } = new();

    public int TotalBlocks => BonusDays.Sum(d => d.Blocks);
    public decimal TotalBonus => BonusDays.Sum(d => d.Bonus);
}