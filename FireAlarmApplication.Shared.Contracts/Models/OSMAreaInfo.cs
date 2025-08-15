namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class OSMAreaInfo
    {
        public bool IsInForest { get; set; }//orman
        public bool IsInSettlement { get; set; }//yerleşim
        public bool IsInProtectedArea { get; set; }//korunan alan

        public double DistanceToNearestForest { get; set; }
        public double DistanceToNearestSettlement { get; set; }

        public List<OSMFeature> NearbyFeatures { get; set; } = new();
        public string PrimaryLandUse { get; set; } = string.Empty;
        public double RiskMultiplier { get; set; } = 1.0;
    }

    public class OSMFeature
    {
        public string Type { get; set; } = string.Empty; // forest, settlement, protected
        public string Name { get; set; } = string.Empty;
        public double Distance { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }
    public class OSMResponse
    {
        public List<OSMElement> Elements { get; set; } = new();
    }
    public class OSMElement
    {
        public long Id { get; set; }
        public string Type { get; set; } = string.Empty; // "way", "node", "relation"
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }


}
