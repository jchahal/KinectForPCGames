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
    /// Class provides methods to send the specific key strokes to the specified games
    /// and converts the generic movements into specific keystrokes on the keyboard
    /// </summary>
    class KeyboardInputProcessor
    {
        static readonly Object _threadLock = new Object();
        log4net.ILog keyboardLogger = null;        

        private Thread keyboardProcessorThread = null;
        private Semaphore handleRequests = null;
        private Queue<KeyboardData> dataQueue = null;
        private volatile bool threadExit; // Any variable that can be changed by other threads has to be volatile (if not using lock)
        private Semaphore requestKeyboardData = null;
        private KeyboardSendInput sendInput = null;
        private GameDataProcessor.GameInfo gameInfo = null;
        private GameDataProcessor gameOperation = null;
        private String gameName = "iw3sp";
        List<int> keysPressed = null;

        /// <summary>
        /// Configures the logger using the log4net library functions
        /// </summary>
        void ConfigureLogManager()
        {

            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger("KIKSoftwareProject.KeyboardInputProcessor") as Logger;
            coreLogger.Parent = rootLogger;
            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = "Keyboard_Thread_Logs.txt";
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();


            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            hierarchy.Configured = true;

            keyboardLogger = LogManager.GetLogger("KIKSoftwareProject.KeyboardInputProcessor");
            //log4net.LogManager.GetRepository().Threshold = log4net.Core.Level.Debug;
        }


        public KeyboardInputProcessor()
        {
            ConfigureLogManager();
           
            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestKeyboardData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            
            sendInput = new KeyboardSendInput();           
            gameOperation = new GameDataProcessor();            
            dataQueue = new Queue<KeyboardData>();
            threadExit = false;
            keysPressed = new List<int>();

            keyboardProcessorThread = new Thread(HandleKeyBoardInput);
            keyboardProcessorThread.Name = "KeyboardProcessorThread";
            keyboardProcessorThread.Start();         

        }

        /// <summary>
        /// Gives the list of keys that are currently pressed
        /// </summary>
        /// <returns>list</returns>
        public List<int> GetKeysPressed()
        {
            return this.keysPressed;
        }
        
        /// <summary>
        /// Class to hold the keyboard event data
        /// </summary>
        public class KeyboardData
        {
            //Keyboard button to be pressed 
            private int keyboardAction = -1;

            //persistance of the keyboard action
            private int keyPersistance = -1;

            public int KeyboardAction
            {
                get { return keyboardAction; }
                set { keyboardAction = value; }
            }

            public int KeyPersistance
            {
                get { return this.keyPersistance; }
                set { this.keyPersistance = value; }
            }

        }

        /// <summary>
        /// Close the running thread
        /// </summary>
        public void ExitThread()
        {
            this.threadExit = true;
            handleRequests.Release();
        }

        /// <summary>
        /// Add the new object to the queue for processing
        /// </summary>
        /// <param name="keyboardData">object with new data</param>
        /// <returns>number of outstanding requests</returns>
        public int AddToQueue(KeyboardData keyboardData)
        {
            //Make sure spot is available in the queue
            this.requestKeyboardData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(keyboardData);
            }

            //Increament the semaphore to indicate there is job to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }

        /// <summary>
        /// Returns the new object to process
        /// </summary>
        /// <returns>new data object</returns>
        private KeyboardData RemoveFromQueue()
        {
            KeyboardData resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            this.requestKeyboardData.Release();

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
        /// Main worker thread that handles all the new data arriving
        /// </summary>
        private void HandleKeyBoardInput()
        {
            try
            {
                keyboardLogger.Info("Inside Keyboard thread.");

                keyboardLogger.Info("Searching for the game interested: " + gameName);
                int searchResult = gameOperation.SearchRunningGame(gameName);

                if (searchResult != (int)ResultCodes.Success)
                {
                    keyboardLogger.Fatal("Game interested: " + gameName + "is not running");
                    this.threadExit = true;
                    return;
                }

                keyboardLogger.Debug("Game interested: " + gameName + " has been found");

                keyboardLogger.Info("Setting the focus to the game: " + gameName);
                gameInfo = gameOperation.GameSetFocus(gameName);

                if (gameInfo == null)
                {
                    keyboardLogger.Fatal("Failed to set the focus of the game!!!!!!!");
                    return;
                }

                keyboardLogger.Info("Focus to the game successfully set.");

                do
                {
                    
                    handleRequests.WaitOne();

                    int queueCount = GetQueueCount();
                    

                    if (queueCount > 0)
                    {
                        
                        KeyboardData newKeyboardInput = RemoveFromQueue();
                        keyboardLogger.Debug("Removed data from queue -:");
                        keyboardLogger.Debug("key = " + newKeyboardInput.KeyboardAction + " , Persistance = " + newKeyboardInput.KeyPersistance);
                        ProcessKeyboardInput(newKeyboardInput);
                    }

                } while (!threadExit);
            }
            catch (Exception ex)
            {
                keyboardLogger.Fatal("Exception in Keyboard handler thread = " + ex.Message);
            }
        }

        /// <summary>
        /// Processes the new keyboard data received from the queue
        /// </summary>
        /// <param name="inputData">new object with data</param>
        /// <returns>processing result</returns>
        private int ProcessKeyboardInput(KeyboardData inputData)
        {
            int keyboardResult = (int)ResultCodes.Success;

            do
            {
                if (inputData == null)
                {
                    keyboardResult = (int)ResultCodes.OutOfMemory;
                    keyboardLogger.Fatal("Error: The element removed from the Queue is NULL");
                    break;
                }

                if (inputData.KeyboardAction == (int)KeyboardAction.STOP)
                {
                    keyboardResult = HandleStopCommand();
                    break;
                }

                //if key is not pressed then do stuff or we'll calling release on pressed key
                if (!keysPressed.Exists(value => value.Equals(inputData.KeyboardAction)) || inputData.KeyPersistance == (int)KeyboardPersistance.RELEASE)
                {
                    keyboardLogger.Debug("Keyboard Command received = " + inputData.KeyboardAction);
                    UInt16 scanCode = GetKeyScanCode(inputData.KeyboardAction);
                    keyboardLogger.Debug("Keyboard scan code = " + scanCode);

                    if (scanCode == (UInt16)KeyBoardScanCodes.ScanCode.DIK_UNKNOWN)
                    {
                        keyboardResult = (int)ResultCodes.KeyboardCommandInvalid;
                        break;
                    }

                    keyboardLogger.Debug("Sending command to the game");

                    keyboardResult = SendKeyboardCommand(inputData.KeyPersistance, gameInfo.GetGameHandle(), scanCode, inputData.KeyboardAction);

                    keyboardLogger.Debug("Successfully sent the command to the game");
                }
                else
                {
                    keyboardLogger.Debug("The key is already pressed = " + inputData.KeyboardAction);
                }

            } while (false);

            return keyboardResult;
        }

        /// <summary>
        /// Handles the stop command by releasing all the keys that has been pressed
        /// </summary>
        /// <returns>processing result</returns>
        private int HandleStopCommand()
        {
            int stopResult = (int)ResultCodes.Success;
            do
            {
                if (this.keysPressed.Count == 0)
                {
                    stopResult = (int)ResultCodes.Success;
                    break;
                }

                foreach (int key in this.keysPressed)
                {
                    keyboardLogger.Debug("Keyboard action removing = " + key);
                    UInt16 scanCode = GetKeyScanCode(key);
                    keyboardLogger.Debug("Keyboard scan code = " + scanCode);


                    if (scanCode == (UInt16)KeyBoardScanCodes.ScanCode.DIK_UNKNOWN)
                    {
                        stopResult = (int)ResultCodes.KeyboardCommandInvalid;
                        break;
                    }

                    int sendInputResult = sendInput.ReleaseKey(gameInfo.GetGameHandle(), scanCode);

                    if (sendInputResult <= 0)
                    {
                        stopResult = (int)ResultCodes.KeyReleaseFailed;
                        keyboardLogger.Fatal("Key Release failed = " + scanCode);
                        break;
                    }
                }

                this.keysPressed.Clear();

            } while (false);

            return stopResult;
        }

        /// <summary>
        /// Sends the keyboard specific command
        /// </summary>
        /// <param name="keyAction">Action to be performed with the key e.g. press/release etc.</param>
        /// <param name="windowHandle">current handle of the game window</param>
        /// <param name="scanCode">code of the keyboard key</param>
        /// <param name="currKeyboardCommand">current keyboard command</param>
        /// <returns>processing result</returns>
        private int SendKeyboardCommand(int keyAction, IntPtr windowHandle, UInt16 scanCode, int currKeyboardCommand)
        {
            int sendResult = (int)ResultCodes.KeyPressFailed;
            do
            {
                if (keyAction == (int)KeyboardPersistance.PRESS)
                {
                    keyboardLogger.Debug("Sending command - Button press");
                    keyboardLogger.Debug("Pressing Key: " + scanCode);
                    sendResult = sendInput.PressKey(windowHandle, scanCode);
                    keysPressed.Add(currKeyboardCommand);
                    break;
                }

                if (keyAction == (int)KeyboardPersistance.PRESS_AND_RELEASE)
                {
                    keyboardLogger.Debug("Sending command - Button press and release");
                    keyboardLogger.Debug("Pressing/Releasing Key: " + scanCode);
                    sendResult = sendInput.PressAndReleaseKey(windowHandle, scanCode);
                    break;
                }

                if (keyAction == (int)KeyboardPersistance.RELEASE)
                {
                    keyboardLogger.Debug("Sending command - Button release");
                    keyboardLogger.Debug("Releasing Key: " + scanCode);
                    sendResult = sendInput.ReleaseKey(windowHandle, scanCode);
                    keysPressed.Remove(currKeyboardCommand);
                    break;
                }

            } while (false);

            if (sendResult <= 0)
            {
                keyboardLogger.Fatal("Keypress failed - scan code - " + scanCode);
                sendResult = (int)ResultCodes.KeyPressFailed;
            }

            return sendResult;
        }

        /// <summary>
        /// Converts a game command into a keyboard scan code
        /// </summary>
        /// <param name="keyboardCommand">current action</param>
        /// <returns>the scan code of the keyboard</returns>
        private UInt16 GetKeyScanCode(int keyboardCommand)
        {
            UInt16 resultCode = 0;

            switch (keyboardCommand)
            {
                case (int)KeyboardAction.MOVE_RIGHT:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_D;
                    break;
                case (int)KeyboardAction.MOVE_LEFT:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_A;
                    break;
                case (int)KeyboardAction.MOVE_UP:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_W;
                    break;
                case (int)KeyboardAction.MOVE_DOWN:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_S;
                    break;
                case (int)KeyboardAction.JUMP:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_SPACE;
                    break;
                case (int)KeyboardAction.RELOAD:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_R;
                    break;
                case (int)KeyboardAction.KNIFE:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_V;
                    break;
                case (int)KeyboardAction.GRENADE:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_G;
                    break;
                case (int)KeyboardAction.ENTER:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_RETURN;
                    break;


                case (int)KeyboardAction.LONG_JUMP:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_Z;
                    break;

                case (int)KeyboardAction.SHOOT:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_Z;
                    break;
                case (int)KeyboardAction.ESC:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_ESCAPE;
                    break;
                case (int)KeyboardAction.UP_ARROW:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_UPARROW;
                    break;
                case (int)KeyboardAction.DOWN_ARROW:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_DOWNARROW;
                    break;
                case (int)KeyboardAction.RIGHT_ARROW:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_RIGHTARROW;
                    break;
                case (int)KeyboardAction.LEFT_ARROW:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_LEFTARROW;
                    break;
                default:
                    resultCode = (UInt16)KeyBoardScanCodes.ScanCode.DIK_UNKNOWN;
                    break;
            }

            return resultCode;
        }
    }
}
