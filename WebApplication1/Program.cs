using Expenses;
using ExpensesApi.Data;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;


public partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data source=expenses.db"));
        builder.Services.AddScoped<ExpenseAnalyticsService>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin();
            });
        });

        builder.Services.Configure<RouteOptions>(options =>
        {
            options.SetParameterPolicy<RegexInlineRouteConstraint>("regex");
        });
        var app = builder.Build();
        app.UseCors("Frontend");
        app.UseSwagger();
        app.UseSwaggerUI();

        var group = app.MapGroup("/expenses");

        group.MapGet("/", async (AppDbContext db, DateTime? from, DateTime? to, decimal? min, decimal? max, string? category, string? search) => {
            
                var query = db.Expenses.AsQueryable();
                
            if (from is not null)
                query = query.Where(e => e.Date >= from);

            if (to is not null)
                query = query.Where(e => e.Date <= to);

            if (min is not null)
                query = query.Where(e => e.Value > min);

            if (max is not null)
                query = query.Where(e => e.Value < max);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => e.Title.Contains(search));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(e => e.Category == category);

            return await query.ToListAsync();
            });

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var expense = await db.Expenses.FindAsync(id);
            if (expense is null)
                return Results.NotFound();
            return Results.Ok(expense);

        });

        group.MapPost("/", async (CreateExpenseDto expDto, AppDbContext db) =>
        {
            Expense exp = new Expense();

            exp.Title = expDto.Title;
            exp.Value = expDto.Value;
            exp.Date = expDto.Date;
            exp.Category = expDto.Category;


            db.Expenses.Add(exp);
            await db.SaveChangesAsync();

            return Results.Created($"/expenses/{exp.Id}", exp);
        });

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            var expense = await db.Expenses.FindAsync(id);

            if (expense is null)
                return Results.NotFound();

            db.Expenses.Remove(expense);
            await db.SaveChangesAsync();
            return Results.NoContent();

        });

        group.MapPut("/{id}", async (int id, UpdateExpenseDto updExp, AppDbContext db) =>
        {
            var expense = await db.Expenses.FindAsync(id);
            if (expense is null)
                return Results.NotFound();

            expense.Title = updExp.Title;
            expense.Value = updExp.Value;
            expense.Date = updExp.Date;
            expense.Category = updExp.Category;

            await db.SaveChangesAsync();
            return Results.Ok(expense);
        });

        group.MapGet("/stats", async (AppDbContext db) =>
        {
            ExpenseStats expStat = new ExpenseStats(db);

            return Results.Ok(await expStat.GetStats());
        });

        group.MapGet("/biggest-spending-weekly", async (AppDbContext db, ExpenseAnalyticsService analytics) =>
        {
            var weekly = await analytics.GetSpendingByPeriod(PeriodType.Week, true);

            return Results.Ok(weekly);
        });

        group.MapGet("/smallest-spending-weekly", async (AppDbContext db, ExpenseAnalyticsService analytics) =>
        {
            var weekly = await analytics.GetSpendingByPeriod(PeriodType.Week, false);

            return Results.Ok(weekly);
        });

        group.MapGet("/biggest-spending-monthly", async (AppDbContext db, ExpenseAnalyticsService analytics) =>
        {
            var monthly = await analytics.GetSpendingByPeriod(PeriodType.Month, true);

            return Results.Ok(monthly);
        });

        group.MapGet("/smallest-spending-monthly", async (AppDbContext db, ExpenseAnalyticsService analytics) =>
        {
            var monthly = await analytics.GetSpendingByPeriod(PeriodType.Month, false);

            return Results.Ok(monthly);
        });

        group.MapGet("/top", async (AppDbContext db) =>
        {
            var top = await db.Expenses.OrderByDescending(e => e.Value).Take(5).ToListAsync();
            
            return Results.Ok(top);
        });

        group.MapGet("/top-categories", async (AppDbContext db) =>
        {
            var top_categories = await db.Expenses.GroupBy(e => e.Category).Select(g =>
            new
            {
                Category = g.Key,
                Total = g.Sum(e => e.Value),
                Count = g.Count()
            }).OrderByDescending(g => g.Total).ToListAsync();

            return Results.Ok(top_categories);
        });

        group.MapGet("/biggest-spending-day", async (AppDbContext db, ExpenseAnalyticsService analytics) =>
        {
            var biggest_day = await analytics.GetSpendingByPeriod(PeriodType.Day, true);
            return Results.Ok(biggest_day);
        });


        group.MapGet("/smallest-spending-day", async (AppDbContext db, ExpenseAnalyticsService analytics) =>
        {
            var biggest_day = await analytics.GetSpendingByPeriod(PeriodType.Day, false);
            return Results.Ok(biggest_day);
        });


        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            var generator = new GenerateData();

            if (!db.Expenses.Any())
                await generator.Generate(db);
        }
        app.Run();
    }
}

public class Expense
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; } = "";
    public Expense() { }

    public Expense(int id, string title, decimal value, DateTime date, string category)
    {
        Id = id;
        Title = title;
        Value = value;
        Date = date;
        Category = category;
    }
}

public class CreateExpenseDto
{
    public string Title { get; set; } = "";
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; } = "";

}

public class UpdateExpenseDto
{
    public string Title { get; set; } = "";
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; } = "";

}

public class ExpenseStatsDto
{
    public decimal Total { get; set; }
    public decimal Avg { get; set; }
    public int Count { get; set; }
    public decimal Max { get; set; }
    public decimal Min { get; set; }
}

public class ExpenseStats
{
    private readonly AppDbContext _db;

    public ExpenseStats(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ExpenseStatsDto> GetStats()
    {
        int count = await _db.Expenses.CountAsync();
        if (count == 0)
        {
            return new ExpenseStatsDto
            {
                Total = 0,
                Avg = 0,
                Count = 0,
                Max = 0,
                Min = 0
            };
        }

        return new ExpenseStatsDto
        {
            Total = await _db.Expenses.SumAsync(e => e.Value),
            Avg = await _db.Expenses.AverageAsync(e => e.Value),
            Count = count,
            Max = await _db.Expenses.MaxAsync(e => e.Value),
            Min = await _db.Expenses.MinAsync(e => e.Value)
        };   
    }


}

[JsonSerializable(typeof(List<Expense>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
