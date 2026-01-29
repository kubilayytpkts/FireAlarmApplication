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

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// ============================================
// REDIS
// ============================================
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis connection string not found");

    var connectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);
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

// ============================================
// HANGFIRE
// ============================================
builder.Services.AddHangfire(configuration =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions
        {
            QueuePollInterval = TimeSpan.FromSeconds(15),
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

// ============================================
// CORS
// ============================================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============================================
// MODULES
// ============================================
var modules = new List<IFireGuardModule>
{
    new FireDetectionModule(),
    new AlertSystemModule(),
    new UserManagementModule()
};

builder.AddFireGuardModules(modules.ToArray());

// ============================================
// BUILD APP
// ============================================
var app = builder.Build();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;

// ============================================
// MIDDLEWARE PIPELINE - DOĞRU SIRALAMA
// ============================================

// 1. Exception handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// 2. HTTPS & Static files
app.UseHttpsRedirection();
app.UseStaticFiles();

// 3. CORS
app.UseCors();

// 4. Swagger (Dev only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================
// ✅ KRİTİK SIRALAMA - SADECE BİR KERE!
// ============================================
app.UseRouting();         // 5️⃣ ROUTING (SADECE BİR KERE!)

app.UseAuthentication();  // 6️⃣ AUTHENTICATION
app.UseAuthorization();   // 7️⃣ AUTHORIZATION
// ============================================

// 8. Endpoints
app.MapFireGuardModules();

// 9. Hangfire
app.UseHangfireServer();
app.UseHangfireDashboard("/hangfire");

// ============================================
// DATA SEEDING
// ============================================
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    try
    {
        await app.SeedFireGuardModulesAsync();
        app.Logger.LogInformation("✅ Module seeding completed");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ Module seeding failed");
    }
}

app.Run();