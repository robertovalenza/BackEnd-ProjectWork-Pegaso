using Backend_Banca_Aurora.Models;

namespace Backend_Banca_Aurora.Services;

public interface IDecisionService
{
    DecisionResultDto Decide(Customer c, LoanApplication app);
}

public class DecisionService : IDecisionService
{
    public DecisionResultDto Decide(Customer c, LoanApplication app)
    {
        if (app.Amount < 500 || app.Amount > 50000 || app.Months < 6 || app.Months > 84)
            return new("DECLINED", null, null, 0);

        var ratio = (double)c.IncomeMonthly / (double)app.Amount;
        var score = (int)Math.Clamp(400 + ratio * 400, 400, 850);

        var apr = (decimal)Math.Round(12 - Math.Min(ratio * 5, 8), 2);
        apr = Math.Clamp(apr, 3.5m, 14m);

        var r = (double)apr / 100 / 12;
        var n = app.Months;
        var A = (decimal)Math.Round((double)app.Amount * (r * Math.Pow(1 + r, n)) / (Math.Pow(1 + r, n) - 1), 2);

        if (c.IncomeMonthly < A * 2) return new("DECLINED", apr, A, score);
        return new("APPROVED", apr, A, score);
    }
}
