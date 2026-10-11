namespace SpotBonusCalculator.Models;

public class BonusPeriod
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public decimal AmountPerBlock { get; set; }

    public bool Contains(DateTime time)
    {
        return time >= Start && time <= End;
    }
}