using FireAlarmApplication.Web.Shared.Common;
using FireAlarmApplication.Web.Shared.Infrastructure;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FireGuardOptions>(builder.Configuration.GetSection(FireGuardOptions.SectionName));
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string not found");

    var connectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);

    // Redis connection test
    var database = connectionMultiplexer.GetDatabase();
    var logger = provider.GetRequiredService<ILogger<Program>>();

    try
    {
        database.StringSet("startup_test", DateTime.UtcNow.ToString(), TimeSpan.FromSeconds(10));
        logger.LogInformation("? Redis connection successful");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "? Redis connection failed");
    }

    return connectionMultiplexer;
});

builder.Services.AddScoped<IRedisService, RedisService>();

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var modules = new List<IFireGuardModule>
{
    // new FireDetectionModule(),
    // new UserManagementModule(),
    // new AlertSystemModule()
};

builder.AddFireGuardModules(modules.ToArray());

var app = builder.Build();

// ??? Configure HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapFireGuardModules();
app.MapRazorPages();
app.MapBlazorHub();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    try
    {
        await app.SeedFireGuardModulesAsync();
        app.Logger.LogInformation("? Module seeding completed");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "? Module seeding failed");
    }
}



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}




app.MapFallbackToPage("/_Host");

app.Run();
