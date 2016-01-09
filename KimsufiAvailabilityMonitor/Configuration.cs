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

        internal string TwilioAccountSid { get; } = ConfigurationManager.AppSettings.Get("TwilioAccountSid");

        internal string TwilioAuthToken { get; } = ConfigurationManager.AppSettings.Get("TwilioAuthToken");

        internal string TwilioSenderNumber { get; } = ConfigurationManager.AppSettings.Get("TwilioSenderNumber");

        internal string TwilioRecipientNumber { get; } = ConfigurationManager.AppSettings.Get("TwilioRecipientNumber");

        internal string ServerSku { get; } = ConfigurationManager.AppSettings.Get("ServerSku");

        internal static Configuration Default { get; } = new Configuration();
    }
}
