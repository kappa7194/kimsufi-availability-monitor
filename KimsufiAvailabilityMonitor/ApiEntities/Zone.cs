namespace KimsufiAvailabilityMonitor.ApiEntities
{
    using Newtonsoft.Json;

    internal class Zone
    {
        [JsonProperty("availability")]
        public string Availability { get; set; }

        [JsonProperty("zone")]
        public string Name { get; set; }
    }
}
