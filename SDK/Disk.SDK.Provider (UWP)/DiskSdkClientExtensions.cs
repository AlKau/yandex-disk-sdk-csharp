/* Лицензионное соглашение на использование набора средств разработки
 * «SDK Яндекс.Диска» доступно по адресу: http://legal.yandex.ru/sdk_agreement
 */

using System;
using System.Threading.Tasks;

using Disk.SDK.Utils;
using Windows.Foundation;
using Windows.Networking.BackgroundTransfer;
using Windows.Security.Authentication.Web;
using Windows.Storage;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace Disk.SDK.Provider
{
    public class UserInfo
    {
        public string Login { get; private set; }
        public string FIO { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Country { get; private set; }

        public UserInfo(string login, string fio, string firstName, string lastName, string country)
        {
            Login = login;
            FIO = fio;
            FirstName = firstName;
            LastName = lastName;
            Country = country;
        }
    }

    /// <summary>
    /// Disk SDK extension methods with upload\download operations for WinRT platform.
    /// </summary>
    public static class DiskSdkClientExtensions
    {
        private static void EnsureSuccessStatusCode(HttpResponseMessage responce)
        {
            if (responce.IsSuccessStatusCode)
            {
                return;
            }

            switch (responce.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new SdkNotAuthorizedException();
                case HttpStatusCode.BadRequest:
                    throw new SdkBadRequestException();
                case HttpStatusCode.NotFound:
                    throw new SdkBadParameterException();
            }

            responce.EnsureSuccessStatusCode();
        }


        /// <summary>
        /// Starts the asynchronous authentication operation.
        /// </summary>
        /// <param name="sdkClient">The SDK client.</param>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="returnUrl">The return URL.</param>
        /// <returns>The access token</returns>
        /// <exception cref="SdkException"/>
        public static async Task<string> AuthorizeAsync(this IDiskSdkClient sdkClient, string clientId, string returnUrl)
        {
            var requestUri = new Uri(string.Format(ResourceProvider.Get("AuthBrowserUrlFormat"), clientId));
            var returnUri = new Uri(returnUrl);
            WebAuthenticationResult authResult = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, requestUri, returnUri);
            if (authResult.ResponseStatus == WebAuthenticationStatus.Success)
            {
                try
                {
                    var responseData = new Uri(authResult.ResponseData);
                    var fragment = responseData.Fragment;

                    if (!string.IsNullOrEmpty(fragment) && fragment[0].Equals('#'))
                    {
                        fragment = fragment.Remove(0, 1).Insert(0, "?");
                    }

                    WwwFormUrlDecoder decoder = new WwwFormUrlDecoder(fragment);
                    return decoder.GetFirstValueByName(ResourceProvider.Get("TokenKey"));
                }
                catch (Exception ex)
                {
                    throw new SdkException(ex.ToString());
                }
            }

            if (authResult.ResponseStatus == WebAuthenticationStatus.ErrorHttp)
            {
                throw new SdkException(authResult.ResponseErrorDetail.ToString());
            }

            throw new SdkException(authResult.ResponseStatus.ToString());
        }

        /// <summary>
        /// Starts to download the file as an asynchronous operation.
        /// </summary>
        /// <param name="sdkClient">The SDK client.</param>
        /// <param name="path">The path to the file.</param>
        /// <param name="file">The file.</param>
        /// <param name="progress">The progress handler.</param>
        public static async Task StartDownloadFileAsync(this IDiskSdkClient sdkClient, string path, IStorageFile file, IProgress progress)
        {
            try
            {
                var uri = new Uri(ResourceProvider.Get("ApiUrl") + path);
                var downloader = new BackgroundDownloader();
                downloader.SetRequestHeader("Accept", "*/*");
                downloader.SetRequestHeader("TE", "chunked");
                downloader.SetRequestHeader("Accept-Encoding", "gzip");
                downloader.SetRequestHeader("Authorization", "OAuth " + sdkClient.AccessToken);
                downloader.SetRequestHeader("X-Yandex-SDK-Version", "winui, 1.0");
                var download = downloader.CreateDownload(uri, file);
                await HandleDownloadAsync(download, progress, true);
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }


        /// <summary>
        /// Starts to download the file as an asynchronous operation.
        /// </summary>
        /// <param name="sdkClient">The SDK client.</param>
        /// <param name="path">The path to the file.</param>
        /// <param name="file">The file.</param>
        /// <param name="progress">The progress handler.</param>
        public static async Task<IHttpContent> GetFileContentAsync(this IDiskSdkClient sdkClient, string path, ulong start = 0, ulong? end = null)
        {
            try
            {
                var uri = new Uri(ResourceProvider.Get("ApiUrl") + path);
                HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
                filter.AllowUI = false;
                using (var httpClient = new HttpClient(filter))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Version", "1");
                    httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
                    httpClient.DefaultRequestHeaders.Add("TE", "chunked");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
                    httpClient.DefaultRequestHeaders.Add("Authorization", "OAuth " + sdkClient.AccessToken);
                    httpClient.DefaultRequestHeaders.Add("X-Yandex-SDK-Version", "winui, 1.0");

                    var range = end != null ? $"bytes={start}-{end}" : $"bytes={start}-";
                    httpClient.DefaultRequestHeaders.Add("Range", range);

                    using (var response = await httpClient.GetAsync(uri))
                    {
                        EnsureSuccessStatusCode(response);
                        return response.Content;
                    }
                }
            }
            catch (SdkException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }

        /// <summary>
        /// Starts to download the file as an asynchronous operation.
        /// </summary>
        /// <param name="sdkClient">The SDK client.</param>
        /// <param name="path">The path to the file.</param>
        /// <param name="file">The file.</param>
        /// <param name="progress">The progress handler.</param>
        public static async Task<ulong?> GetFileContentLengthAsync(this IDiskSdkClient sdkClient, string path)
        {
            try
            {
                var uri = new Uri(ResourceProvider.Get("ApiUrl") + path);
                HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
                filter.AllowUI = false;
                using (var httpClient = new HttpClient(filter))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Version", "1");
                    httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
                    httpClient.DefaultRequestHeaders.Add("TE", "chunked");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
                    httpClient.DefaultRequestHeaders.Add("Authorization", "OAuth " + sdkClient.AccessToken);
                    httpClient.DefaultRequestHeaders.Add("X-Yandex-SDK-Version", "winui, 1.0");
                    httpClient.DefaultRequestHeaders.Add("Range", "");

                    using (var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                    {
                        EnsureSuccessStatusCode(response);

                        if (response.Content?.Headers?.ContentLength != null)
                        {
                            return response.Content.Headers.ContentLength;
                        }

                        if (response.Headers != null &&
                            response.Headers.TryGetValue("Content-Length", out var strLen) &&
                            ulong.TryParse(strLen, out var len))
                        {
                            return len;
                        }

                        return null;
                    }
                }
            }
            catch (SdkException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }

        /// <summary>
        /// Gets file thumbnail
        /// </summary>
        /// <param name="sdkClient">The SDK client.</param>
        /// <param name="path">The path to the file.</param>
        public static async Task<IHttpContent> GetFileThumbnailAsync(this IDiskSdkClient sdkClient, string path)
        {
            try
            {
                var uri = new Uri($"{ResourceProvider.Get("ApiUrl")}{path}?preview&size=XS");
                HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
                filter.AllowUI = false;
                using (var httpClient = new HttpClient(filter))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Version", "1");
                    httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
                    httpClient.DefaultRequestHeaders.Add("Authorization", "OAuth " + sdkClient.AccessToken);
                    httpClient.DefaultRequestHeaders.Add("X-Yandex-SDK-Version", "winui, 1.0");

                    using (var response = await httpClient.GetAsync(uri))
                    {
                        EnsureSuccessStatusCode(response);
                        return response.Content;
                    }
                }
            }
            catch (SdkException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }

        /// <summary>
        /// Gets user info
        /// </summary>
        /// <param name="sdkClient">The SDK client.</param>
        public static async Task<UserInfo> GetUserInfoAsync(this IDiskSdkClient sdkClient)
        {
            try
            {
                var uri = new Uri($"{ResourceProvider.Get("ApiUrl")}?userinfo");
                HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
                filter.AllowUI = false;
                using (var httpClient = new HttpClient(filter))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Version", "1");
                    httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
                    httpClient.DefaultRequestHeaders.Add("Authorization", "OAuth " + sdkClient.AccessToken);
                    httpClient.DefaultRequestHeaders.Add("X-Yandex-SDK-Version", "winui, 1.0");

                    using (var response = await httpClient.GetAsync(uri))
                    {
                        EnsureSuccessStatusCode(response);
                        var rawInfo = await response.Content.ReadAsStringAsync();

                        var parts = rawInfo.Split("\n", StringSplitOptions.RemoveEmptyEntries);

                        string login = string.Empty;
                        string fio = string.Empty;
                        string firstName = string.Empty;
                        string lastName = string.Empty;
                        string country = string.Empty;

                        const string loginPrefix = "login:";
                        const string fioPrefix = "fio:";
                        const string firstnamePrefix = "firstname:";
                        const string lastnamePrefix = "lastname:";
                        const string countryPrefix = "country:";

                        foreach (var part in parts)
                        {
                            if (part.StartsWith(loginPrefix))
                            {
                                login = part.Substring(loginPrefix.Length);
                            }
                            else if (part.StartsWith(fioPrefix))
                            {
                                fio = part.Substring(fioPrefix.Length);
                            }
                            else if (part.StartsWith(firstnamePrefix))
                            {
                                firstName = part.Substring(firstnamePrefix.Length);
                            }
                            else if (part.StartsWith(lastnamePrefix))
                            {
                                lastName = part.Substring(lastnamePrefix.Length);
                            }
                            else if (part.StartsWith(countryPrefix))
                            {
                                country = part.Substring(countryPrefix.Length);
                            }
                        }

                        return new UserInfo(login, fio, firstName, lastName, country);
                    }
                }
            }
            catch (SdkException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }


        /// <summary>
        /// Starts to upload the file as an asynchronous operation.
        /// </summary>
        /// <param name="sdkClient">The SDK client.</param>
        /// <param name="path">The path to the file.</param>
        /// <param name="file">The file.</param>
        /// <param name="progress">The progress.</param>
        public static async Task StartUploadFileAsync(this IDiskSdkClient sdkClient, string path, IStorageFile file, IProgress progress)
        {
            try
            {
                var uri = new Uri(ResourceProvider.Get("ApiUrl") + path);
                var uploader = new BackgroundUploader { Method = "PUT" };
                uploader.SetRequestHeader("Authorization", "OAuth " + sdkClient.AccessToken);
                uploader.SetRequestHeader("X-Yandex-SDK-Version", "winui, 1.0");
                var upload = uploader.CreateUpload(uri, file);
                await HandleUploadAsync(upload, progress, true);
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }

        /// <summary>
        /// Resumes specified download operation.
        /// Use <code>BackgroundDownloader.GetCurrentDownloadsAsync()</code> to get incomplete operations.
        /// </summary>
        /// <param name="downloadOperation">The download operation.</param>
        /// <param name="progress">The progress.</param>
        public static async Task ResumeDownloadAsync(DownloadOperation downloadOperation, IProgress progress)
        {
            await HandleDownloadAsync(downloadOperation, progress, false);
        }

        /// <summary>
        /// Resumes specified upload operation.
        /// Use <code>BackgroundUploader.GetCurrentDownloadsAsync()</code> to get incomplete operations.
        /// </summary>
        /// <param name="uploadOperation">The upload operation.</param>
        /// <param name="progress">The progress.</param>
        public static async Task ResumeUploadAsync(UploadOperation uploadOperation, IProgress progress)
        {
            await HandleUploadAsync(uploadOperation, progress, false);
        }

        /// <summary>
        /// Handles download operation.
        /// </summary>
        /// <param name="download">The download operation.</param>
        /// <param name="progress">The progress.</param>
        /// <param name="start">if set to <c>true</c> starts a new operation, else attach to exist operation.</param>
        private static async Task HandleDownloadAsync(DownloadOperation download, IProgress progress, bool start)
        {
            try
            {
                var progressCallback = new Progress<DownloadOperation>(
                    operation =>
                        {
                            if (operation.Progress.TotalBytesToReceive > 0)
                            {
                                progress.UpdateProgress(operation.Progress.BytesReceived, operation.Progress.TotalBytesToReceive);
                            }
                        });

                if (start)
                {
                    await download.StartAsync().AsTask(progressCallback);
                }
                else
                {
                    await download.AttachAsync().AsTask(progressCallback);
                }
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }

        /// <summary>
        /// Handles upload operation.
        /// </summary>
        /// <param name="upload">The upload operation.</param>
        /// <param name="progress">The progress.</param>
        /// <param name="start">if set to <c>true</c> starts a new operation, else attach to exist operation.</param>
        private static async Task HandleUploadAsync(UploadOperation upload, IProgress progress, bool start)
        {
            try
            {
                var progressCallback = new Progress<UploadOperation>(
                    operation =>
                        {
                            if (operation.Progress.TotalBytesToSend > 0)
                            {
                                progress.UpdateProgress(operation.Progress.BytesSent, operation.Progress.TotalBytesToSend);
                            }
                        });

                if (start)
                {
                    await upload.StartAsync().AsTask(progressCallback);
                }
                else
                {
                    await upload.AttachAsync().AsTask(progressCallback);
                }
            }
            catch (Exception ex)
            {
                throw HttpUtilities.ProcessException(ex);
            }
        }
    }
}