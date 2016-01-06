namespace KimsufiAvailabilityMonitor.ApiEntities
{
    using Newtonsoft.Json;

    internal class Response
    {
        [JsonProperty("answer")]
        public Answer Answer { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }
    }
}
