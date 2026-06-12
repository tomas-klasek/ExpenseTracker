using ExpensesApi.Data;
using System.Xml.Serialization;


namespace Expenses
{
    public class GenerateData
    {
        private readonly Random rn = new Random();

        public async Task Generate(AppDbContext db)
        { 
            string[] merchants = { "alza", "McDonalds", "Costa", "Albert", "Lidl" };
            for (int i = 0; i< 500; i++) {
                Expense expense = new Expense { 
                    Title = merchants[rn.Next(merchants.Length)],
                    Value = rn.Next(20, 1000),
                    Date = DateTime.Now.AddDays(-rn.Next(0, 365))
                };
                db.Expenses.Add(expense);
                }
                await db.SaveChangesAsync();
            }
    }
}
