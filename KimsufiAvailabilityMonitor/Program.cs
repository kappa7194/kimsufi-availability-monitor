namespace KimsufiAvailabilityMonitor
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using KimsufiAvailabilityMonitor.ApiEntities;

    using Newtonsoft.Json;

    using NLog;

    public static class Program
    {
        private readonly static CancellationTokenSource CancellationSource = new CancellationTokenSource();
        private readonly static ILogger Logger = LogManager.GetLogger("application");
        private readonly static object SynchronizationToken = new object();

        private static bool dialogIsOpen;

        [STAThread]
        public static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Logger.Trace("Application starting.");
            Logger.Trace("HTTP client starting.");

            using (var httpClient = new HttpClient())
            {
                Logger.Trace("HTTP client started.");
                Logger.Trace("Timer starting.");

                using (var timer = new System.Threading.Timer(CallbackAsync, httpClient, 0, 10000))
                {
                    Logger.Trace("Timer started.");
                    Logger.Info("Application started.");
                    Logger.Info("Press ENTER while the application has focus to stop it.");

                    Console.ReadLine();

                    Logger.Info("Application shutdown requested by user.");
                    Logger.Trace("Application stopping.");
                    Logger.Trace("Task cancellation started.");

                    CancellationSource.Cancel();

                    Logger.Trace("Task cancellation completed.");
                    Logger.Trace("Timer deactivating.");

                    timer.Change(-1, -1);

                    Logger.Trace("Timer deactivated.");
                    Logger.Trace("Timer stopping.");
                }

                Logger.Trace("Timer stopped.");
                Logger.Trace("HTTP client stopping.");
            }

            Logger.Trace("HTTP client stopped.");
            Logger.Info("Application stopped.");
        }

        private static async void CallbackAsync(object state)
        {
            Logger.Trace("Callback started.");

            var httpClient = (HttpClient) state;

            Logger.Trace("HTTP request started.");

            HttpResponseMessage httpResponseMessage = null;

            try
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    httpResponseMessage = await httpClient.GetAsync("https://ws.ovh.com/dedicated/r2/ws.dispatcher/getAvailability2", CancellationSource.Token);
                }
                catch (HttpRequestException exception)
                {
                    Logger.Error("An error occurred while executing the HTTP request.");

                    LogException(exception);

                    return;
                }
                catch (TaskCanceledException)
                {
                    Logger.Info("The HTTP request has been cancelled due to either a timeout or an application shutdown request.");

                    return;
                }

                stopwatch.Stop();

                Logger.Trace("HTTP request completed.");
                Logger.Debug(CultureInfo.InvariantCulture, "HTTP request took {0:D} ms.", stopwatch.ElapsedMilliseconds);
                Logger.Trace("HTTP request content read started.");

                Response response;

                var serializer = new JsonSerializer();

                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        using (var jsonTextReader = new JsonTextReader(streamReader))
                        {
                            Logger.Trace("JSON deserialization started.");

                            stopwatch.Restart();

                            response = serializer.Deserialize<Response>(jsonTextReader);

                            stopwatch.Stop();

                            Logger.Trace("JSON deserialization completed.");
                            Logger.Debug("JSON deserialization took {0:D} ms.", stopwatch.ElapsedMilliseconds);
                        }
                    }
                }

                Logger.Trace("Availability check started.");

                var isAvailable = response.Answer.Availabilities.Single(a => a.Reference == "150sk30").Zones.Any(a => a.Availability != "unknown");

                Logger.Trace("Availability check completed.");

                if (isAvailable)
                {
                    Logger.Warn("Server available.");

                    NotifyAvailability();
                }
                else
                {
                    Logger.Info("Server not available.");
                }
            }
            finally
            {
                httpResponseMessage?.Dispose();
            }

            Logger.Trace("Callback completed.");
        }

        private static void NotifyAvailability()
        {
            Logger.Trace("Availability notification started.");
            Logger.Trace("Synchronization lock acquiring.");

            lock (SynchronizationToken)
            {
                Logger.Trace("Synchronization lock acquired.");

                if (dialogIsOpen)
                {
                    return;
                }

                dialogIsOpen = true;

                Logger.Trace("Synchronization lock releasing.");
            }

            Logger.Trace("Synchronization lock released.");

            Task.Run(DisplayDialog);

            Logger.Trace("Availability notification completed.");
        }

        private static Task DisplayDialog()
        {
            Logger.Trace("Dialog display started.");

            MessageBox.Show("Server is available.", "Kimsufi Availability Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Logger.Trace("User acknowledged dialog.");
            Logger.Trace("Synchronization lock acquiring.");

            lock (SynchronizationToken)
            {
                Logger.Trace("Synchronization lock acquired.");

                dialogIsOpen = false;

                Logger.Trace("Synchronization lock releasing.");
            }

            Logger.Trace("Synchronization lock released.");
            Logger.Trace("Dialog display completed.");

            return Task.CompletedTask;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var id = CreateExceptionId();
            var exception = (Exception) e.ExceptionObject;

            Logger.Fatal("Unhandled exception ({0})", id);

            LogException(exception, id);
        }

        private static string CreateExceptionId()
        {
            return Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        }

        private static void LogException(Exception exception)
        {
            var id = CreateExceptionId();

            LogException(exception, id);
        }

        private static void LogException(Exception exception, string id)
        {
            var message = FormatException(exception, id);

            LogManager.GetLogger("exceptions").Fatal(message);
        }

        private static string FormatException(Exception exception, string id)
        {
            var moment = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(id);
            stringBuilder.AppendLine(moment);

            FormatException(stringBuilder, exception);

            return stringBuilder.ToString();
        }

        private static void FormatException(StringBuilder stringBuilder, Exception exception)
        {
            var type = exception.GetType();

            stringBuilder.AppendLine(type.FullName);
            stringBuilder.AppendLine(exception.Message);
            stringBuilder.AppendLine(exception.StackTrace);

            if (exception.InnerException != null)
            {
                FormatException(stringBuilder, exception.InnerException);
            }
        }
    }
}
