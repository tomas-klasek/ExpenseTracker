using ExpensesApi.Data;
using Microsoft.EntityFrameworkCore;

namespace Expenses
{
    public class ExpenseAnalyticsService
    {
        private readonly AppDbContext _db;

        public ExpenseAnalyticsService(AppDbContext db)
        {
            _db = db;
        }

        private static IQueryable<PeriodTotal> GroupByPeriod(
            IQueryable<Expense> query,
            PeriodType period)
        {
            return period switch
            {
                PeriodType.Day =>
                    query.GroupBy(e => e.Date.Date)
                         .Select(g => new PeriodTotal
                         {
                             Date = g.Key,
                             Total = g.Sum(x => x.Value)
                         }),

                PeriodType.Week =>
                    query.GroupBy(e => new { e.Date.Year, Week = e.Date.DayOfYear / 7 })
                         .Select(g => new PeriodTotal
                         {
                             Date = new DateTime(g.Key.Year, 1, 1).AddDays(g.Key.Week * 7),
                             Total = g.Sum(x => x.Value)
                         }),

                PeriodType.Month =>
                    query.GroupBy(e => new { e.Date.Year, e.Date.Month })
                         .Select(g => new PeriodTotal
                         {
                             Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                             Total = g.Sum(x => x.Value)
                         }),

                PeriodType.Year =>
                    query.GroupBy(e => e.Date.Year)
                         .Select(g => new PeriodTotal
                         {
                             Date = new DateTime(g.Key, 1, 1),
                             Total = g.Sum(x => x.Value)
                         }),

                _ => throw new ArgumentOutOfRangeException(nameof(period))
            };
        }

        public async Task<PeriodTotal?> GetSpendingByPeriod(PeriodType period, bool biggest)
        {
            var group = GroupByPeriod(_db.Expenses, period);

            var query = biggest ? group.OrderByDescending(x => x.Total) :
                group.OrderBy(x => x.Total);

            return await query.FirstOrDefaultAsync();

            }

        }

    public enum PeriodType
    {
        Day,
        Week,
        Month,
        Year
    }

    public class PeriodTotal
    {
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
    }

}
