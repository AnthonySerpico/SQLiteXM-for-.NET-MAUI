using Foundation;
using System;
using System.Globalization;
using System.IO;
using static SQLiteXM.AzureBlobStorage.AzureBlobStorageUtilities;

namespace SQLiteXM
{
    public partial class BackgroundUpload : IBackgroundUpload
    {
        private string _destinationZipFullPath = default(string);
        private static NSUrlSession _session = default(NSUrlSession);
        private string _cloudFileName;
        private UploadFileType _uploadFileType;

        public BackgroundUpload()
        {
        }

        long IBackgroundUpload.UploadRequest(string[] filesToUpload, string zipFileName, string cloudFileName, UploadFileType uploadFileType)
        {
            long zipFileLength = 0;

            _cloudFileName = cloudFileName;
            _uploadFileType = uploadFileType;

            _destinationZipFullPath = ZipFileSupport.zipUpFiles(filesToUpload, zipFileName);
            if (!string.IsNullOrEmpty(_destinationZipFullPath))
            {
                zipFileLength = new FileInfo(_destinationZipFullPath).Length;
                azureNsurlUploadRequest(_destinationZipFullPath);
            }

            return zipFileLength;
        }

        long IBackgroundUpload.UploadRequest(string fileToUpload, string cloudFileName, UploadFileType uploadFileType)
        {
            long zipFileLength = 0;

            _cloudFileName = cloudFileName;
            _uploadFileType = uploadFileType;

            _destinationZipFullPath = fileToUpload;
            if (File.Exists(_destinationZipFullPath))
            {
                zipFileLength = new FileInfo(_destinationZipFullPath).Length;
                azureNsurlUploadRequest(_destinationZipFullPath);
            }

            return zipFileLength;
        }

        void IBackgroundUpload.ZipFileUploadedAPI(string zipFilePath)
        {
            string uploadedFileName = Path.GetFileName(zipFilePath as string);
            string[] subs = uploadedFileName.Split('_');

            NewAudio newAudioDataModel = new NewAudio()
            {
                MemberID = int.Parse(subs[0]),
                ChapterQuestionID = int.Parse(subs[1]),
                FilePath = uploadedFileName
            };

            nsurlAPICall(Defines.LifeBioBaseAPI + Defines.InsertChapterQuestionFile, Newtonsoft.Json.JsonConvert.SerializeObject(newAudioDataModel));
        }

        private void s3NsurlUploadRequest(string destinationZipFullPath)
        {
            if (_session == null)
                _session = InitBackgroundSession();

            try
            {
                Amazon.S3.Model.GetPreSignedUrlRequest requestS3 = new Amazon.S3.Model.GetPreSignedUrlRequest
                {
                    BucketName = S3Constants.BucketName,
                    Key = _cloudFileName + @"/" + Path.GetFileName(destinationZipFullPath),
                    Verb = Amazon.S3.HttpVerb.PUT,
                    Expires = DateTime.Now.AddDays(1),
                    ContentType = "multipart/form-data; boundary=FileBoundry",
                    Protocol = Amazon.S3.Protocol.HTTPS
                };
                string webApiAddress = S3Utils.S3Client.GetPreSignedURL(requestS3);

                // Create request
                NSUrl uploadHandleUrl = NSUrl.FromString(webApiAddress);
                NSMutableUrlRequest request = new NSMutableUrlRequest(uploadHandleUrl);
                request.HttpMethod = "PUT";
                request["Content-Type"] = "multipart/form-data; boundary=FileBoundry";

                // Create the upload task.
                NSUrlSessionUploadTask uploadTask = _session.CreateUploadTask(request, NSUrl.FromFilename(destinationZipFullPath));
                uploadTask.TaskDescription = destinationZipFullPath;
                // Start the task.
                uploadTask.Resume();
            }
            catch (Exception ex)
            {
                string m = ex.Message;
            }
        }

        private void azureNsurlUploadRequest(string destinationZipFullPath)
        {
            string url = new AzureBlobStorage.AzureBlobStorageUtilities(destinationZipFullPath, _uploadFileType)?.GetContainerSasUri();
            NSUrl uploadHandleUrl = NSUrl.FromString(url);

            // Create the session with a delegate to recieve callbacks and debug.
            if (_session == null)
                _session = InitBackgroundSession();

            // Add any headers for Blob PUT.
            NSMutableDictionary nsMutableDictionary = new NSMutableDictionary();
            nsMutableDictionary.Add(new NSString("x-ms-blob-type"), new NSString("BlockBlob"));
            string xMSDate = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
            nsMutableDictionary.Add(new NSString("x-ms-date"), new NSString(xMSDate));
            nsMutableDictionary.Add(new NSString("x-ms-version"), new NSString("2018-03-28"));
            FileInfo fi = new FileInfo(destinationZipFullPath);
            nsMutableDictionary.Add(new NSString("Content-Length"), new NSString(fi.Length.ToString()));

            // Create the request with NSMutableUrlRequest A default NSUrlRequest.FromURL() is immutable with a GET method.
            NSMutableUrlRequest request = new NSMutableUrlRequest(uploadHandleUrl);
            request.Headers = nsMutableDictionary;
            request.HttpMethod = "PUT";

            // Create the upload task.
            NSUrlSessionUploadTask uploadTask = _session.CreateUploadTask(request, NSUrl.FromFilename(destinationZipFullPath));
            uploadTask.TaskDescription = destinationZipFullPath;
            // Start the task.
            uploadTask.Resume();
        }

        private void nsurlAPICall(string url, string jsonBody)
        {
            // Create the session with a delegate to recieve callbacks and debug.
            if (_session == null)
                _session = InitBackgroundSession();

            NSMutableDictionary nsMutableDictionary = new NSMutableDictionary();
            nsMutableDictionary.Add(new NSString("Content-Type"), new NSString("application/json"));
            nsMutableDictionary.Add(new NSString("Authentication"), new NSString("Bearer " + GetAuthToken.GetToken()));  // Need a long term authorization token for this. At least as long as: TimeoutIntervalForResource.

            NSMutableUrlRequest request = new NSMutableUrlRequest(NSUrl.FromString(url));
            request.HttpMethod = "POST";
            request.Headers = nsMutableDictionary; // Add Headers
            request.Body = NSData.FromString(jsonBody); // Add body

            NSUrlSessionTask dataTask = _session.CreateDataTask(request);
            dataTask.Resume();
        }

        public NSUrlSession InitBackgroundSession()
        {
            // See URL below for configuration options
            // https://developer.apple.com/library/ios/documentation/Foundation/Reference/NSURLSessionConfiguration_class/index.html

            // Use same identifier for background tasks so in case app terminiated, iOS can resume tasks when app relaunches.
            string identifier = NSBundle.MainBundle.BundleIdentifier + ".LifeBioMemoryBackgroundTaskID";

            NSUrlSessionConfiguration config = NSUrlSessionConfiguration.BackgroundSessionConfiguration(identifier);
            if (config == default)
                config = NSUrlSessionConfiguration.CreateBackgroundSessionConfiguration(identifier);

            using (config)
            {
                config.TimeoutIntervalForResource = 86400.0; // One day; iOS Default is seven days.
                config.Discretionary = false;
                config.HttpMaximumConnectionsPerHost = 15;
                return NSUrlSession.FromConfiguration(config, (INSUrlSessionDelegate)(new UploadDelegate()), new NSOperationQueue());
            }
        }
    }

    public class UploadDelegate : NSUrlSessionDataDelegate
    {
        // Called by iOS when the task finished trasferring data. It's important to note that his is called even when there isn't an error.
        // See: https://developer.apple.com/library/ios/documentation/Foundation/Reference/NSURLSessionTaskDelegate_protocol/index.html#//apple_ref/occ/intfm/NSURLSessionTaskDelegate/URLSession:task:didCompleteWithError:
        public override async void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError error)
        {
            if (File.Exists(task.TaskDescription) && (Path.GetExtension(task.TaskDescription).ToLower().Equals(".tmp") || Path.GetExtension(task.TaskDescription).ToLower().Equals(".zip")))
            {
                long bs = task.BytesSent;
                if (Path.GetExtension(task.TaskDescription).ToLower().Equals(".zip"))
                {
                    nint taskId = 0;
                    taskId = UIKit.UIApplication.SharedApplication.BeginBackgroundTask("taskname", () =>
                    {
                        // When time is up and task has not finished, call this method to finish the task to prevent the app from being terminated.
                        UIKit.UIApplication.SharedApplication.EndBackgroundTask(taskId);
                    });

                    //PostRequestResponse postRequestResponse = await ExecuteAPI.ExecuteAPICommandSilent(InsertChapterQuestionFile.NewAudio, task.TaskDescription);
                    //Plugin.LocalNotifications.CrossLocalNotifications.Current.Show("Recording Notification ", "Status Code: " + postRequestResponse.statusCode + "   Error Message: " + postRequestResponse.errorMessage == null ? "null" : postRequestResponse.errorMessage + "    Error Description: " + postRequestResponse.errorDescription == null ? "null" : postRequestResponse.errorDescription + "    JSON Response: " + postRequestResponse.jsonResponseString == null ? "null" : postRequestResponse.jsonResponseString);

                    //await Task.Run(() => { DependencyService.Get<IBackgroundUpload>().ZipFileUploadedAPI(task.TaskDescription); });

                    UIKit.UIApplication.SharedApplication.EndBackgroundTask(taskId);
                }
                File.Delete(task.TaskDescription);
            }
        }

        // Called by iOS when session has been invalidated.
        // See: https://developer.apple.com/library/ios/documentation/Foundation/Reference/NSURLSessionDelegate_protocol/index.html#//apple_ref/occ/intfm/NSURLSessionDelegate/URLSession:didBecomeInvalidWithError:
        public override void DidBecomeInvalid(NSUrlSession session, NSError error)
        {
            //Console.WriteLine("DidBecomeInvalid" + (error == null ? "undefined" : error.Description));
        }

        // Called by iOS when all messages enqueued for a session have been delivered.
        // See: https://developer.apple.com/library/ios/documentation/Foundation/Reference/NSURLSessionDelegate_protocol/index.html#//apple_ref/occ/intfm/NSURLSessionDelegate/URLSessionDidFinishEventsForBackgroundURLSession:
        public override void DidFinishEventsForBackgroundSession(NSUrlSession session)
        {
            //Console.WriteLine("DidFinishEventsForBackgroundSession");
            using (Microsoft.Maui.Controls.PlatformConfiguration.iOS.AppDelegate appDelegate = UIKit.UIApplication.SharedApplication.Delegate as iOS.AppDelegate)
            {
            }
        }

        // Called by iOS to periodically inform the progress of sending body content to the server.
        // See: https://developer.apple.com/library/ios/documentation/Foundation/Reference/NSURLSessionTaskDelegate_protocol/index.html#//apple_ref/occ/intfm/NSURLSessionTaskDelegate/URLSession:task:didSendBodyData:totalBytesSent:totalBytesExpectedToSend:
        public override void DidSendBodyData(NSUrlSession session, NSUrlSessionTask task, long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
        {
            // Uncomment line below to see file upload progress outputed to the console. You can track/manage this in your app to monitor the upload progress.
            //Console.WriteLine ("DidSendBodyData bSent: {0}, totalBSent: {1} totalExpectedToSend: {2}", bytesSent, totalBytesSent, totalBytesExpectedToSend);
        }
    }
}
