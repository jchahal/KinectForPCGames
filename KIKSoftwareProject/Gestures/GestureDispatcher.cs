using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Windows.Controls;
using Microsoft.Research.Kinect.Nui;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using log4net.Appender;
using System.Linq;
using System.Text;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Provides methods to dispatch gesture data to the required/respective gesture threads
    /// </summary>
    class GestureDispatcher
    {
        private log4net.ILog gestureDispatchLogger = null;
        private static readonly Object _threadLock = new Object();        
        private Semaphore handleRequests = null;
        private Queue<GestureData> dataQueue = null;        
        private Semaphore requestGestureDataSem = null;
        private KeyboardInputProcessor keyboardProcessor = null;
        private MouseInputProcessor mouseProcessor = null;
        private MoveLeftRightGestures legsGesture = null;
        private MoveFrontBackGestures frontbackGesture = null;
        private Runtime nuiRuntime = null;
        private MousePointerGesture mouseGesture = null;
        private ShootingGesture shootGesture = null;
        private JumpGesture jumpGesture = null;
        private Canvas tranformedSkeleton = null;
        private TransformedSkeletonDrawing drawSkeleton = null;

        private bool stopVisualThreads = false;
         

        void ConfigureLogManager()
        {

            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger("KIKSoftwareProject.GestureDispatcher") as Logger;
            coreLogger.Parent = rootLogger;
            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = "Gesture_Dispatcher_Logs.txt";
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();


            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            hierarchy.Configured = true;

            gestureDispatchLogger = LogManager.GetLogger("KIKSoftwareProject.GestureDispatcher");            
        }

        public GestureDispatcher(Runtime nui, KeyboardInputProcessor keyboard, MouseInputProcessor mouse, Canvas mainTransformedSkeleton)
        {
            ConfigureLogManager();

            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestGestureDataSem = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);

            this.keyboardProcessor = keyboard;
            this.nuiRuntime = nui;
            this.mouseProcessor = mouse;
            this.tranformedSkeleton = mainTransformedSkeleton;

            dataQueue = new Queue<GestureData>();
            

            this.drawSkeleton = new TransformedSkeletonDrawing(nui, mainTransformedSkeleton);

            GestureProcessorModule();
        }

        public class GestureData
        {
            private SkeletonFrame skeletalFrame = null;

            public SkeletonFrame GetSkeletonFrame()
            {
                return this.skeletalFrame;
            }

            public void SetSkeletalFrame(SkeletonFrame frame)
            {
                this.skeletalFrame = frame;
            }
        }

        public bool StopVisualThread
        {
            get { return this.stopVisualThreads; }

            set { this.stopVisualThreads = value; }
        }

        public int AddToQueue(GestureData data)
        {
            //Decrement the semaphore to make sure the spot is available
            this.requestGestureDataSem.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(data);
            }

            //Increament the semaphore to indicate there is work to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }

        private GestureData RemoveFromQueue()
        {
            GestureData resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            int previousCount = this.requestGestureDataSem.Release();

            return resultValue;
        }

        private int GetQueueCount()
        {
            int count = 0;

            lock (_threadLock)
            {
                count = dataQueue.Count;
            }

            return count;
        }

        private void GestureProcessorModule()
        {
            //int processResult = (int)ResultCodes.Success;

            try
            {
                legsGesture = new MoveLeftRightGestures();
                legsGesture.keyboardGestureAction += new NuiKeyboardGestureEvent(legsGesture_gestureAction);

                mouseGesture = new MousePointerGesture(this.nuiRuntime);
                mouseGesture.mouseGestureAction += new NuiMouseGestureEvent(mouseGesture_mouseGestureAction);

                shootGesture = new ShootingGesture();
                shootGesture.mouseGestureAction += new NuiMouseGestureEvent(shootGesture_mouseGestureAction);

                frontbackGesture = new MoveFrontBackGestures();
                frontbackGesture.keyboardGestureAction += new NuiKeyboardGestureEvent(frontbackGesture_gestureAction);

                jumpGesture = new JumpGesture();
                jumpGesture.keyboardGestureAction += new NuiKeyboardGestureEvent(jumpGesture_keyboardGestureAction);             
            }
            catch (Exception ex)
            {
                gestureDispatchLogger.Fatal("Error: Skeletal processing thread exception = " + ex.Message);
            }
        }

        public int ProcessNewGestureData(GestureData newGestureData)
        {
            int result = (int)ResultCodes.Success;

            do
            {
                SkeletonData trackedSkeleton = (from skeletons in newGestureData.GetSkeletonFrame().Skeletons
                                                where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                                select skeletons).FirstOrDefault();

                if (trackedSkeleton == null)
                {
                    result = (int)ResultCodes.NullTrackedSkeleton;
                    break;
                }

                GestureBase.NuiElement nuiElement = new GestureBase.NuiElement();

                nuiElement.SetSkeletonFrame(newGestureData.GetSkeletonFrame());
                nuiElement.SetTimeStamp(DateTime.Now);

                if (legsGesture != null) legsGesture.AddToQueue(nuiElement);
                if (mouseGesture != null) mouseGesture.AddToQueue(nuiElement);
               
                if (frontbackGesture != null) frontbackGesture.AddToQueue(nuiElement);
                if (jumpGesture != null) jumpGesture.AddToQueue(nuiElement);

                if (shootGesture != null) shootGesture.AddToQueue(nuiElement);                

                if (!this.stopVisualThreads)
                {
                    TransformedSkeletonDrawing.SkeletalDrawingData newDrawingData = new TransformedSkeletonDrawing.SkeletalDrawingData();
                    newDrawingData.SetSkeletalFrame(newGestureData.GetSkeletonFrame());

                    this.drawSkeleton.AddToQueue(newDrawingData);
                }
                
            } while (false);

            return result;
        }

        private bool AreHandsClose(SkeletonFrame frame)
        {
            bool areHandsclose = false;
            SkeletonFrame currentFrame = frame;


            foreach (SkeletonData data in currentFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    Joint rightHandJoint = data.Joints[JointID.WristRight];
                    Joint leftHandJoint = data.Joints[JointID.WristLeft];

                    double handdiffx = rightHandJoint.Position.X - leftHandJoint.Position.X;
                    double handdiffy = rightHandJoint.Position.Y - leftHandJoint.Position.Y;                    
                    double handpointsdiff = Math.Sqrt((handdiffx * handdiffx) + (handdiffy * handdiffy));

                    if (handpointsdiff < 0.05)
                    {
                        areHandsclose = true;
                        break;
                    }
                }
            }

            return areHandsclose;
        }

        void jumpGesture_keyboardGestureAction(object sender, KeyboardEventData eventData)
        {
            SendGameCommand(eventData.GetKeyboradAction(), eventData.GetKeyboardPersistance());
        }

        void shootGesture_mouseGestureAction(object sender, MouseEventData eventData)
        {
            SendMouseCommand(eventData.MouseAction, eventData.KeyPersistance, eventData.XCoordinate, eventData.YCoordinate);
        }

        void mouseGesture_mouseGestureAction(object sender, MouseEventData eventData)
        {
            SendMouseCommand(eventData.MouseAction, eventData.KeyPersistance, eventData.XCoordinate, eventData.YCoordinate);
        }

        void legsGesture_gestureAction(object sender, KeyboardEventData eventData)
        {
            gestureDispatchLogger.Debug("The thread executing this code is : " + Thread.CurrentThread.Name);
            SendGameCommand(eventData.GetKeyboradAction(), eventData.GetKeyboardPersistance());
        }


        void frontbackGesture_gestureAction(object sender, KeyboardEventData eventData)
        {
            gestureDispatchLogger.Debug("The thread executing this code is : " + Thread.CurrentThread.Name);
            SendGameCommand(eventData.GetKeyboradAction(), eventData.GetKeyboardPersistance());
        }    

        private int SendGameCommand(int actionID, int persistance)
        {
            int sendResult = (int)ResultCodes.Success;

            try
            {
                KeyboardInputProcessor.KeyboardData keyboardData = new KeyboardInputProcessor.KeyboardData();
                keyboardData.KeyboardAction = actionID;
                keyboardData.KeyPersistance = persistance;

                sendResult = keyboardProcessor.AddToQueue(keyboardData);
            }

            catch (Exception ex)
            {
                gestureDispatchLogger.Debug(ex.Message);
            }


            return sendResult;
        }

        private int SendMouseCommand(int keyAction, int persistance,int xCoordinate, int yCoordinate)
        {
            int sendResult = (int)ResultCodes.KeyPressFailed;
            do
            {
                try
                {
                    MouseInputProcessor.MouseData mouseData = new MouseInputProcessor.MouseData();
                    mouseData.MouseAction = keyAction;
                    mouseData.KeyPersistance = persistance;
                    mouseData.XCoordinate = xCoordinate;
                    mouseData.YCoordinate = yCoordinate;

                    sendResult = mouseProcessor.AddToQueue(mouseData);                   

                }

                catch (Exception ex)
                {
                    gestureDispatchLogger.Debug(ex.Message);
                }
            } while (false);

            if (sendResult <= 0)
            {
                sendResult = (int)ResultCodes.KeyPressFailed;
            }

            return sendResult;
        }
    }
}
