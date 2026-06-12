using Expenses;
using ExpensesApi.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore;
using System.Numerics;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;


public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data source=expenses.db"));

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

        group.MapGet("/", async (AppDbContext db, DateTime? from, DateTime? to, decimal? min, decimal? max, string? search) => {
            
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

            await db.SaveChangesAsync();
            return Results.Ok(expense);
        });

        group.MapGet("/stats", async (AppDbContext db) =>
        {
            ExpenseStats expStat = new ExpenseStats(db);

            return Results.Ok(await expStat.GetStats());
        });

        group.MapGet("/weekly", async (AppDbContext db) =>
        {
            var weekly = await db.Expenses.GroupBy(e => e.Date.Date.AddDays(-(int)e.Date.DayOfWeek)).Select(g =>
            new
            {
                WeekStart = g.Key,
                Value = g.Sum(e => e.Value),
                Count = g.Count()
            }).OrderBy(x => x.WeekStart).ToListAsync();

            return Results.Ok(weekly);
        });

        group.MapGet("/monthly", async (AppDbContext db) =>
        {
            var monthly = await db.Expenses.GroupBy(e => new { e.Date.Year, e.Date.Month }).Select(g =>
            new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Value = g.Sum(e => e.Value),
                Count = g.Count()
            }).OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync();

            return Results.Ok(monthly);
        });

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            var generator = new GenerateData();

            if (!db.Expenses.Any())
                generator.Generate(db);
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

    public Expense() { }

    public Expense(int id, string title, decimal value, DateTime date)
    {
        Id = id;
        Title = title;
        Value = value;
        Date = date;
    }
}

public class CreateExpenseDto
{
    public string Title { get; set; } = "";
    public decimal Value { get; set; }
    public DateTime Date { get; set; }

}

public class UpdateExpenseDto
{
    public string Title { get; set; } = "";
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
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
