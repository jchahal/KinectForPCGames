using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Threading;
using System.IO;
using Microsoft.Research.Kinect.Nui;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Diagnostics;

namespace KIKSoftwareProject
{
    /// <summary>
    /// This class provides functions to handle the video related events triggered by Kinect
    /// </summary>
    class VideoDataProcessor
    {
        Image videoStreamToForm = null;

        static readonly Object _threadLock = new Object();
        private Thread videoProcessorThread = null;
        private Semaphore handleRequests = null;
        private Queue<VideoData> dataQueue = null;
        private volatile bool threadExit;
        private Semaphore requestVideoData = null; 
        
        public VideoDataProcessor(Image video)
        {
            videoStreamToForm = video;
            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestVideoData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS); 
            dataQueue = new Queue<VideoData>();
            threadExit = false;

            videoProcessorThread = new Thread(VideoProcessorModule);
            videoProcessorThread.Name = "VideoProcessorThread";
            videoProcessorThread.Start();            
        }

        /// <summary>
        /// Adapter class to hold the data for the video object
        /// </summary>
        public class VideoData
        {
            private ImageFrame imageFrame = null;

            public ImageFrame ImageFrame
            {
                get { return imageFrame; }
                set { imageFrame = value; }
            }

        }

        /// <summary>
        /// Stops the thread by changing the variable state
        /// </summary>
        public void ExitThread()
        {
            threadExit = true;
            handleRequests.Release();
        }

        /// <summary>
        /// Adds the new video object to the queue to be processed
        /// </summary>
        /// <param name="videoData">Object with new data</param>
        /// <returns>Outstanding requests in the queue</returns>
        public int AddToQueue(VideoData videoData)
        {
            //Decrement the semaphore to make sure the spot is available
            this.requestVideoData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(videoData);
            }

            //Increament the semaphore to indicate there is work to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }

        /// <summary>
        /// Removes the new video object from the queue
        /// </summary>
        /// <returns>video object</returns>
        private VideoData RemoveFromQueue()
        {
            VideoData resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            int previousCount = this.requestVideoData.Release();

            return resultValue;
        }

        /// <summary>
        /// Returns the worker Queue count
        /// </summary>
        /// <returns>How many tasks are left</returns>
        private int GetQueueCount()
        {
            int count = 0;

            lock (_threadLock)
            {
                count = dataQueue.Count;
            }

            return count;
        }

        /// <summary>
        /// Video processor thread module
        /// </summary>
        private void VideoProcessorModule()
        {
            try
            {
                do
                {
                    handleRequests.WaitOne();

                    if (GetQueueCount() > 0)
                    {
                        VideoData newVideoData = RemoveFromQueue();
                        ProcessNewVideoData(newVideoData);
                    }

                } while (!threadExit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// This function displays the current image on the screen passed from Kinect event
        /// </summary>
        /// <param name="newVideoData">Object containing the new Image frame</param>
        /// <returns>result code for failure or success </returns>
        private int ProcessNewVideoData(VideoData newVideoData)
        {
            try
            {
                if (newVideoData == null)
                {
                    return (int)ResultCodes.OutOfMemory;
                }
                
                PlanarImage latestImage = newVideoData.ImageFrame.Image;                

                videoStreamToForm.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Send /*Send - the highest priority for the dispatcher*/,
                TimeSpan.FromSeconds(1),
                new Action(
                () =>
                {

                    try
                    {
                        videoStreamToForm.Source = BitmapSource.Create(
                        latestImage.Width, latestImage.Height, 96, 96, PixelFormats.Bgr32, null, latestImage.Bits, latestImage.Width * latestImage.BytesPerPixel);
                    }
                    catch
                    {

                    }
                }));
               
            }
            catch
            {

                return (int)ResultCodes.VideoProcessorFailed;

            }

            return (int)ResultCodes.Success;
        }
    }
}
