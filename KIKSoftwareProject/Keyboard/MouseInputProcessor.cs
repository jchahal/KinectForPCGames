using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Microsoft.Research.Kinect.Nui;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using log4net.Appender;

namespace KIKSoftwareProject
{
    /// <summary>
    /// Provides methods that take the skeleton coordinates and 
    /// perform the mouse movements at the given mouse coordinates
    /// </summary>
    public class MouseInputProcessor
    {

        static readonly Object _threadLock = new Object();
        log4net.ILog mouseLogger = null;

        private Thread mouseProcessorThread = null;
        private Semaphore handleRequests = null;
        private Queue<MouseData> dataQueue = null;
        private volatile bool threadExit; // Any variable that can be changed by other threads has to be volatile (if not using lock)
        private Semaphore requestMouseData = null;
        private KeyboardSendInput sendInput = null;
        private GameDataProcessor.GameInfo gameInfo = null;
        private GameDataProcessor gameOperation = null;
        private String gameName = "iw3sp";
        private List<int> pressedClicks = new List<int>();
       
        /// <summary>
        /// Configure the logger
        /// </summary>
        void ConfigureLogManager()
        {

            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger("KIKSoftwareProject.MouseInputProcessor") as Logger;
            coreLogger.Parent = rootLogger;
            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = "Mouse_Thread_Logs.txt";
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();
            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            hierarchy.Configured = true;

            mouseLogger = LogManager.GetLogger("KIKSoftwareProject.MouseInputProcessor");
            
        }


        public MouseInputProcessor()
        {
            ConfigureLogManager();
           
            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestMouseData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            
            sendInput = new KeyboardSendInput();           
            gameOperation = new GameDataProcessor();            
            dataQueue = new Queue<MouseData>();
            threadExit = false;
            
            mouseProcessorThread = new Thread(HandleMouseInput);
            mouseProcessorThread.Name = "MouseProcessorThread";
            mouseProcessorThread.Start();         

        }

        /// <summary>
        /// class to hold mouse event data
        /// </summary>
        public class MouseData
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
                get{ return xCoordinate;}
                set { xCoordinate = value; }
            }

            public int YCoordinate
            {
                get { return yCoordinate; }
                set { yCoordinate = value; }
            }
        }

        /// <summary>
        /// Method to close the running mouse processor thread
        /// </summary>
        public void ExitThread()
        {
            this.threadExit = true;
            handleRequests.Release();
        }

        /// <summary>
        /// Add the new object to the queue for processing
        /// </summary>
        /// <param name="mouseData">object with new data</param>
        /// <returns>number of outstanding requests</returns>
        public int AddToQueue(MouseData mouseData)
        {
            //Make sure spot is available in the queue
            this.requestMouseData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(mouseData);
            }

            //Increament the semaphore to indicate there is job to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }

        /// <summary>
        /// Returns the new object to process
        /// </summary>
        /// <returns>new data object</returns>
        private MouseData RemoveFromQueue()
        {
            MouseData resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            this.requestMouseData.Release();

            return resultValue;
        }

        /// <summary>
        /// Give count of the queue
        /// </summary>
        /// <returns>number of objects in the queue at any given time</returns>
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
        /// Worker thread that handles the new mouse event tasks
        /// </summary>
        private void HandleMouseInput()
        {
            try
            {
                mouseLogger.Info("Inside Mouse thread.");
                int searchResult = gameOperation.SearchRunningGame(gameName);

                if (searchResult != (int)ResultCodes.Success)
                {
                    this.mouseLogger.Fatal("Game interested: " + gameName + "is not running");
                    this.threadExit = true;
                    return;
                }

                gameInfo = gameOperation.GameSetFocus(gameName);

                do
                {

                    handleRequests.WaitOne();

                    int queueCount = GetQueueCount();


                    if (queueCount > 0)
                    {

                        MouseData newMouseInput = RemoveFromQueue();
                        mouseLogger.Debug("Removed data from queue -:");
                        mouseLogger.Debug("key = " + newMouseInput.MouseAction + " , Persistance = " + newMouseInput.KeyPersistance);
                        ProcessMouseInput(newMouseInput);
                    }

                } while (!threadExit);
            }
            catch (Exception ex)
            {
                mouseLogger.Fatal("Exception in Keyboard handler thread = " + ex.Message);
            }
        }

        /// <summary>
        /// Processes the new data received from the queue and decodes the command to be sent
        /// </summary>
        /// <param name="inputData">new data object</param>
        /// <returns>result of the processing</returns>
        private int ProcessMouseInput(MouseData inputData)
        {
            int mouseResult = (int)ResultCodes.Success;

            do
            {
                if (inputData == null)
                {
                    mouseResult = (int)ResultCodes.OutOfMemory;
                    mouseLogger.Fatal("Error: The element removed from the Queue is NULL");
                    break;
                }

                if (inputData.MouseAction == (int)MouseButton.MOUSE_MOVE)
                {
                    mouseResult = SendMouseCommand(inputData.MouseAction, gameInfo.GetGameHandle(), inputData.XCoordinate , inputData.YCoordinate, inputData.KeyPersistance );
                    break;
                }

                if (inputData.MouseAction == (int)MouseButton.LEFT_MOUSE_BUTTON && !pressedClicks.Exists(value => value.Equals(inputData.MouseAction)))
                {
                    mouseResult = SendMouseCommand(inputData.MouseAction, gameInfo.GetGameHandle(), -1, -1, inputData.KeyPersistance);
                    break;
                }

                if (inputData.MouseAction == (int)MouseButton.RIGHT_MOUSE_BUTTON && !pressedClicks.Exists(value => value.Equals(inputData.MouseAction)))
                {
                    mouseResult = SendMouseCommand(inputData.MouseAction, gameInfo.GetGameHandle(), -1, -1, inputData.KeyPersistance);
                    break;
                }

                if (inputData.MouseAction == (int)MouseButton.LEFT_MOUSE_BUTTON && inputData.KeyPersistance == (int)MousePresistance.RELEASE)
                {
                    mouseResult = SendMouseCommand(inputData.MouseAction, gameInfo.GetGameHandle(), -1, -1, inputData.KeyPersistance);
                    break;
                }

                if (inputData.MouseAction == (int)MouseButton.RIGHT_MOUSE_BUTTON && inputData.KeyPersistance == (int)MousePresistance.RELEASE)
                {
                    mouseResult = SendMouseCommand(inputData.MouseAction, gameInfo.GetGameHandle(), -1, -1, inputData.KeyPersistance);
                    break;
                }

            } while (false);

            return mouseResult;
        }

        /// <summary>
        /// Sends the respective command to the game
        /// </summary>
        /// <param name="mouseButton">Points to specific mouse button</param>
        /// <param name="windowHandle">Current handle of the game window</param>
        /// <param name="xCoordinate">x coordinate to click</param>
        /// <param name="yCoordinate">y coordinate to click</param>
        /// <param name="presistance">Click hold, or click release etc. actions</param>
        /// <returns></returns>
        private int SendMouseCommand(int mouseButton, IntPtr windowHandle, int xCoordinate, int yCoordinate, int presistance)
        {
            int sendResult = (int)ResultCodes.KeyPressFailed;
            do
            {
                if (mouseButton == (int)MouseButton.MOUSE_MOVE)
                {
                    sendResult = sendInput.MoveMouse(xCoordinate, yCoordinate, windowHandle);
                    break;
                }
                 
                if (mouseButton == (int)MouseButton.LEFT_MOUSE_BUTTON)
                {
                    if (presistance == (int)MousePresistance.PRESS_AND_RELEASE)
                    {
                        sendResult = sendInput.MouseClickAndRelease(mouseButton, windowHandle);
                        this.mouseLogger.Debug("Recevived KeyAction = LEFT_CLICK_PRESS and release");
                        break;
                    }

                    else if (presistance == (int)MousePresistance.DOUBLE_CLICK_HOLD)
                    {
                        sendResult = sendInput.MouseClickAndRelease(mouseButton, windowHandle);

                        sendResult = sendInput.MouseClick(mouseButton, (int)MousePresistance.PRESS_AND_HOLD, windowHandle);
                        pressedClicks.Add(mouseButton);
                        this.mouseLogger.Debug("Recevived KeyAction = LEFT double click and hold");
                        break;
                    }

                    else if (presistance == (int)MousePresistance.RELEASE)
                    {
                        sendResult = sendInput.MouseClick(mouseButton,(int)MousePresistance.RELEASE, windowHandle);
                        pressedClicks.Remove(mouseButton);
                        break;
                    }

                    else if (presistance == (int)MousePresistance.PRESS_AND_HOLD)
                    {
                        sendResult = sendInput.MouseClick(mouseButton, presistance, windowHandle);
                        pressedClicks.Add(mouseButton);
                        this.mouseLogger.Debug("Recevived KeyAction = LEFT_CLICK click and hold");
                        break;
                    }

                    else
                    {
                        sendResult = (int)ResultCodes.InvalidPersistance;
                        break;
                    }
                }                

                if (mouseButton == (int)MouseButton.RIGHT_MOUSE_BUTTON)
                {
                    if (presistance == (int)MousePresistance.PRESS_AND_RELEASE)
                    {
                        sendResult = sendInput.MouseClickAndRelease(mouseButton, windowHandle);
                        break;
                    }
                    else if (presistance == (int)MousePresistance.RELEASE)
                    {
                        sendResult = sendInput.MouseClick(mouseButton, (int)MousePresistance.RELEASE, windowHandle);
                        pressedClicks.Remove(mouseButton);
                        break;
                    }
                    else if (presistance == (int)MousePresistance.PRESS_AND_HOLD)
                    {
                        sendResult = sendInput.MouseClick(mouseButton, presistance, windowHandle);
                        pressedClicks.Add(mouseButton);
                        this.mouseLogger.Debug("Recevived KeyAction = RIGHT button and hold");
                        break;
                    }

                    else
                    {
                        sendResult = (int)ResultCodes.InvalidPersistance;
                        break;
                    }
                }

            } while (false);

            if (sendResult <= 0)
            {
                mouseLogger.Fatal("Mouse action failed: " + mouseButton);
                sendResult = (int)ResultCodes.KeyPressFailed;
            }

            return sendResult;
        }
    }
}
