using System;
using Microsoft.Research.Kinect.Nui;
using System.Windows.Controls;
using Microsoft.Speech.Recognition;
using System.Threading;
using log4net;
using log4net.Repository.Hierarchy;
using KIKSoftwareProject.Skeletal;


namespace KIKSoftwareProject
{
    /// <summary>
    /// Main class that signs up for different Kinect events 
    /// and passes the data to different threads once data is recieved for an event
    /// Closes all the threads once the application is exiting
    /// </summary>
    class KinectNuiInterface
    {
        private log4net.ILog kinectEventLogger = null;
        
        private Runtime nuiRuntime = null;
        private DateTime appStartTime = DateTime.MinValue;
        private DepthDataProcessor depthProcessor = null;
        private VideoDataProcessor videoProcessor = null;
        private SkeletalDataProcessor skeletalProcessor = null;
        private KeyboardInputProcessor keyboardProcessor = null;
        private MouseInputProcessor mouseProcessor = null;
        private SoundDataProcessor soundProcessor = null;

        private bool stopVisualThread = false;
        private bool stopAudioThread = false;

        /// <summary>
        /// Configures the logger using the log4net library functions
        /// </summary>
        void ConfigureLogManager()
        {
            
            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger("KIKSoftwareProject.KinectNuiInterface") as Logger;
            coreLogger.Parent = rootLogger;

            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = "Main_Thread_Logs.txt";
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();

            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            hierarchy.Configured = true;

            kinectEventLogger = LogManager.GetLogger("KIKSoftwareProject.KinectNuiInterface");

            log4net.LogManager.GetRepository().Threshold = log4net.Core.Level.Debug;       

        }

        public bool StopVisualThread
        {
            get { return this.stopVisualThread; }

            set { this.stopVisualThread = value; }
        }

        public bool StopAudioThread
        {
            get { return this.stopAudioThread; }

            set { this.stopAudioThread = value; }
        }


        /// <summary>
        /// Sets up the Kinect events and starts all the worker threads that do all the processing
        /// </summary>
        /// <param name="video">Iamge object from the GUI thread</param>
        /// <param name="skeleton">Canvas object from the GUI thread</param>
        /// <param name="transformedSkeleton">Second canvas object from the GUi thread</param>
        public KinectNuiInterface(Image video, Canvas skeleton, Canvas transformedSkeleton)
        {
            try
            {
                nuiRuntime = Runtime.Kinects[0];// new Runtime();
                ConfigureLogManager();
                kinectEventLogger.Info("Sucessfully configured log manager.");
                
            }
            catch(InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Sorry!!!!!!Failed to configure the Logger");
            }

            try
            {
                kinectEventLogger.Info("Intializing nuiRuntime object");
                nuiRuntime.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
                //to experiment, toggle TransformSmooth between true & false
                // parameters used to smooth the skeleton data
                ///*
                nuiRuntime.SkeletonEngine.TransformSmooth = true;
                TransformSmoothParameters parameters = new TransformSmoothParameters();
                parameters.Smoothing = 0.0f;
                parameters.Correction = 0.0f;
                parameters.Prediction = 0.0f;
                parameters.JitterRadius = 0.0f;
                parameters.MaxDeviationRadius = 0.0f;
                //nuiRuntime.SkeletonEngine.SmoothParameters = parameters; ///*
            }

            catch (InvalidOperationException)
            {
                kinectEventLogger.Fatal("Error - Intialization failed, make sure Kinect is plugged in.");

                System.Windows.MessageBox.Show("Kinect NUI initialization failed. Please make sure Kinect device is plugged in.");
                nuiRuntime = null;
                return;
            }

            try
            {
                kinectEventLogger.Info("Sucessfully intialized Kinect runtime object");
                kinectEventLogger.Info("Opening the video and depth streams");
                nuiRuntime.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                nuiRuntime.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            }
            catch (InvalidOperationException)
            {
                kinectEventLogger.Fatal("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                System.Windows.MessageBox.Show("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                return;
            }

            try
            {
                kinectEventLogger.Debug("Signing up for the runtime events");
                //nuiRuntime.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nuiRuntime_DepthFrameReady);
                nuiRuntime.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nuiRuntime_SkeletonFrameReady);
                nuiRuntime.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nuiRuntime_VideoFrameReady);

                appStartTime = DateTime.Now;
                kinectEventLogger.Debug("App start time = " + appStartTime.TimeOfDay.ToString());

                kinectEventLogger.Debug("Creating KeyboardProcessor object");
                //create keyboard thread to process keyboard inputs
                keyboardProcessor = new KeyboardInputProcessor();

                //create mouse thread to process mouse input
                mouseProcessor = new MouseInputProcessor();

                //create the Depth processor object
                //depthProcessor = new DepthDataProcessor(keyboardProcessor);

                //create the video processor object
                kinectEventLogger.Debug("Creating videoProcessor object");
                videoProcessor = new VideoDataProcessor(video);

                //create object for skeleton processor
                kinectEventLogger.Debug("Creating skeletalProcessor object");
                skeletalProcessor = new SkeletalDataProcessor(nuiRuntime, skeleton, keyboardProcessor, mouseProcessor, transformedSkeleton);

                kinectEventLogger.Debug("Intializing Sound object intializer thread");
                Thread soundInitializerThread = new Thread(IntializerThreadFunc);
                soundInitializerThread.SetApartmentState(ApartmentState.MTA);
                soundInitializerThread.Start();

                soundInitializerThread.Join(); //Parent waiting on child to finish 
                kinectEventLogger.Info("Successfully intialized all the runtime objects");
                kinectEventLogger.Info("Ready to receive events.......");

            }

            catch (Exception ex)
            {
                kinectEventLogger.Fatal("OOps!!! exception: " + ex.Message);
            }           
        }

        /// <summary>
        /// Thread function that intializes and signs up for Sound events for Kinect
        /// Required because of MTA thread state
        /// </summary>
        void IntializerThreadFunc()
        {
            try
            {
                soundProcessor = new SoundDataProcessor(keyboardProcessor);

                SpeechRecognitionEngine sre = soundProcessor.GetSreInstance();
                sre.SpeechRecognized += SreSpeechRecognized;
            }
            catch (Exception ex)
            {
                kinectEventLogger.Fatal("Sound initializer thread failed = " + ex.Message);
            }
        }       

        /// <summary>
        /// Kinect event called when new skeleton is ready and adds the object to skeleton processor thread queue
        /// </summary>
        /// <param name="sender">sender object</param>
        /// <param name="e">new skeleton frame data</param>
        void nuiRuntime_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            kinectEventLogger.Debug("Skeleton frame event received");

            skeletalProcessor.StopVisualThread = this.stopVisualThread;

            SkeletalDataProcessor.SkeletalData newSkeletalData = new SkeletalDataProcessor.SkeletalData();
            newSkeletalData.SkeletonFrame = e.SkeletonFrame;

            kinectEventLogger.Debug("Adding the the object to skeletal thread queue");
            skeletalProcessor.AddToQueue(newSkeletalData);
            kinectEventLogger.Debug("Successfully added to thread queue");            
        }

        /// <summary>
        /// Kinect event called when new video frame is ready and adds object to video processor thread queue
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">new video frame data</param>
        void nuiRuntime_VideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            if (!this.stopVisualThread)
            {
                VideoDataProcessor.VideoData newVideoData = new VideoDataProcessor.VideoData();
                newVideoData.ImageFrame = e.ImageFrame;

                videoProcessor.AddToQueue(newVideoData);
            }
            
        }

        /// <summary>
        /// Kinect event called when Sound command is recognised and adds the object to sound processor thread queue
        /// </summary>
        /// <param name="sender">sender object</param>
        /// <param name="e">new sound command data</param>
       void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //This first release of the Kinect language pack doesn't have a reliable confidence model, so 
            //we don't use e.Result.Confidence here.
            if (!this.stopAudioThread)
            {
                if (e.Result.Confidence > 0.95)
                {
                    kinectEventLogger.Debug("Received Speech event with command: " + e.Result.Text);
                    kinectEventLogger.Debug("Confidence is: " + e.Result.Confidence);

                    SoundDataProcessor.SoundData newSoundData = new SoundDataProcessor.SoundData();
                    newSoundData.SetSoundCommand(e.Result.Text);

                    kinectEventLogger.Debug("Adding the the object to sound processor thread queue");
                    soundProcessor.AddToQueue(newSoundData);
                    kinectEventLogger.Debug("Sucessfully added to the queue");
                }
            }
        }       

        /// <summary>
        /// Called when the application is terminating and stops all the threads running gracefully
        /// and unintializes Kinect runtime object
        /// </summary>
        public void TerminateApplication()
        {
            kinectEventLogger.Info("Terminating the application!!!!!!!!!!!");

            if (depthProcessor != null)
            {
                //exit the depth processor thread
                depthProcessor.ExitThread();
            }

            if (keyboardProcessor != null)
            {
                //exit the keyboard processor thread
                keyboardProcessor.ExitThread();
            }

            if (videoProcessor != null)
            {
                //exit the video processor thread
                videoProcessor.ExitThread();
            }

            if (skeletalProcessor != null)
            {
                //exit the skeleton processor thread
                skeletalProcessor.ExitThread();
            }

            if (soundProcessor != null)
            {
                //exit the sound thread
                soundProcessor.ExitThread();
            }

            if (nuiRuntime != null)
            {
                //unintialize the NUI
                nuiRuntime.Uninitialize();
            }     
            
        }
    }
}
