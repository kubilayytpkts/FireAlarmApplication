using FireAlarmApplication.Shared.Contracts.Enums;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IRegionDetectionService
    {
        SatelliteSourceInfo GetFastestSource(double latitude, double longitude);
        bool IsInMTGCoverage(double latitude, double longitude);
        bool IsInVIIRSRealtimeCoverage(double latitude, double longitude);
    }
}
