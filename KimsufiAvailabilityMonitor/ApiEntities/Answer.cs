namespace KimsufiAvailabilityMonitor.ApiEntities
{
    using Newtonsoft.Json;

    internal class Answer
    {
        [JsonProperty("availability")]
        public Availability[] Availabilities { get; set; }
    }
}
