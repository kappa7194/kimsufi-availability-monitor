namespace KimsufiAvailabilityMonitor
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
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
                httpClient.Timeout = TimeSpan.FromSeconds(Configuration.Default.ApiTimeout);

                Logger.Trace("HTTP client started.");
                Logger.Trace("Timer starting.");

                using (var timer = new System.Threading.Timer(CallbackAsync, httpClient, 0, Configuration.Default.CheckPeriod))
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
            Logger.Trace("Timer callback started.");

            var response = await QueryApiAsync((HttpClient) state);

            if (response == null)
            {
                Logger.Error("Timer callback failed.");

                return;
            }

            CheckAvailability(response);

            Logger.Trace("Timer callback completed.");
        }

        private static async Task<Response> QueryApiAsync(HttpClient httpClient)
        {
            Logger.Trace("API query started.");

            HttpResponseMessage httpResponseMessage = null;

            try
            {
                httpResponseMessage = await ExecuteApiCallAsync(httpClient);

                if (httpResponseMessage == null)
                {
                    Logger.Trace("API query failed.");

                    return null;
                }

                Logger.Trace("API response deserialization started.");

                var serializer = new JsonSerializer();

                serializer.Error += OnDeserializationError;

                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        using (var jsonTextReader = new JsonTextReader(streamReader))
                        {
                            var stopwatch = Stopwatch.StartNew();
                            var response = serializer.Deserialize<Response>(jsonTextReader);

                            stopwatch.Stop();

                            Logger.Debug(CultureInfo.InvariantCulture, "API response deserialization took {0:D} ms.", stopwatch.ElapsedMilliseconds);
                            Logger.Trace("API response deserialization completed.");
                            Logger.Trace("API query completed.");

                            return response;
                        }
                    }
                }
            }
            finally
            {
                httpResponseMessage?.Dispose();
            }
        }

        private static async Task<HttpResponseMessage> ExecuteApiCallAsync(HttpClient httpClient)
        {
            Logger.Trace("API call started.");

            HttpResponseMessage httpResponseMessage;
            Stopwatch stopwatch;

            try
            {
                stopwatch = Stopwatch.StartNew();

                httpResponseMessage = await httpClient.GetAsync(Configuration.Default.ApiEndpoint, CancellationSource.Token);

                stopwatch.Stop();
            }
            catch (HttpRequestException exception)
            {
                var exceptionId = LogException(exception);

                Logger.Error("An error occurred while executing the API call ({0}).", exceptionId);

                return null;
            }
            catch (TaskCanceledException)
            {
                Logger.Info("The API call has been cancelled due to either a timeout or an application shutdown request.");

                return null;
            }

            Logger.Debug(CultureInfo.InvariantCulture, "API HTTP call took {0:D} ms.", stopwatch.ElapsedMilliseconds);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                Logger.Trace("API call completed.");

                return httpResponseMessage;
            }

            Logger.Error("API call failed: {0}", httpResponseMessage.ReasonPhrase);

            return null;
        }

        private static void OnDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            Logger.Trace("JSON deserialization error handler started.");

            if (e.CurrentObject != e.ErrorContext.OriginalObject)
            {
                Logger.Trace("Deserialization error is propagating, skipping handling.");

                return;
            }

            var exceptionId = LogException(e.ErrorContext.Error);

            Logger.Error("JSON deserialization failed ({0}).", exceptionId);
            Logger.Trace("JSON deserialization error handler completed.");
        }

        private static void CheckAvailability(Response response)
        {
            Logger.Trace("Availability check started.");

            if (response.Error != null)
            {
                Logger.Error("API returned an error: {0}", response.Error);
                Logger.Trace("Availability check failed.");

                return;
            }

            var isAvailable = response.Answer.Availabilities.Single(a => a.Reference == Configuration.Default.ServerSku).Zones.Any(a => a.Availability != "unknown" && a.Availability != "unavailable");

            if (isAvailable)
            {
                Logger.Warn("Server available.");

                NotifyAvailability();
            }
            else
            {
                Logger.Info("Server not available.");
            }

            Logger.Trace("Availability check completed.");
        }

        private static void NotifyAvailability()
        {
            Logger.Trace("Availability notification started.");

            Task.Run(DisplayDialog);

            Logger.Trace("Availability notification completed.");
        }

        private static Task DisplayDialog()
        {
            Logger.Trace("Dialog display started.");

            lock (SynchronizationToken)
            {
                if (dialogIsOpen)
                {
                    Logger.Trace("Dialog already displaying.");

                    return Task.CompletedTask;
                }

                dialogIsOpen = true;
            }

            Logger.Trace("Opening dialog.");

            using (var form = new Form())
            {
                form.Location = new Point(SystemInformation.VirtualScreen.Bottom + 10, SystemInformation.VirtualScreen.Right + 10);
                form.Size = new Size(1, 1);
                form.StartPosition = FormStartPosition.Manual;

                form.Show();
                form.Activate();

                MessageBox.Show(form, "Server is available.", "Kimsufi Availability Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Logger.Trace("User acknowledged dialog.");

            lock (SynchronizationToken)
            {
                dialogIsOpen = false;
            }

            Logger.Trace("Dialog display completed.");

            return Task.CompletedTask;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception) e.ExceptionObject;
            var exceptionId = LogException(exception);

            Logger.Fatal("Unhandled exception ({0})", exceptionId);
        }

        private static string LogException(Exception exception)
        {
            var id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
            var message = FormatException(exception, id);

            LogManager.GetLogger("exceptions").Fatal(message);

            return id;
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
