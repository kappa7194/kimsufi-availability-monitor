namespace KimsufiAvailabilityMonitor
{
    using System.Configuration;
    using System.Globalization;

    internal class Configuration
    {
        private Configuration()
        {
        }

        internal string ApiEndpoint { get; } = ConfigurationManager.AppSettings.Get("ApiEndpoint");

        internal int ApiTimeout { get; } = 1000 * int.Parse(ConfigurationManager.AppSettings.Get("ApiTimeout"), NumberStyles.None, CultureInfo.InvariantCulture);

        internal int CheckPeriod { get; } = 1000 * int.Parse(ConfigurationManager.AppSettings.Get("CheckPeriod"), NumberStyles.None, CultureInfo.InvariantCulture);

        internal string ServerSku { get; } = ConfigurationManager.AppSettings.Get("ServerSku");

        internal static Configuration Default { get; } = new Configuration();
    }
}
