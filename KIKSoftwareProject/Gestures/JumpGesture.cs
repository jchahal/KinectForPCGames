using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Inherits from Gesture base and provides the implmentation of the Jump gesture
    /// </summary>
    class JumpGesture: GestureBase
    {
        List<NuiElement> jumpFrameBuffer = null;
        List<NuiElement> leftFrameBuffer = null;
        int frameBufferSize = -1;
        int thresholdGestureTime = -1;
        float thresholdDistance = 0.0f;        
        GestureThresholdValues.JumpGesture thresholds = null;
        
        
        
        
        public JumpGesture()
            : base("JumpGesture", "JumpGestureData.txt", "JumpGesture")
        {
            thresholds = new GestureThresholdValues.JumpGesture();

            this.thresholdGestureTime = thresholds.ThresholdGestureTime;
            this.thresholdDistance = thresholds.ThresholdDistance;
            this.frameBufferSize = thresholds.ThresholdFrameBufferSize;

            jumpFrameBuffer = new List<NuiElement>(frameBufferSize);
            leftFrameBuffer = new List<NuiElement>(frameBufferSize);

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
            //if gesture detected raise event to send to keyboard
            int detectResult = -1;

            do
            {
                if ((int)ResultCodes.GestureDetected == DetectJumpGesture(newGestureData))
                {
                    

                    //event to press key and hold

                    RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.JUMP, (int)KeyboardPersistance.PRESS_AND_RELEASE);

                    detectResult = (int)ResultCodes.Success;
                    break;
                }

            } while (false);

            return detectResult;
        }

        private int DetectJumpGesture(NuiElement newGestureData)
        {
            int conditionResult = (int)ResultCodes.GestureDetectionFailed;
            NuiElement latestElement = null;
            NuiElement oldestElement = null;
            NuiElement previousElement = null;

            SkeletonData latestSkeleton = null;
            SkeletonData oldestSkeleton = null;
            SkeletonData previouSkeleton = null;

            do
            {
                jumpFrameBuffer.Add(newGestureData);

                logger.Debug("JumpGesture: Frame added to queue and queue count = " + jumpFrameBuffer.Count);

                if (jumpFrameBuffer.Count <= 1)//buffer size should be more than 1 element for comparison purpose
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                oldestElement = jumpFrameBuffer[0];
                previousElement = jumpFrameBuffer[jumpFrameBuffer.Count - 2];
                latestElement = jumpFrameBuffer[jumpFrameBuffer.Count - 1];

                if (latestElement.GetSkeletonFrame() == null)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                if (latestElement.GetSkeletonFrame().Skeletons == null)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                latestSkeleton = (from skeletons in latestElement.GetSkeletonFrame().Skeletons
                                  where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                  select skeletons).FirstOrDefault();

                previouSkeleton = (from skeletons in previousElement.GetSkeletonFrame().Skeletons
                                   where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                   select skeletons).FirstOrDefault();


                oldestSkeleton = (from skeletons in oldestElement.GetSkeletonFrame().Skeletons
                                  where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                  select skeletons).FirstOrDefault();

                if (latestSkeleton == null || previouSkeleton == null || oldestSkeleton == null)
                {
                    conditionResult = (int)ResultCodes.NullTrackedSkeleton;
                    break;
                }
                
                if (latestSkeleton.Joints[JointID.HipCenter].Position.Y <= previouSkeleton.Joints[JointID.HipCenter].Position.Y)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    jumpFrameBuffer.Clear();
                    break;
                }

                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                    (latestSkeleton.Joints[JointID.HipCenter].Position.Y - oldestSkeleton.Joints[JointID.HipCenter].Position.Y) >= thresholdDistance)
                {
                    
                    logger.Debug("Jump: Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    logger.Debug("latestSkeleton.Joints[JointID.HipCenter].Position.Y = " + latestSkeleton.Joints[JointID.HipCenter].Position.Y);
                    logger.Debug("oldestSkeleton.Joints[JointID.HipCenter].Position.Y = " + oldestSkeleton.Joints[JointID.HipCenter].Position.Y);
                    logger.Debug("Jump: latestSkeleton.Joints[JointID.HipCenter].Position.Y - oldestSkeleton.Joints[JointID.HipCenter].Position.Y) = " + (latestSkeleton.Joints[JointID.HipCenter].Position.Y - oldestSkeleton.Joints[JointID.HipCenter].Position.Y));

                    conditionResult = (int)ResultCodes.GestureDetected;
                    jumpFrameBuffer.Clear();
                    break;
                }

            } while (false);

            return conditionResult;
        }

        private void RaiseGestureEvent(int gestureId, int keyAction, int persistance)
        {
            logger.Debug("Raising Event for the keyboard");
            KeyboardEventData data = new KeyboardEventData();

            data.SetGestureID(gestureId);
            data.SetKeyboardAction(keyAction);
            data.SetKeyboardPersistance(persistance);

            this.RaiseKeyboardEvent(data);
        }
    }
}
