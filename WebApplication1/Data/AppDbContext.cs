namespace ExpensesApi.Data;

using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Expense> Expenses => Set<Expense>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
