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
using KIKSoftwareProject;


namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Base class for all the gestures and contains the thread implementation and logging configuration and objects creation
    /// </summary> 
    class GestureBase
    {
        private static readonly Object _threadLock = new Object();
        protected log4net.ILog logger = null;
        protected GestureThresholdValues gestureThresholds = new GestureThresholdValues();

        private Thread gestureThread = null;
        protected Semaphore handleRequests = null;
        private Queue<NuiElement> dataQueue = null;
        protected volatile bool threadExit; // Any variable that can be changed by other threads has to be volatile (if not using lock)
        private Semaphore requestKeyboardData = null;

        public event NuiKeyboardGestureEvent keyboardGestureAction;

        public event NuiMouseGestureEvent mouseGestureAction;

        public class NuiElement
        {
            SkeletonFrame frame;
            DateTime timeStamp;

            public SkeletonFrame GetSkeletonFrame()
            {
                return this.frame;
            }

            public void SetSkeletonFrame(SkeletonFrame extframe)
            {
                this.frame = extframe;
                return;
            }

            public DateTime GetTimeStamp()
            {
                return this.timeStamp;
            }

            public void SetTimeStamp(DateTime extTS)
            {
                this.timeStamp = extTS;
                return;
            }
        }

        protected void RaiseKeyboardEvent(KeyboardEventData eventData)
        {
            if (keyboardGestureAction != null) keyboardGestureAction(this, eventData);
        }

        protected void RaiseMouseEvent(MouseEventData eventData)
        {
            if (mouseGestureAction != null) mouseGestureAction(this, eventData);
        }


        private void ConfigureLogManager(String logName, String loggerName)
        {

            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger(loggerName) as Logger;
            coreLogger.Parent = rootLogger;
            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = logName;
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();


            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            hierarchy.Configured = true;

            logger = LogManager.GetLogger(loggerName);

        }

        public GestureBase(String threadName, String logName, String loggerName)
        {
            ConfigureLogManager(logName, loggerName);

            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestKeyboardData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);

            dataQueue = new Queue<NuiElement>();
            threadExit = false;

            gestureThread = new Thread(GestureAnalyzer);
            gestureThread.Name = threadName;
            gestureThread.Start();
        }

        protected virtual void GestureAnalyzer()
        {
            return;
        }

        public void ExitThread()
        {
            this.threadExit = true;
            handleRequests.Release();
        }

        public int AddToQueue(NuiElement nuiData)
        {
            //Make sure spot is available in the queue
            this.requestKeyboardData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(nuiData);
            }

            //Increament the semaphore to indicate there is job to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }

        protected NuiElement RemoveFromQueue()
        {
            NuiElement resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            this.requestKeyboardData.Release();

            return resultValue;
        }

        protected int GetQueueCount()
        {
            int count = 0;

            lock (_threadLock)
            {
                count = dataQueue.Count;
            }

            return count;
        }
    }

    public delegate void NuiKeyboardGestureEvent(object sender, KeyboardEventData eventData);

    public delegate void NuiMouseGestureEvent(object sender, MouseEventData eventData);
    
    
    public class KeyboardEventData : EventArgs
    {
        int gestureID = -1;
        int keyboardAction = -1;
        int keyboardPersistance = -1;

        public int GetGestureID()
        {
            return this.gestureID;
        }

        public void SetGestureID(int newGestureID)
        {
            this.gestureID = newGestureID;
        }

        public int GetKeyboradAction()
        {
            return this.keyboardAction;
        }

        public void SetKeyboardAction(int newKeyboardAction)
        {
            this.keyboardAction = newKeyboardAction;
        }

        public int GetKeyboardPersistance()
        {
            return this.keyboardPersistance;
        }

        public void SetKeyboardPersistance(int newKeyboardPersistance)
        {
            this.keyboardPersistance = newKeyboardPersistance;
        }
    }

    public class MouseEventData : EventArgs
    {
        //Keyboard button to be pressed 
        private int mouseAction = -1;

        //persistance of the keyboard action
        private int mousePersistance = -1;

        private int xCoordinate;

        private int yCoordinate;

        public int MouseAction
        {
            get { return mouseAction; }
            set { mouseAction = value; }
        }

        public int KeyPersistance
        {
            get { return this.mousePersistance; }
            set { this.mousePersistance = value; }
        }

        public int XCoordinate
        {
            get { return xCoordinate; }
            set { xCoordinate = value; }
        }

        public int YCoordinate
        {
            get { return yCoordinate; }
            set { yCoordinate = value; }
        }
    }
}
