namespace FireAlarmApplication.Web.Modules.FireDetection.Models
{

    public class MtgProduct
    {
        public string Id { get; set; }
        public string DownloadUrl { get; set; }
        public string TimeStart { get; set; }
        public string TimeEnd { get; set; }
    }

    public class MtgSearchResponse
    {
        public List<MtgFeature> features { get; set; }
    }

    public class MtgFeature
    {
        public string id { get; set; }
        public MtgProperties properties { get; set; }
    }

    public class MtgProperties
    {
        public string date { get; set; }
        public MtgLinks links { get; set; }
    }

    public class MtgLinks
    {
        public List<MtgLink> data { get; set; }
    }

    public class MtgLink
    {
        public string href { get; set; }
    }

    public class PythonMtgResult
    {
        public PythonMetadata metadata { get; set; }
        public List<PythonFire> fires { get; set; }
    }

    public class PythonMetadata
    {
        public string time_start { get; set; }
        public string time_end { get; set; }
    }

    public class PythonFire
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string confidence { get; set; }
        public int confidence_value { get; set; }
        public double? probability { get; set; }
    }

    public class TokenResponse
    {
        public string access_token { get; set; }
    }
}
