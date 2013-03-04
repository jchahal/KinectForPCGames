using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Microsoft.Research.Kinect.Audio;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using log4net.Appender;

namespace KIKSoftwareProject
{
    /// <summary>
    /// Processes the sound commands received from the Kinect events
    /// </summary>
    class SoundDataProcessor
    {
        private KinectAudioSource kinectAudioResourse = null;
        private RecognizerInfo ri = null;
        private const string recognizerId = "SR_MS_en-US_Kinect_10.0";
        private SpeechRecognitionEngine sre = null;
        private Choices speechCommands = null;
        private GrammarBuilder gb = null;
        private Grammar grammar = null;
        private Stream audioSourceStream = null;
        private KeyboardInputProcessor keyboardProcessor = null;

        static readonly Object _threadLock = new Object();
        private Thread soundProcessorThread = null;
        private Semaphore handleRequests = null;
        private Queue<SoundData> dataQueue = null;
        private volatile bool threadExit;
        private Semaphore requestSoundData = null;
        private log4net.ILog soundLogger = null;
        private CommandRecorder previousCommand = new CommandRecorder();
        private Dictionary<int, string> iterations = new Dictionary<int, string> {{1,"one"},{2, "two"},{3,"three"},{4,"four"},{5,"five"}};


        public class CommandRecorder
        {
            public string command;
            public DateTime time;
        }

        void ConfigureLogManager()
        {

            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger("KIKSoftwareProject.SoundDataProcessor") as Logger;
            coreLogger.Parent = rootLogger;
            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = "Sound_Thread_Logs.txt";
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();


            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            
            hierarchy.Configured = true;

            soundLogger = LogManager.GetLogger("KIKSoftwareProject.SoundDataProcessor");            
        }


        public SoundDataProcessor(KeyboardInputProcessor keyboard)
        {

            ConfigureLogManager();
            keyboardProcessor = keyboard;
            kinectAudioResourse = new KinectAudioSource();
            kinectAudioResourse.FeatureMode = true;
            kinectAudioResourse.AutomaticGainControl = false; //Important to turn this off for speech recognition
            kinectAudioResourse.SystemMode = SystemMode.OptibeamArrayOnly; //No AEC for this sample


            ri = SpeechRecognitionEngine.InstalledRecognizers().Where(r => r.Id == recognizerId).FirstOrDefault();

            if (ri == null)
            {
                Trace.WriteLine("Could not find speech recognizer: {0}. Please refer to the sample requirements.", recognizerId);
                throw new System.InvalidOperationException("Could not find speech recognizer: {0}. Please refer to the sample requirements." + recognizerId);
            }

            sre = new SpeechRecognitionEngine(ri.Id);
            speechCommands = new Choices();

            speechCommands.Add("jump");
            speechCommands.Add("reload");
            speechCommands.Add("aim");
            speechCommands.Add("knife");
            speechCommands.Add("grenade");

            speechCommands.Add("menu");
            speechCommands.Add("pause");
            speechCommands.Add("select");
            speechCommands.Add("okay");
            speechCommands.Add("enter");
            speechCommands.Add("up");
            speechCommands.Add("down");
            speechCommands.Add("left");
            speechCommands.Add("right");

            gb = new GrammarBuilder();
            gb.Culture = ri.Culture;
            gb.Append(speechCommands);

            grammar = new Grammar(gb);
            sre.LoadGrammar(grammar);

            audioSourceStream = kinectAudioResourse.Start();
            sre.SetInputToAudioStream(audioSourceStream,
                                                  new SpeechAudioFormatInfo(
                                                      EncodingFormat.Pcm, 16000, 16, 1,
                                                      32000, 2, null));
            sre.RecognizeAsync(RecognizeMode.Multiple);
            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestSoundData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);

            dataQueue = new Queue<SoundData>();
            threadExit = false;

            soundProcessorThread = new Thread(SoundProcessorModule);
            soundProcessorThread.Name = "SoundProcessorThread";
            soundProcessorThread.SetApartmentState(ApartmentState.MTA);
            soundProcessorThread.Start();

        }
            
    

        public SpeechRecognitionEngine GetSreInstance()
        {
            return this.sre;
        }

        public class SoundData
        {
            String soundCommand;

            public void SetSoundCommand(String command)
            {
                soundCommand = command;
            }

            public String GetSoundCommand()
            {
                return soundCommand;
            }
        }

        public void ExitThread()
        {
            threadExit = true;
            sre.RecognizeAsyncStop();  
            handleRequests.Release();
        }


        public int AddToQueue(SoundData soundData)
        {

            //Decrement the semaphore to make sure the spot is available
            this.requestSoundData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(soundData);
            }

            //Increament the semaphore to indicate there is work to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }


        private SoundData RemoveFromQueue()
        {
            SoundData resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            int previousCount = this.requestSoundData.Release();

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

        private void SoundProcessorModule()
        {
            int processResult = (int)ResultCodes.Success;
            try
            {
                do
                {
                    soundLogger.Debug("Before Waitone function call");
                    handleRequests.WaitOne();

                    if (GetQueueCount() > 0)
                    {
                        SoundData newSoundData = RemoveFromQueue();
                        processResult = ProcessNewSoundData(newSoundData);
                    }

                } while (!threadExit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private int ProcessNewSoundData(SoundData data)
        {
            
            if (data.GetSoundCommand().CompareTo("jump") == 0)
            {

                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.JUMP;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding Jump sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }
            if (data.GetSoundCommand().CompareTo("reload") == 0)
            {

                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.RELOAD;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding Jump sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }            
            if (data.GetSoundCommand().CompareTo("knife") == 0)
            {

                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.KNIFE;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding KNIFE sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }
            if (data.GetSoundCommand().CompareTo("grenade") == 0)
            {

                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.GRENADE;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding grenade sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }

            if (data.GetSoundCommand().CompareTo("menu") == 0 || data.GetSoundCommand().CompareTo("pause") == 0)
            {
                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.ESC;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding Menu/Pause sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }
            if (data.GetSoundCommand().CompareTo("select") == 0 || data.GetSoundCommand().CompareTo("okay") == 0 || data.GetSoundCommand().CompareTo("enter") == 0)
            {
                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.ENTER;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding select/okay/enter sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }
            if (data.GetSoundCommand().CompareTo("up") == 0)
            {
                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.UP_ARROW;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding up sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }

            if (data.GetSoundCommand().CompareTo("down") == 0)
            {
                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.DOWN_ARROW;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding down sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }

            if (data.GetSoundCommand().CompareTo("right") == 0)
            {
                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.RIGHT_ARROW;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding right command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }

            if (data.GetSoundCommand().CompareTo("left") == 0)
            {
                KeyboardInputProcessor.KeyboardData newKeyboardData = new KeyboardInputProcessor.KeyboardData();
                newKeyboardData.KeyboardAction = (int)KeyboardAction.LEFT_ARROW;
                newKeyboardData.KeyPersistance = (int)KeyboardPersistance.PRESS_AND_RELEASE;

                soundLogger.Debug("Adding left sound command to the keyboard queue");
                keyboardProcessor.AddToQueue(newKeyboardData);
                soundLogger.Debug("Successfully added");
            }

            
            return (int)ResultCodes.Success;
        }


        void pressMuliple(int iterations, KeyboardInputProcessor.KeyboardData newKeyboardData)
        {

            for (int i = 0; i < iterations; i++) 
            {
                keyboardProcessor.AddToQueue(newKeyboardData);
            }
        }

    }
}
