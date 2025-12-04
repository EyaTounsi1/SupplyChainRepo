using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PartTracker.Components;
using PartTracker.Models;
using PartTracker;
using MudBlazor.Services;
// using PartTracker.Shared.Variables;
// using PartTracker.Shared.Functions;
// using PartTracker.Configurations;
// using PartTracker.Shared.Services;
// using PartTracker.Components.Pages.PartTracker.Avdelningar.TA.Cyclic;


var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Debug);

// Database
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 36)))
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment());

    // options.AddInterceptors(sp.GetRequiredService<TopasMaterialInterceptor>());
});
builder.Services.AddScoped<AnotherSnowflakeService>();
// Blazor and Session Storage
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<ProtectedSessionStorage>();
// builder.Services.AddScoped<UserSession>();
// builder.Services.AddScoped<GlobalVariables>();
// builder.Services.AddScoped<DataTimer>();
// builder.Services.AddSingleton<DashboardTimerService>();
builder.Services.AddControllers();
builder.Services.AddMudServices();
// builder.Services.AddSingleton<TopasMaterialInterceptor>();
builder.Services.AddHttpClient();
// builder.Services.AddScoped<TaPartSearchEngine>();

// Excel import service
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddHostedService<ExcelSyncBackgroundService>();

// Safety stock service
builder.Services.AddScoped<ISafetyStockService, SafetyStockService>();
builder.Services.AddScoped<AnotherSnowflakeService>();

// Forecast service
builder.Services.AddScoped<IForecastService, ForecastService>();

// Services
// builder.Services.AddScoped<LaunchService>();

// Hub for SignalR
builder.Services.AddSignalR();

var supportedCultures = new[] { new CultureInfo("sv-SE") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("sv-SE");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

// Apply localization
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

// Ensure DB is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.EnsureCreated();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database connection failed: {ex.Message}");
    }
}

// Error pages
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Errors");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Exception logging middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // var error = new SystemError
        // {
        //     Message = ex.Message,
        //     StackTrace = ex.StackTrace ?? "",
        //     Source = ex.Source ?? "",
        //     Path = context.Request.Path,
        //     TimeStamp = DateTime.Now,
        //     Handled = false
        // };

        // try
        // {
        //     db.SystemError.Add(error); // Singular DbSet<SystemError>
        //     await db.SaveChangesAsync();
        // }
        // catch (Exception logEx)
        // {
        //     Console.WriteLine("Failed to log exception: " + logEx.Message);
        // }
        Console.WriteLine($"Exception: {ex.Message}");

        if (app.Environment.IsDevelopment())
        {
            throw;
        }
        else
        {
            context.Response.Redirect("/Errors");
        }
    }
});

// Middleware pipeline
app.UseHttpsRedirection();
app.MapControllers();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
// app.MapHub<PartTracker.Shared.Hubs.AppHub>("/hub/app");

app.Run();