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

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Class povides methods to draw on the GUI the transformed skeleton points passed by other threads
    /// </summary>
    class TransformedSkeletonDrawing : Window
    {
        private static readonly Object _threadLock = new Object();
        private Thread skeletalDrawingThread = null;
        private Semaphore handleRequests = null;
        private Queue<SkeletalDrawingData> dataQueue = null;
        private volatile bool threadExit;
        private Semaphore requestSkeletalData = null;
        private Runtime nuiRuntime = null;
        private Canvas transformedSkeletonToForm = null;
        private SkeletonTransformation tranformSkeleton = null;
        private SkeletonTransformation.TransformedJoints tranformedJoints = new SkeletonTransformation.TransformedJoints();

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

        public TransformedSkeletonDrawing(Runtime nui, Canvas transformedSkeleton)
        {
            transformedSkeletonToForm = transformedSkeleton;
            nuiRuntime = nui;

            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestSkeletalData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);

            tranformSkeleton = new SkeletonTransformation();

            dataQueue = new Queue<SkeletalDrawingData>();
            threadExit = false;
            skeletalDrawingThread = new Thread(DrawSkeletonModule);
            skeletalDrawingThread.Name = "SkeletalDrawingThread";
            skeletalDrawingThread.SetApartmentState(ApartmentState.STA);
            skeletalDrawingThread.Start();

        }

        public void ExitThread()
        {
            threadExit = true;
            handleRequests.Release();
        }

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

        private int GetQueueCount()
        {
            int count = 0;

            lock (_threadLock)
            {
                count = dataQueue.Count;
            }
            return count;
        }

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

        private int ProcessNewSkeletalData(SkeletalDrawingData skeletalData)
        {

            if (skeletalData == null)
            {
                return (int)ResultCodes.OutOfMemory;
            }
            
            transformedSkeletonToForm.Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.FromSeconds(1), new Action(
                () =>
                {
                    try
                    {
                        SkeletonFrame skeletonFrame = skeletalData.GetSkeletonFrame();

                        this.tranformedJoints.jointsDictionary = this.tranformSkeleton.TransformSkeleton(skeletonFrame);

                        Brush[] brushes = new Brush[6];
                        int iSkeleton = 0;

                        brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                        brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                        brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
                        brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
                        brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
                        brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

                        transformedSkeletonToForm.Children.Clear();

                        Brush brush = brushes[iSkeleton % brushes.Length];

                        //Add different line segments
                        AddLineSegments(this.tranformedJoints.jointsDictionary, brush);

                        // Draw joints

                        foreach (KeyValuePair<JointID, Microsoft.Research.Kinect.Nui.Vector> keyValuePair in this.tranformedJoints.jointsDictionary)
                        {
                            Joint newJoint = new Joint();

                            newJoint.ID = keyValuePair.Key;
                            newJoint.Position = keyValuePair.Value;
                            newJoint.TrackingState = JointTrackingState.Tracked;

                            Point jointPos = getDisplayPosition(newJoint);
                            Line jointLine = new Line();
                            jointLine.X1 = jointPos.X - 3;
                            jointLine.X2 = jointLine.X1 + 6;
                            jointLine.Y1 = jointLine.Y2 = jointPos.Y;
                            jointLine.Stroke = jointColors[newJoint.ID];
                            jointLine.StrokeThickness = 6;

                            transformedSkeletonToForm.Children.Add(jointLine);
                        }
                    }
                    catch (Exception ex)
                    {
                        string msg = ex.Message;
                    }
                }));

            return (int)ResultCodes.Success;
        }

        private void AddLineSegments( Dictionary<JointID, Microsoft.Research.Kinect.Nui.Vector> jointsDictionary, Brush brush)
        {
            //Draw neck and center body line
            Polyline line1 = getBodySegment(jointsDictionary, brush, JointID.HipCenter, JointID.Spine, JointID.ShoulderCenter, JointID.Head);
            transformedSkeletonToForm.Children.Add(line1);

            //Draw Left arm line
            Polyline line2 = getBodySegment(jointsDictionary, brush, JointID.ShoulderCenter, JointID.ShoulderLeft, JointID.ElbowLeft, JointID.WristLeft, JointID.HandLeft);
            transformedSkeletonToForm.Children.Add(line2);

            //Draw right arm line
            Polyline line3 = getBodySegment(jointsDictionary, brush, JointID.ShoulderCenter, JointID.ShoulderRight, JointID.ElbowRight, JointID.WristRight, JointID.HandRight);
            transformedSkeletonToForm.Children.Add(line3);

            //Draw left leg lines
            Polyline line4 = getBodySegment(jointsDictionary, brush, JointID.HipCenter, JointID.HipLeft, JointID.KneeLeft, JointID.AnkleLeft, JointID.FootLeft);
            transformedSkeletonToForm.Children.Add(line4);

            //Draw right leg lines
            Polyline line5 = getBodySegment(jointsDictionary, brush, JointID.HipCenter, JointID.HipRight, JointID.KneeRight, JointID.AnkleRight, JointID.FootRight);
            transformedSkeletonToForm.Children.Add(line5);

        }
        
        private Polyline getBodySegment(Dictionary<JointID, Microsoft.Research.Kinect.Nui.Vector> jointsDictionary, Brush brush, params JointID[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);


            for (int i = 0; i < ids.Length; ++i)
            {
                Joint newJoint = new Joint();

                foreach (KeyValuePair<JointID, Microsoft.Research.Kinect.Nui.Vector> keyValuePair in jointsDictionary)
                {
                    if (keyValuePair.Value.Equals(jointsDictionary[ids[i]]))
                    {
                        newJoint.ID = keyValuePair.Key;
                    }

                }

                newJoint.Position = jointsDictionary[ids[i]];
                newJoint.TrackingState = JointTrackingState.Tracked;

                points.Add(getDisplayPosition(newJoint));
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

            newPoint = new Point((int)(transformedSkeletonToForm.Width * colorX / 640.0), (int)(transformedSkeletonToForm.Height * colorY / 480));

            return newPoint;
        }
    }    
}
