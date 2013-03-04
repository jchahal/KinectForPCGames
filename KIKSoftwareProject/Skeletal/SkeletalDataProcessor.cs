using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Windows.Controls;
using Microsoft.Research.Kinect.Nui;
using log4net;
using log4net.Repository.Hierarchy;
using KIKSoftwareProject.Gestures;

namespace KIKSoftwareProject.Skeletal
{
    /// <summary>
    /// Class that provides methods to proccesses the skeleton data 
    /// and passes the data to GUI threads as well as the Gesture recognition threads
    /// </summary>
    class SkeletalDataProcessor
    {
        static readonly Object _threadLock = new Object();

        private log4net.ILog skeletalDataLogger = null;
        private Thread skeletalProcessorThread = null;
        private Semaphore handleRequests = null;
        private Queue<SkeletalData> dataQueue = null;
        private volatile bool threadExit;
        private Semaphore requestSkeletalData = null;
        private Runtime nuiRuntime = null;
        private Canvas skeletonCanvas = null;
        private Canvas transformedSkeleton = null;
        private KeyboardInputProcessor keyboardProcessor = null;
        private SkeletalDrawing skeletalDrawingObject = null;
        private GestureDispatcher gestureDispatch = null;
        private bool trackedState = false;
        private MouseInputProcessor mouseInput = null;
        private bool stopGestureRecognition = false;

        private bool stopVisualThreads = false;

        /// <summary>
        /// Configures the logger using the log4net library functions
        /// </summary>
        void ConfigureLogManager()
        {

            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger("KIKSoftwareProject.SkeletalDataProcessor") as Logger;
            coreLogger.Parent = rootLogger;

            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = "skeletalDP_Logs.txt";
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();

            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            hierarchy.Configured = true;

            skeletalDataLogger = LogManager.GetLogger("KIKSoftwareProject.SkeletalDataProcessor");
        }

        /// <summary>
        /// Property that is used to stop all the visual threads
        /// </summary>
        public bool StopVisualThread
        {
            get { return this.stopVisualThreads; }

            set { this.stopVisualThreads = value; }
        }

        /// <summary>
        /// Class used to hold new skeleton objects for the queue
        /// </summary>
        public class SkeletalData
        {
            private SkeletonFrame skeletalframe = null;

            public SkeletonFrame SkeletonFrame
            {
                get { return skeletalframe; }
                set { skeletalframe = value; }
            }
        }

        public SkeletalDataProcessor(Runtime nui, Canvas skeleton, KeyboardInputProcessor keyboard, MouseInputProcessor mouse, Canvas mainTransformedSkeleton)
        {
            ConfigureLogManager();
            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestSkeletalData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);

            this.keyboardProcessor = keyboard;
            this.nuiRuntime = nui;
            this.skeletonCanvas = skeleton;
            this.transformedSkeleton = mainTransformedSkeleton;
            this.mouseInput = mouse;
            this.skeletalDrawingObject = new SkeletalDrawing(nui, skeleton);
            this.gestureDispatch = new GestureDispatcher(nui,keyboard, mouse, mainTransformedSkeleton);

            dataQueue = new Queue<SkeletalData>();
            threadExit = false;
            skeletalProcessorThread = new Thread(SkeletalProcessorModule);
            skeletalProcessorThread.Name = "SkeletalProcessorThread";
            skeletalProcessorThread.Start();            
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
        /// Adds the new skeletal drawing object to the queue to be processed
        /// </summary>
        /// <param name="skeletalData">new object with the data</param>
        /// <returns>number tasks outstanding in the queue</returns>
        public int AddToQueue(SkeletalData skeletalData)
        {

            //Decrement the semaphore to make sure the spot is available
            this.requestSkeletalData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(skeletalData);
            }

            //Increament the semaphore to indicate there is work to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }

        /// <summary>
        /// Removes the new skeleton data object from the queue
        /// </summary>
        /// <returns>SkeletalData object with new data</returns>
        private SkeletalData RemoveFromQueue()
        {
            SkeletalData resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            int previousCount = this.requestSkeletalData.Release();

            return resultValue;
        }

        /// <summary>
        /// Gives the current count of the queue
        /// </summary>
        /// <returns>the queue count</returns>
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
        /// Thread method that waits on the queue for any requests to arrive
        /// </summary>
        private void SkeletalProcessorModule()
        {
            int processResult = (int)ResultCodes.Success;

            try
            {
                do
                {
                    handleRequests.WaitOne();

                    if (GetQueueCount() > 0)
                    {
                        SkeletalData newSkeletalData = RemoveFromQueue();
                        processResult = ProcessNewSkeletalData(newSkeletalData);
                    }

                } while (!threadExit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error: Skeletal processing thread exception = " + ex.Message);    
            }
        }
        /// <summary>
        /// Processes the new skeleton data by passing the new object to the respective thread and checks the
        /// conditions if the gesture recognition has not stopped or started again or the first time
        /// </summary>
        /// <param name="skeletalData">the new skeleton information object</param>
        /// <returns>Processing result (success or failure)</returns>
        private int ProcessNewSkeletalData(SkeletalData skeletalData)
        {
            int processingResult = (int)ResultCodes.Success;

            do
            {
                if (skeletalData == null)
                {
                    processingResult = (int)ResultCodes.OutOfMemory;
                    break;
                }

                if (!this.stopVisualThreads)
                {
                    SkeletalDrawing.SkeletalDrawingData newDrawingData = new SkeletalDrawing.SkeletalDrawingData();
                    newDrawingData.SetSkeletalFrame(skeletalData.SkeletonFrame);
                    this.skeletalDrawingObject.AddToQueue(newDrawingData);
                }

                if (!this.trackedState && CheckIntialCondition(skeletalData) == (int)ResultCodes.GestureDetected)
                {
                    skeletalDataLogger.Debug("this.trackedstate is true");
                    this.trackedState = true;                    
                }

                this.stopGestureRecognition = StopGestureRecognition(skeletalData);

                if (this.trackedState)
                {


                    if (!this.stopGestureRecognition)
                    {
                        GestureDispatcher.GestureData newGestureData = new GestureDispatcher.GestureData();
                        newGestureData.SetSkeletalFrame(skeletalData.SkeletonFrame);
                        skeletalDataLogger.Debug("traked state is true drawing data added into processor queue");

                        this.gestureDispatch.StopVisualThread = this.stopVisualThreads;
                        this.gestureDispatch.ProcessNewGestureData(newGestureData);
                    }
                    else
                    {
                        this.trackedState = false;
                    }
                }
                

            } while (false);

            return processingResult;
        }

        /// <summary>
        /// Checks the condition if the Gesure recognition need to be stopped
        /// </summary>
        /// <param name="skeletalData">the new skeleton information object</param>
        /// <returns>true/false based on the conditions matched</returns>
        private bool StopGestureRecognition(SkeletalData skeletalData)
        {
            bool stopResult = false;
            SkeletonFrame currentFrame = skeletalData.SkeletonFrame;


            foreach (SkeletonData data in currentFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    Joint headJoint = data.Joints[JointID.Head];
                    Joint rightHandJoint = data.Joints[JointID.HandRight];

                    Joint leftHandJoint = data.Joints[JointID.HandLeft];
                    
                    if (rightHandJoint.Position.Y > headJoint.Position.Y && leftHandJoint.Position.Y > headJoint.Position.Y)
                    {
                        stopResult = true;
                        break;
                    }
                }
            }

            return stopResult;
        }

        /// <summary>
        /// Checks the intial condition if the gesture recognition need to be started
        /// </summary>
        /// <param name="skeletalData">the new skeleton information object</param>
        /// <returns>condition detected - different code values</returns>
        private int CheckIntialCondition(SkeletalData skeletalData)
        {
            int conditionCheck = -1;
            SkeletonFrame currentFrame = skeletalData.SkeletonFrame;

            foreach (SkeletonData data in currentFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    Joint headJoint = data.Joints[JointID.Head];
                    Joint rightHandJoint = data.Joints[JointID.HandRight];
                    Joint leftHandJoint = data.Joints[JointID.HandLeft];

                    if (this.stopGestureRecognition)
                    {
                        
                        conditionCheck = (int)ResultCodes.GestureDetectionFailed;
                        break;
                        
                    }

                    if (rightHandJoint.Position.Y > headJoint.Position.Y && leftHandJoint.Position.Y > headJoint.Position.Y)
                    {
                        conditionCheck = (int)ResultCodes.GestureDetectionFailed;
                        break;
                    }

                    if (rightHandJoint.Position.Y > headJoint.Position.Y && leftHandJoint.Position.Y < headJoint.Position.Y)
                    {
                        conditionCheck = (int)ResultCodes.GestureDetected;
                        break;
                    }
                }
            }

            return conditionCheck;
        }
    }
}
