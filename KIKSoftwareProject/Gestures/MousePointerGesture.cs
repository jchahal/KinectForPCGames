using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;
using System.IO;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Provides the functionality for the Mouse pointer gesture and instructs the Keyboard&Mouse thread to perform respective actions
    /// </summary>
    class MousePointerGesture : GestureBase
    {
        int frameNumber;
        float prevDepthX;
        float prevDepthY;
        Runtime nuiRuntime = null;
        
        public MousePointerGesture(Runtime nui): base("MousePointerThread", "MousePointer_Gesture.txt", "Gestures.MousePointerGestures")
        {
            frameNumber = 0;
            prevDepthX = 0;
            prevDepthY = 0;
            nuiRuntime = nui;
        }


        /// <summary>
        /// Main thread fuction where worker thread waits for any new data to arrive
        /// </summary>
        protected override void GestureAnalyzer()
        {
            do
            {
                this.handleRequests.WaitOne();

                int queueCount = GetQueueCount();

                if (queueCount > 0)
                {
                    NuiElement newGestureData = RemoveFromQueue();
                    ProcessNewGestureData(newGestureData);
                }


            } while (!this.threadExit);
        }
        
        private int ProcessNewGestureData(NuiElement newGestureData)
        {
            
            SkeletonData latestSkeleton = (from skeletons in newGestureData.GetSkeletonFrame().Skeletons
                                           where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                           select skeletons).FirstOrDefault();
            if (latestSkeleton == null)
            {
                return (int)ResultCodes.OutOfMemory;
            }

            bool isInside = IsInsideBox(latestSkeleton);

            //IsFistRolled(latestSkeleton);

            if (isInside)
            {
                //this.logger.Debug("Hand inside the box is true");
                float depthX, depthY, tmpX, tmpY, scaledDepthX, scaledDepthY;

              
                Joint leftHand = latestSkeleton.Joints[JointID.WristLeft];
                Joint leftHip = latestSkeleton.Joints[JointID.HipLeft];
                Joint leftShoulder = latestSkeleton.Joints[JointID.ShoulderLeft];
                Joint rightHip = latestSkeleton.Joints[JointID.HipRight];

                depthX = leftHand.Position.X;
                depthY = leftHand.Position.Y;
                
                if (frameNumber <= 1)
                {
                    prevDepthX = depthX;
                    prevDepthY = depthY;
                    frameNumber++;
                }
                else
                {
                    //applying relativety
                    tmpX = depthX;
                    tmpY = depthY;
                    depthX = depthX - prevDepthX;
                    depthY = depthY - prevDepthY;
                    prevDepthX = tmpX;
                    prevDepthY = tmpY;

                    //applying scaling
                    scaledDepthX = depthX * 800;
                    scaledDepthY = -1 * depthY * 600;

                    this.logger.Debug("FrameNumber = " + frameNumber);
                    this.logger.Debug("Moving relativly X by = " + scaledDepthX);
                    this.logger.Debug("Moving relativly Y by = " + scaledDepthY);
                    
                    MouseEventData mouseData = new MouseEventData();
                    mouseData.MouseAction = (int)MouseButton.MOUSE_MOVE;
                    mouseData.XCoordinate = (int)scaledDepthX;
                    mouseData.YCoordinate = (int)scaledDepthY;

                    this.RaiseMouseEvent(mouseData);
                }
            }
            else
            {
                frameNumber = 0;
            }


            return 0;
        }

        private bool IsFistRolled(SkeletonData trackedSkeleton)
        {
            bool isRolled = false;

            this.logger.Debug("*****************************************************************");
            this.logger.Debug(trackedSkeleton.Joints[JointID.HandLeft].Position.X + "," + trackedSkeleton.Joints[JointID.HandLeft].Position.Y + "," + trackedSkeleton.Joints[JointID.HandLeft].Position.Z);
            this.logger.Debug(trackedSkeleton.Joints[JointID.WristLeft].Position.X + "," + trackedSkeleton.Joints[JointID.WristLeft].Position.Y + "," + trackedSkeleton.Joints[JointID.WristLeft].Position.Z);
            this.logger.Debug("*****************************************************************");

            string wristLeftData = (trackedSkeleton.Joints[JointID.WristLeft].Position.X + "," + trackedSkeleton.Joints[JointID.WristLeft].Position.Y + "," + trackedSkeleton.Joints[JointID.WristLeft].Position.Z);
            string handLeftData = (trackedSkeleton.Joints[JointID.HandLeft].Position.X + "," + trackedSkeleton.Joints[JointID.HandLeft].Position.Y + "," + trackedSkeleton.Joints[JointID.HandLeft].Position.Z);

            
            return isRolled;
        }

        private bool IsInsideBox(SkeletonData trackedSkeleton)
        {
            bool isInside = false;

            do
            {
                if (trackedSkeleton == null)
                {
                    isInside = false;
                    break;
                }

                Joint leftHand = trackedSkeleton.Joints[JointID.WristLeft];
                Joint hipLeft = trackedSkeleton.Joints[JointID.HipLeft];
                Joint hipRight = trackedSkeleton.Joints[JointID.HipRight];

                Joint rightShoulder = trackedSkeleton.Joints[JointID.ShoulderRight];
                Joint leftShoulder = trackedSkeleton.Joints[JointID.ShoulderLeft];

                float boundDistance = (3.0f/4.0f)*(trackedSkeleton.Joints[JointID.Head].Position.Y - trackedSkeleton.Joints[JointID.Spine].Position.Y);
                float currentHandChestDifference = trackedSkeleton.Joints[JointID.Spine].Position.Z - trackedSkeleton.Joints[JointID.HandLeft].Position.Z;
                
                if( currentHandChestDifference < boundDistance )
                {
                    isInside = false;
                    break;
                }
                
                if (leftHand.Position.Y < hipLeft.Position.Y || leftHand.Position.Y < hipRight.Position.Y)
                {
                    isInside = false;
                    break;
                }              

                isInside = true;

            } while (false);

            return isInside;
        }
    }
}
