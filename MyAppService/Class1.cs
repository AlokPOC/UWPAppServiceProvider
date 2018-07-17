using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Core;

namespace MyAppService
{
    public sealed class Inventory : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private AppServiceConnection appServiceconnection;
        private String[] inventoryItems = new string[] { "Robot vacuum", "Chair" };
        private double[] inventoryPrices = new double[] { 129.99, 88.99 };
        private List<DownloadOperation> activeDownloads;
        private CancellationTokenSource cts;
        TimeSpan period;
        TimeTrigger hourlyTrigger;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            hourlyTrigger = new TimeTrigger(60, false);
            period = TimeSpan.FromSeconds(60);
            this.backgroundTaskDeferral = taskInstance.GetDeferral(); // Get a deferral so that the service isn't terminated.
            taskInstance.Canceled += OnTaskCanceled; // Associate a cancellation handler with the background task.
            // Retrieve the app service connection and set up a listener for incoming app service requests.
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            appServiceconnection = details.AppServiceConnection;
            appServiceconnection.RequestReceived += OnRequestReceived;
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            activeDownloads = new List<DownloadOperation>();
            cts = new CancellationTokenSource();

            // Get a deferral because we use an awaitable API below to respond to the message
            // and we don't want this call to get cancelled while we are waiting.
            var messageDeferral = args.GetDeferral();

            ValueSet message = args.Request.Message;
            ValueSet returnData = new ValueSet();

            string command = message["Command"] as string;
            int? inventoryIndex = message["ID"] as int?;

            if (inventoryIndex.HasValue &&
                 inventoryIndex.Value >= 0 &&
                 inventoryIndex.Value < inventoryItems.GetLength(0))
            {
                switch (command)
                {
                    case "automatic_download_promotions":
                        {
                            TimerJob();
                            returnData.Add("Status", "OK");
                            break;
                        }

                    case "manual_download_promotions":
                        {
                            String strResponse = DownloadPromotionJson();
                            returnData.Add("Status", "OK");
                            returnData.Add("Result", strResponse);
                            //returnData.Add("Result", inventoryItems[inventoryIndex.Value]);
                            //returnData.Add("Status", "OK");
                            break;
                        }

                    default:
                        {
                            returnData.Add("Status", "Fail: unknown command");
                            break;
                        }
                }
            }
            else
            {
                returnData.Add("Status", "Fail: Index out of range");
            }

            try
            {
                await args.Request.SendResponseAsync(returnData); // Return the data to the caller.
            }
            catch (Exception e)
            {
                // your exception handling code here
            }
            finally
            {
                // Complete the deferral so that the platform knows that we're done responding to the app service call.
                // Note for error handling: this must be called even if SendResponseAsync() throws an exception.
                messageDeferral.Complete();
            }
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
            {
                // Complete the service deferral.
                this.backgroundTaskDeferral.Complete();
            }
        }

        public void TimerJob()
        {
            ThreadPoolTimer PeriodicTimer = ThreadPoolTimer.CreatePeriodicTimer((source) =>
            {
                DownloadPromotionJson();

                //
                // Update the UI thread by using the UI core dispatcher.
                //
                //Dispatcher.RunAsync(CoreDispatcherPriority.High,
                //    () =>
                //    {
                //        //
                //        // UI components can be accessed within this scope.
                //        //

                //    });

            }, period);
        }

        

        public string DownloadPromotionJson()
        {
            string localfilepathJson = "";
            try
            {
                string url = "https://gameplay.intel.com/netstorage/IntelMarketingMaterial.json"; // new url

                    localfilepathJson = getFilename(url);

                Task[] TaskScheduler = new Task[1]
                {
                    Task.Factory.StartNew(() => DownloadFile(url, localfilepathJson))
                };

                Task.WaitAll(DownloadFile(url, localfilepathJson));
                String result = ((Task<string>)TaskScheduler[0]).Result;

                return result;
            }
            catch (Exception e)
            {
               return e.Message;
            }
        }

        private async Task<string> DownloadFile(string strUrl, string strFile, String[] credentials = null)
        {

            Uri uri = new Uri(strUrl);
            Uri source;
            if (!Uri.TryCreate(strUrl.Trim(), UriKind.Absolute, out source))
            {
                return "URI wrong";
            }

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            //StorageFile storageFile = await localFolder.CreateFileAsync(strFile, CreationCollisionOption.ReplaceExisting);
            StorageFolder sharedDownloadsFolder = ApplicationData.Current.GetPublisherCacheFolder("Downloads");

            StorageFile storageFile = await sharedDownloadsFolder.CreateFileAsync(strFile, CreationCollisionOption.ReplaceExisting);

            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation downloadOperation = downloader.CreateDownload(uri, storageFile);
            downloadOperation.Priority = BackgroundTransferPriority.Default;

            return await HandleDownloadAsync(downloadOperation, true);
        }

        // Note that this event is invoked on a background thread, so we cannot access the UI directly.
        private void DownloadProgress(DownloadOperation download)
        {
            // DownloadOperation.Progress is updated in real-time while the operation is ongoing. Therefore,
            // we must make a local copy so that we can have a consistent view of that ever-changing state
            // throughout this method's lifetime.
            BackgroundDownloadProgress currentProgress = download.Progress;

            //MarshalLog(String.Format(CultureInfo.CurrentCulture, "Progress: {0}, Status: {1}", download.Guid,
            //    currentProgress.Status));

            double percent = 100;
            if (currentProgress.TotalBytesToReceive > 0)
            {
                percent = currentProgress.BytesReceived * 100 / currentProgress.TotalBytesToReceive;
            }

            //MarshalLog(String.Format(
            //    CultureInfo.CurrentCulture,
            //    " - Transferred bytes: {0} of {1}, {2}%",
            //    currentProgress.BytesReceived,
            //    currentProgress.TotalBytesToReceive,
            //    percent));

            //if (currentProgress.HasRestarted)
            //{
            //    MarshalLog(" - Download restarted");
            //}

            if (currentProgress.HasResponseChanged)
            {
                // We have received new response headers from the server.
                // Be aware that GetResponseInformation() returns null for non-HTTP transfers (e.g., FTP).
                ResponseInformation response = download.GetResponseInformation();
                int headersCount = response != null ? response.Headers.Count : 0;

                //MarshalLog(" - Response updated; Header count: " + headersCount);

                // If you want to stream the response data this is a good time to start.
                // download.GetResultStreamAt(0);
            }
        }

        private async Task<string> HandleDownloadAsync(DownloadOperation download, bool start)
        {
            try
            {
                //LogStatus("Running: " + download.Guid, NotifyType.StatusMessage);

                // Store the download so we can pause/resume.
                activeDownloads.Add(download);

                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(DownloadProgress);
                if (start)
                {
                    // Start the download and attach a progress handler.
                    await download.StartAsync().AsTask(cts.Token, progressCallback);
                }
                else
                {
                    // The download was already running when the application started, re-attach the progress handler.
                    await download.AttachAsync().AsTask(cts.Token, progressCallback);
                }

                ResponseInformation response = download.GetResponseInformation();

                // GetResponseInformation() returns null for non-HTTP transfers (e.g., FTP).
                string statusCode = response != null ? response.StatusCode.ToString() : String.Empty;
                return statusCode;
                //LogStatus(
                //    String.Format(
                //        CultureInfo.CurrentCulture,
                //        "Completed: {0}, Status Code: {1}",
                //        download.Guid,
                //        statusCode),
                //    NotifyType.StatusMessage);
            }
            catch (TaskCanceledException et)
            {
                //LogStatus("Canceled: " + download.Guid, NotifyType.StatusMessage);
                return et.Message;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                activeDownloads.Remove(download);
            }
        }

        private bool IsExceptionHandled(string title, Exception ex, DownloadOperation download = null)
        {
            //WebErrorStatus error = BackgroundTransferError.GetStatus(ex.HResult);
            //if (error == WebErrorStatus.Unknown)
            //{
            //    return false;
            //}

            //if (download == null)
            //{
            //    LogStatus(String.Format(CultureInfo.CurrentCulture, "Error: {0}: {1}", title, error),
            //        NotifyType.ErrorMessage);
            //}
            //else
            //{
            //    LogStatus(String.Format(CultureInfo.CurrentCulture, "Error: {0} - {1}: {2}", download.Guid, title,
            //        error), NotifyType.ErrorMessage);
            //}

            return true;
        }


        private string getFilename(string hreflink)
        {
            if (hreflink != null && hreflink != string.Empty)
                return Path.GetFileName(new Uri(hreflink).LocalPath);
            else
                return string.Empty;
        }
        
    }
}
