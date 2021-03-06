﻿namespace KimsufiAvailabilityMonitor
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

    using Twilio;

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

                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        using (var jsonTextReader = new JsonTextReader(streamReader))
                        {
                            Response response;
                            Stopwatch stopwatch;

                            var serializer = new JsonSerializer();

                            try
                            {
                                stopwatch = Stopwatch.StartNew();
                                response = serializer.Deserialize<Response>(jsonTextReader);
                                stopwatch.Stop();
                            }
                            catch (JsonReaderException exception)
                            {
                                var exceptionId = LogException(exception);

                                Logger.Error("API response deserialization failed ({0}).", exceptionId);

                                await LogJsonAsync(stream, exceptionId);

                                return null;
                            }

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

            Task.Run(() => SendMessage());
            Task.Run(() => DisplayDialog());

            Logger.Trace("Availability notification completed.");
        }

        private static void SendMessage()
        {
            Logger.Trace("Message sending started.");

            if (!IsMessageGatewayConfigured())
            {
                Logger.Debug("Message gateway not configured.");
                Logger.Trace("Message sending aborted.");

                return;
            }

            var twilioClient = new TwilioRestClient(Configuration.Default.TwilioAccountSid, Configuration.Default.TwilioAuthToken);

            var message = twilioClient.SendMessage(Configuration.Default.TwilioSenderNumber, Configuration.Default.TwilioRecipientNumber, "Server available.");

            if (message.RestException != null)
            {
                Logger.Error("Message sending failed: {0} {1}", message.RestException.Code, message.RestException.Message);
            }
            else if (message.ErrorCode != null || message.ErrorMessage != null)
            {
                Logger.Error("Message sending failed: {0} {1}", message.ErrorCode, message.ErrorMessage);
            }
            else
            {
                Logger.Trace("Message sending completed.");
            }
        }

        private static bool IsMessageGatewayConfigured()
        {
            return
                !string.IsNullOrWhiteSpace(Configuration.Default.TwilioAccountSid)
                && !string.IsNullOrWhiteSpace(Configuration.Default.TwilioAuthToken)
                && !string.IsNullOrWhiteSpace(Configuration.Default.TwilioSenderNumber)
                && !string.IsNullOrWhiteSpace(Configuration.Default.TwilioRecipientNumber);
        }

        private static void DisplayDialog()
        {
            Logger.Trace("Dialog display started.");

            lock (SynchronizationToken)
            {
                if (dialogIsOpen)
                {
                    Logger.Trace("Dialog already displaying.");

                    return;
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

        private static async Task LogJsonAsync(Stream stream, string streamId)
        {
            if (!stream.CanSeek)
            {
                return;
            }

            stream.Seek(0, SeekOrigin.Begin);

            var logPath = string.Format(CultureInfo.InvariantCulture, @"Logs\ApiResponse_{0}.json", streamId);

            using (var fileStream = new FileStream(logPath, FileMode.CreateNew))
            {
                await stream.CopyToAsync(fileStream);
            }
        }
    }
}
