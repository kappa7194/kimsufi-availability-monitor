namespace KimsufiAvailabilityMonitor
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using KimsufiAvailabilityMonitor.ApiEntities;

    using Newtonsoft.Json;

    public static class Program
    {
        private readonly static CancellationTokenSource CancellationSource = new CancellationTokenSource();
        private readonly static object SynchronizationRoot = new object();

        private static bool dialogIsOpen;

        [STAThread]
        public static void Main()
        {
            using (var httpClient = new HttpClient())
            {
                using (var timer = new System.Threading.Timer(CallbackAsync, httpClient, 0, 10000))
                {
                    Console.ReadLine();

                    CancellationSource.Cancel(true);

                    timer.Change(-1, -1);
                }
            }
        }

        private static async void CallbackAsync(object state)
        {
            var httpClient = (HttpClient) state;

            using (var httpResponseMessage = await httpClient.GetAsync("https://ws.ovh.com/dedicated/r2/ws.dispatcher/getAvailability2", CancellationSource.Token))
            {
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    return;
                }

                var json = await httpResponseMessage.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<Response>(json);
                var isAvailable = response.Answer.Availabilities.Single(a => a.Reference == "150sk30").Zones.Any(a => a.Availability != "unknown");

                if (isAvailable)
                {
                    NotifyAvailability();
                }
            }
        }

        private static void NotifyAvailability()
        {
            lock (SynchronizationRoot)
            {
                if (dialogIsOpen)
                {
                    return;
                }

                dialogIsOpen = true;
            }

            Task.Run(DisplayDialog);
        }

        private static Task DisplayDialog()
        {
            MessageBox.Show("text", "caption", MessageBoxButtons.OK, MessageBoxIcon.Information);

            lock (SynchronizationRoot)
            {
                dialogIsOpen = false;
            }

            return Task.CompletedTask;
        }
    }
}
