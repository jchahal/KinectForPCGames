using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using Microsoft.Research.Kinect.Nui;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

namespace KIKSoftwareProject
{
    /// <summary>
    /// Inherits from Window class and provides methods to draw points and lines on the screen canvas
    /// </summary>
    class SkeletalDrawing : Window
    {
        private static readonly Object _threadLock = new Object();
        private Thread skeletalDrawingThread = null;
        private Semaphore handleRequests = null;
        private Queue<SkeletalDrawingData> dataQueue = null;
        private volatile bool threadExit;
        private Semaphore requestSkeletalData = null;
        private Runtime nuiRuntime = null;
        private Canvas skeletonToForm = null;

        /// <summary>
        /// Dictionary containing different skeleton joints and the respective colour values
        /// </summary>
        Dictionary<JointID, Brush> jointColors = new Dictionary<JointID, Brush>() { 
            {JointID.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointID.Head, new SolidColorBrush(Color.FromRgb(200, 0,   0))},
            {JointID.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79,  84,  33))},
            {JointID.ElbowLeft, new SolidColorBrush(Color.FromRgb(84,  33,  42))},
            {JointID.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HandLeft, new SolidColorBrush(Color.FromRgb(215,  86, 0))},
            {JointID.ShoulderRight, new SolidColorBrush(Color.FromRgb(33,  79,  84))},
            {JointID.ElbowRight, new SolidColorBrush(Color.FromRgb(33,  33,  84))},
            {JointID.WristRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.HandRight, new SolidColorBrush(Color.FromRgb(37,   69, 243))},
            {JointID.HipLeft, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.KneeLeft, new SolidColorBrush(Color.FromRgb(69,  33,  84))},
            {JointID.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointID.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointID.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222,  76))},
            {JointID.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointID.FootRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))}
        };

        /// <summary>
        /// Class that holds the new Skeleton frame data
        /// </summary>
        public class SkeletalDrawingData
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
   
        public SkeletalDrawing(Runtime nui, Canvas skeleton)
        {
            skeletonToForm = skeleton;
            nuiRuntime = nui;      

            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestSkeletalData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);      

            dataQueue = new Queue<SkeletalDrawingData>();
            threadExit = false;
            skeletalDrawingThread = new Thread(DrawSkeletonModule);
            skeletalDrawingThread.Name = "SkeletalDrawingThread";
            skeletalDrawingThread.SetApartmentState(ApartmentState.STA);
            skeletalDrawingThread.Start();

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
        /// <param name="skeletalDrawingData">new object with the data</param>
        /// <returns>number tasks outstanding in the queue</returns>
        public int AddToQueue(SkeletalDrawingData skeletalDrawingData)
        {

            //Decrement the semaphore to make sure the spot is available
            this.requestSkeletalData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(skeletalDrawingData);
            }

            //Increament the semaphore to indicate there is work to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }

        /// <summary>
        /// Removes the new skeleton data object from the queue
        /// </summary>
        /// <returns>SkeletalDrawing object with new data</returns>
        private SkeletalDrawingData RemoveFromQueue()
        {
            SkeletalDrawingData resultValue = null;

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
        private void DrawSkeletonModule()
        {
            int processResult = (int)ResultCodes.Success;
            try
            {
                do
                {
                    handleRequests.WaitOne();

                    if (GetQueueCount() > 0)
                    {
                        SkeletalDrawingData newSkeletalData = RemoveFromQueue();
                        processResult = ProcessNewSkeletalData(newSkeletalData);
                    }

                } while (!threadExit);
            }

            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Performs the drawing of the object by dispatching the drawing of the frame to the GUI thread
        /// </summary>
        /// <param name="skeletalData">the object with new coordinates to be drawn</param>
        /// <returns>the result value for success or failure</returns>
        private int ProcessNewSkeletalData(SkeletalDrawingData skeletalData)
        {
            
            if (skeletalData == null)
            {
                return (int)ResultCodes.OutOfMemory;
            }
            
            skeletonToForm.Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.FromSeconds(1), new Action(
                () =>
                {
                    try
                    {
                        SkeletonFrame skeletonFrame = skeletalData.GetSkeletonFrame();

                        Brush[] brushes = new Brush[6];
                        int iSkeleton = 0;

                        brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                        brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                        brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
                        brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
                        brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
                        brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

                        skeletonToForm.Children.Clear();

                        foreach (SkeletonData data in skeletonFrame.Skeletons)
                        {
                            if (SkeletonTrackingState.Tracked == data.TrackingState)
                            {
                                Brush brush = brushes[iSkeleton % brushes.Length];

                                //Add different line segments
                                AddLineSegments(data, brush);

                                // Draw joints
                                foreach (Joint joint in data.Joints)
                                {
                                    Point jointPos = getDisplayPosition(joint);
                                    Line jointLine = new Line();
                                    jointLine.X1 = jointPos.X - 3;
                                    jointLine.X2 = jointLine.X1 + 6;
                                    jointLine.Y1 = jointLine.Y2 = jointPos.Y;
                                    jointLine.Stroke = jointColors[joint.ID];
                                    jointLine.StrokeThickness = 6;

                                    skeletonToForm.Children.Add(jointLine);

                                }
                            }

                            iSkeleton++;
                        }
                    }
                    catch
                    {
                        
                    }
                }));

            return (int)ResultCodes.Success;
        }

        private void AddLineSegments(SkeletonData data, Brush brush)
        {
            //Draw neck and center body line
            Polyline line1 = getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.Spine, JointID.ShoulderCenter, JointID.Head);
            skeletonToForm.Children.Add(line1);

            //Draw Left arm line
            Polyline line2 = getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderLeft, JointID.ElbowLeft, JointID.WristLeft, JointID.HandLeft);
            skeletonToForm.Children.Add(line2);

            //Draw right arm line
            Polyline line3 = getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderRight, JointID.ElbowRight, JointID.WristRight, JointID.HandRight);
            skeletonToForm.Children.Add(line3);

            //Draw left leg lines
            Polyline line4 = getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipLeft, JointID.KneeLeft, JointID.AnkleLeft, JointID.FootLeft);
            skeletonToForm.Children.Add(line4);

            //Draw right leg lines
            Polyline line5 = getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipRight, JointID.KneeRight, JointID.AnkleRight, JointID.FootRight);
            skeletonToForm.Children.Add(line5);

        }

        private Polyline getBodySegment(Microsoft.Research.Kinect.Nui.JointsCollection joints, Brush brush, params JointID[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i)
            {
                points.Add(getDisplayPosition(joints[ids[i]]));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        private Point getDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            Point newPoint = new Point(); ;
            nuiRuntime.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);
            depthX = Math.Max(0, Math.Min(depthX * 320, 320));  //convert to 320, 240 space
            depthY = Math.Max(0, Math.Min(depthY * 240, 240));  //convert to 320, 240 space
            int colorX, colorY;
            ImageViewArea iv = new ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point
            nuiRuntime.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);

            newPoint = new Point((int)(skeletonToForm.Width * colorX / 640.0), (int)(skeletonToForm.Height * colorY / 480));            

            return newPoint;
        }
    }
}
