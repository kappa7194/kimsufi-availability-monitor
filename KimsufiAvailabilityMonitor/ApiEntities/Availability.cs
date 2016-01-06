namespace KimsufiAvailabilityMonitor.ApiEntities
{
    using Newtonsoft.Json;

    internal class Availability
    {
        [JsonProperty("displayMetazones")]
        public bool DisplayMetazones { get; set; }

        [JsonProperty("reference")]
        public string Reference { get; set; }

        [JsonProperty("metaZones")]
        public Zone[] MetaZones { get; set; }

        [JsonProperty("zones")]
        public Zone[] Zones { get; set; }
    }
}
