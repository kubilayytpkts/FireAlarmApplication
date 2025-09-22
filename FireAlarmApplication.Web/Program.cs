using FireAlarmApplication.Web.Modules.AlertSystem.Main_Operations;
using FireAlarmApplication.Web.Modules.FireDetection.Modules;
using FireAlarmApplication.Web.Shared.Common;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FireGuardOptions>(builder.Configuration.GetSection(FireGuardOptions.SectionName));
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
//var fireDetectionDB = builder.Configuration.GetConnectionString("FireDetectionDB");

//builder.Services.AddDbContext<FireDetectionDbContext>(options =>
//    options.UseNpgsql(fireDetectionDB, npgsql =>
//    {
//        npgsql.UseNetTopologySuite();
//        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "fire_detection");
//    }));

//builder.Services.AddDbContext<AlertSystemDbContext>(options =>
//    options.UseNpgsql(connectionString, npgsql =>
//    {
//        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "alert_system");
//    }));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis connection string not found");

    var connectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);

    // Redis connection test
    var database = connectionMultiplexer.GetDatabase();
    var logger = provider.GetRequiredService<ILogger<Program>>();

    try
    {
        database.StringSet("startup_test", DateTime.UtcNow.ToString(), TimeSpan.FromSeconds(10));
        logger.LogInformation("Redis connection successful");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Redis connection failed");
    }

    return connectionMultiplexer;
});

builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddHangfire(configuration =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions
        {
            QueuePollInterval = TimeSpan.FromSeconds(15), // kısa tutabilirsin test için
            PrepareSchemaIfNecessary = true,
            SchemaName = "hangfire"
        });
});
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1;
    options.Queues = new[] { "default", "fire-sync" };
});

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var modules = new List<IFireGuardModule>
{
    new FireDetectionModule(),
    new AlertSystemModule(),
    new UserManagementModule()
};

builder.AddFireGuardModules(modules.ToArray());

var app = builder.Build();
using var scospe = app.Services.CreateScope();
var services = scospe.ServiceProvider;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapFireGuardModules();
app.UseHangfireServer();
app.MapFallbackToPage("/_Host");
app.UseHangfireDashboard("/hangfire");

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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.Run();
