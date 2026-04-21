using Microsoft.EntityFrameworkCore;
using Juego.Data;
using Juego.Hubs;
using Juego.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Entity Framework Core + SQLite
builder.Services.AddDbContext<JuegoDbContext>(options =>
    options.UseSqlite("Data Source=juego.db"));

// Game state manager (Singleton)
builder.Services.AddSingleton<GameStateManager>();

var app = builder.Build();

// Auto-create database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Juego}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<GameHub>("/gameHub");

app.Run();
