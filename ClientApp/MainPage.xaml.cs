using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.AppService;
using Windows.Storage;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ClientApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private AppServiceConnection inventoryService;
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            string result = "";

            // Add the connection.
            if (this.inventoryService == null)
            {
                this.inventoryService = new AppServiceConnection();

                var appServiceName = "com.microsoft.inventory";
                //var appServiceName = "InProcessAppSvc";
                // Here, we use the app service name defined in the app service provider's Package.appxmanifest file in the <Extension> section.
                this.inventoryService.AppServiceName = appServiceName;
                var listing = await AppServiceCatalog.FindAppServiceProvidersAsync(appServiceName);
                var packageName = listing[0].PackageFamilyName;

                // Use Windows.ApplicationModel.Package.Current.Id.FamilyName within the app service provider to get this value.
                //this.inventoryService.PackageFamilyName = "032e90bc-4fc8-4f7d-a738-084235381814_zrn1d0te84tw0";
                this.inventoryService.PackageFamilyName = packageName;

                var status = await this.inventoryService.OpenAsync();
                if (status != AppServiceConnectionStatus.Success)
                {
                    textBox.Text = "Failed to connect";
                    return;
                }

                result = GetStatusDetail(status);
            }

            // Call the service.
            int idx = int.Parse(textBox.Text);
            var message = new ValueSet();
            message.Add("Command", "manual_download_promotions");
            message.Add("ID", idx);
            AppServiceResponse response = await this.inventoryService.SendMessageAsync(message);
            

            if (response.Status == AppServiceResponseStatus.Success)
            {
                // Get the data  that the service sent  to us.
                if (response.Message["Status"] as string == "OK")
                {
                    result += response.Message["Result"] as string;

                    StorageFolder sharedDownloadsFolder = ApplicationData.Current.GetPublisherCacheFolder("Downloads");
                    StorageFile sampleFile = await sharedDownloadsFolder.GetFileAsync("IntelMarketingMaterial.json");
                    String fileContent = await FileIO.ReadTextAsync(sampleFile);
                    textBox1.Text = fileContent;
                }
            }

            idx = int.Parse(textBox.Text);
            message = new ValueSet();
            message.Add("Command", "automatic_download_promotions");
            message.Add("ID", idx);
            response = await this.inventoryService.SendMessageAsync(message);
            result = "";

            if (response.Status == AppServiceResponseStatus.Success)
            {
                // Get the data  that the service sent  to us.
                if (response.Message["Status"] as string == "OK")
                {
                    result += response.Message["Result"] as string;

                    StorageFolder sharedDownloadsFolder = ApplicationData.Current.GetPublisherCacheFolder("Downloads");
                    StorageFile sampleFile = await sharedDownloadsFolder.GetFileAsync("IntelMarketingMaterial.json");
                    String fileContent = await FileIO.ReadTextAsync(sampleFile);
                    textBox1.Text = fileContent;
                }
            }

            message.Clear();



            textBox.Text = result;
        }

        private string GetStatusDetail(AppServiceConnectionStatus status)
        {
            var result = "";
            switch (status)
            {
                case AppServiceConnectionStatus.Success:
                    result = "connected";
                    break;
                case AppServiceConnectionStatus.AppNotInstalled:
                    result = "AppServiceSample seems to be not installed";
                    break;
                case AppServiceConnectionStatus.AppUnavailable:
                    result =
                        "App is currently not available (could be running an update or the drive it was installed to is not available)";
                    break;
                case AppServiceConnectionStatus.AppServiceUnavailable:
                    result = "App is installed, but the Service does not respond";
                    break;
                case AppServiceConnectionStatus.Unknown:
                    result = "Unknown error with the AppService";
                    break;
            }
            return result;
        }
    }
}
