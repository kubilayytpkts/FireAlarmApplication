using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Web.Modules.FireDetection.Models;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class MtgFireService : IMtgFireService
    {
        private readonly HttpClient _httpClient;
        private readonly FireGuardOptions _fireGuardOptions;
        private readonly ILogger<MtgFireService> _logger;
        private readonly IRiskCalculationService _riskCalculationService;
        private readonly IOsmGeoDataService _osmGeoDataService;

        private string _accessToken;
        private DateTime _tokenExpiryDate;

        public MtgFireService(HttpClient httpClient, IOptions<FireGuardOptions> fireGuardOptions, ILogger<MtgFireService> logger, IRiskCalculationService riskCalculationService, IOsmGeoDataService osmGeoDataService)
        {
            _httpClient = httpClient;
            _fireGuardOptions = fireGuardOptions.Value;
            _logger = logger;
            _riskCalculationService = riskCalculationService;
            _osmGeoDataService = osmGeoDataService;
        }

        public async Task<List<Models.FireDetection>> FetchActiveFiresAsync(string area = null, int minutesRange = 43200)
        {
            try
            {
                var fires = new List<Models.FireDetection>();
                await EnsureTokenAsync();
                var products = await SearchProductsAsync(area, minutesRange);


                if (products == null || products.Count == 0)
                {
                    _logger.LogInformation("No MTG fire products found in last {Minutes} minutes", minutesRange);
                    return new List<Models.FireDetection>();
                }

                var productsToProcess = products
                   .OrderByDescending(p => p.TimeStart)
                   .Where((p, index) => index % 20 == 0)
                   .Take(5)
                   .ToList();

                foreach (var item in productsToProcess)
                {
                    var fire = await DownloadAndParseProductAsync(item, area);
                    fires.AddRange(fire);

                    if (fires.Count > 0)
                    {
                        _logger.LogInformation("Found fires! Continuing to get more...");
                    }

                }

                return fires;

            }
            catch (Exception)
            {

                throw;
            }
        }

        public Task<List<Models.FireDetection>> FetchFiresForRegionAsync(double minLat, double minLng, double maxLat, double maxLng, int minutesRange = 30)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsApiHealthyAsync()
        {
            throw new NotImplementedException();
        }


        // HELPER METHODS 

        private async Task EnsureTokenAsync()
        {

            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiryDate)
                return;

            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{_fireGuardOptions.Eumetsat.ConsumerKey}:{_fireGuardOptions.Eumetsat.ConsumerSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_fireGuardOptions.Eumetsat.BaseUrl}/token");
            request.Headers.Add("Authorization", $"Basic {auth}");
            request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Models.TokenResponse>(json);

            _accessToken = tokenResponse?.access_token;

            _tokenExpiryDate = tokenResponse != null ? DateTime.UtcNow.AddMinutes(50) : DateTime.MinValue; // Token 1 saat geçerli, 50 dk kullan
        }

        private async Task<List<Models.MtgProduct>> SearchProductsAsync(string bbox, int minutesRange)
        {
            var now = DateTime.UtcNow;
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMinutes(-minutesRange);

            minutesRange = 86400;

            var url = $"{_fireGuardOptions.Eumetsat.BaseUrl}/data/search-products/os" +
                      $"?pi={_fireGuardOptions.Eumetsat.Collection}" +
                      $"&bbox={bbox}" +
                      $"&dtstart={startTime:yyyy-MM-ddTHH:mm:ssZ}" +
                      $"&dtend={endTime:yyyy-MM-ddTHH:mm:ssZ}" +
                      $"&si=0&c=180&format=json";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonSerializer.Deserialize<Models.MtgSearchResponse>(json);

            return searchResponse?.features?.Select(f => new MtgProduct
            {
                Id = f.id,
                DownloadUrl = f.properties.links.data.FirstOrDefault()?.href,
                TimeStart = f.properties.date.Split('/')[0],
                TimeEnd = f.properties.date.Split('/')[1]
            }).ToList() ?? new List<MtgProduct>();
        }

        private async Task<List<Models.FireDetection>> DownloadAndParseProductAsync(MtgProduct product, string bbox)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "mtg_fires");
                Directory.CreateDirectory(tempDir);

                var zipPath = Path.Combine(tempDir, $"{Guid.NewGuid()}.zip");
                await DownloadFileAsync(product.DownloadUrl, zipPath);

                var ncPath = ExtractNetCdfFromZip(zipPath);

                var fires = await ParseNetCdfWithPythonAsync(ncPath, bbox);

                File.Delete(zipPath);
                File.Delete(ncPath);

                return fires;
            }
            catch (Exception)
            {

                throw;
            }
        }

        private async Task DownloadFileAsync(string url, string filePath)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(filePath);
            await response.Content.CopyToAsync(fileStream);
        }
        private string ExtractNetCdfFromZip(string zipPath)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var ncEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nc"));

            if (ncEntry == null)
                throw new Exception("NetCDF file not found in ZIP");


            var ncPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.nc");
            ncEntry.ExtractToFile(ncPath);
            return ncPath;
        }
        private async Task<List<Models.FireDetection>> ParseNetCdfWithPythonAsync(string ncPath, string bbox)
        {
            try
            {
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "parse_mtg_fire.py");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError("Python script not found: {Path}", scriptPath);
                    return new List<Models.FireDetection>();
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"\"{scriptPath}\" \"{ncPath}\" \"{bbox}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };


                using var process = Process.Start(startInfo);
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var debugFilePath = Path.Combine(
        Directory.GetCurrentDirectory(),
        $"mtg_debug_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
       );


                var debugContent = $@"
=== MTG FIRE DEBUG ===
Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
NetCDF: {ncPath}

=== PYTHON STDERR (Debug Output) ===
{error}

=== PYTHON STDOUT (JSON Output) ===
{output}

=== EXIT CODE ===
{process.ExitCode}
";


                await File.WriteAllTextAsync(debugFilePath, debugContent);
                _logger.LogInformation("🐛 Debug file saved: {Path}", debugFilePath);
                // ⬆️⬆️⬆️ DOSYAYA YAZ ⬆️⬆️⬆️


                if (process.ExitCode != 0)
                {
                    _logger.LogError("Python script failed: {Error}", error);
                    return new List<Models.FireDetection>();
                }
                return await ParsePythonOutput(output);
            }
            catch (Exception)
            {
                throw;
            }
        }
        private async Task<List<Models.FireDetection>> ParsePythonOutput(string jsonOutput)
        {
            var fires = new List<Models.FireDetection>();

            try
            {
                var pythonResult = JsonSerializer.Deserialize<Models.PythonMtgResult>(jsonOutput);
                if (pythonResult?.fires == null)
                    return fires;

                foreach (var mtgFire in pythonResult.fires)
                {
                    var fire = new Models.FireDetection
                    {
                        Id = Guid.NewGuid(),
                        Location = new Point(mtgFire.longitude, mtgFire.latitude) { SRID = 4326 },
                        DetectedAt = DateTime.Parse(pythonResult.metadata.time_start),

                        // MTG confidence (1/2/3) → 0-100
                        Confidence = mtgFire.confidence_value switch
                        {
                            3 => 85.0, // high
                            2 => 60.0, // medium
                            1 => 35.0, // low
                            _ => 50.0
                        },

                        Probability = mtgFire.probability,
                        ConfidenceLevel = mtgFire.confidence,
                        Brightness = null,
                        FireRadiativePower = null,
                        Satellite = "MTG-I1-FCI",
                        Status = mtgFire.confidence_value >= 2 ? FireStatus.Verified : FireStatus.Detected,
                        RiskScore = 0, // ileride bakılmalı buraya 
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    //fire.RiskScore = await _riskCalculationService.CalculateRiskScoreAsync(fire);
                    fires.Add(fire);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Python output");
            }

            return fires;
        }
    }
}
