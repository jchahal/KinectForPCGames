using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Inherits from gesture base and provides the functionality for move left and right gesture
    /// </summary>
    class MoveLeftRightGestures : GestureBase
    {
        List<NuiElement> rightFrameBuffer = null;
        List<NuiElement> leftFrameBuffer = null;
        int frameBufferSize = -1;
        int thresholdGestureTime = -1;
        float thresholdDistance = 0.0f;
        bool isMoveGestureDetected = false;
        GestureThresholdValues.MoveLeftRightThresholds thresholds = null;

        public MoveLeftRightGestures()
            : base("MoveLeftRightGesturesThread", "MoveLeftRightGestures.txt", "Gestures.MoveLeftRightGestures")
        {
            thresholds = new GestureThresholdValues.MoveLeftRightThresholds();
            
            this.thresholdGestureTime = thresholds.ThresholdGestureTime;
            this.thresholdDistance = thresholds.ThresholdDistance;
            this.frameBufferSize = thresholds.ThresholdFrameBufferSize;

            rightFrameBuffer = new List<NuiElement>(frameBufferSize);
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

                if (!this.isMoveGestureDetected)
                {
                   
                }
                else
                {
                    if (CancelLeftRightMoveGesture(newGestureData))
                    {
                        //Global detection true
                        isMoveGestureDetected = false;
                        logger.Debug("Cancel Left right move gesture detected");
                        RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_LEFT, (int)KeyboardPersistance.RELEASE);

                        RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_RIGHT, (int)KeyboardPersistance.RELEASE);

                        detectResult = (int)ResultCodes.Success;
                        break;
                    }
                
                }
                
                if ((int)ResultCodes.GestureDetected == DetectRightMoveGesture(newGestureData))
                {
                    //Global detection true
                    isMoveGestureDetected = true;

                    //event to press key and hold
                    RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_LEFT, (int)KeyboardPersistance.RELEASE);

                    RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_RIGHT, (int)KeyboardPersistance.PRESS);

                    detectResult = (int)ResultCodes.Success;
                    break;
                }

                if ((int)ResultCodes.GestureDetected == DetectLeftMoveGesture(newGestureData))
                {
                    //Global detection true
                    isMoveGestureDetected = true;

                    //event to press key and hold
                    RaiseGestureEvent((int)GestureID.LEFT_LEG_MOVED, (int)KeyboardAction.MOVE_RIGHT, (int)KeyboardPersistance.RELEASE);

                    RaiseGestureEvent((int)GestureID.LEFT_LEG_MOVED, (int)KeyboardAction.MOVE_LEFT, (int)KeyboardPersistance.PRESS);

                    detectResult = (int)ResultCodes.Success;
                    break;
                }


            } while (false);

            return detectResult;
        }

        private int DetectRightMoveGesture(NuiElement newGestureData)
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
                rightFrameBuffer.Add(newGestureData);

                logger.Debug("RightMove: Frame added to queue and queue count = " + rightFrameBuffer.Count);

                if (rightFrameBuffer.Count <= 1)//buffer size should be more than 1 element for comparison purpose
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                oldestElement = rightFrameBuffer[0];
                previousElement = rightFrameBuffer[rightFrameBuffer.Count - 2];
                latestElement = rightFrameBuffer[rightFrameBuffer.Count - 1];

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

                previouSkeleton = ( from skeletons in previousElement.GetSkeletonFrame().Skeletons
                                    where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                    select skeletons).FirstOrDefault();


                oldestSkeleton = ( from skeletons in oldestElement.GetSkeletonFrame().Skeletons
                                   where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                   select skeletons).FirstOrDefault();

                if (latestSkeleton == null || previouSkeleton == null || oldestSkeleton == null)
                {
                    conditionResult = (int)ResultCodes.NullTrackedSkeleton;
                    break;
                }
                
                if (latestSkeleton.Joints[JointID.AnkleRight].Position.X <= previouSkeleton.Joints[JointID.AnkleRight].Position.X)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    rightFrameBuffer.Clear();
                    break;
                }

                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                    (latestSkeleton.Joints[JointID.AnkleRight].Position.X - oldestSkeleton.Joints[JointID.AnkleRight].Position.X) >= thresholdDistance)
                {
                    logger.Debug("RightMove: Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    conditionResult = (int)ResultCodes.GestureDetected;
                    rightFrameBuffer.Clear();
                    break;
                }

            } while (false);

            return conditionResult;
        }

        private int DetectLeftMoveGesture(NuiElement newGestureData)
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
                leftFrameBuffer.Add(newGestureData);

                logger.Debug("LeftMove: Frame added to queue and queue count = " + leftFrameBuffer.Count);

                if (leftFrameBuffer.Count <= 1)//buffer size should be more than 1 element for comparison purpose
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                oldestElement = leftFrameBuffer[0];
                previousElement = leftFrameBuffer[leftFrameBuffer.Count - 2];
                latestElement = leftFrameBuffer[leftFrameBuffer.Count - 1];

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
                
                previouSkeleton = ( from skeletons in previousElement.GetSkeletonFrame().Skeletons
                                    where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                    select skeletons).FirstOrDefault();                

                oldestSkeleton = ( from skeletons in oldestElement.GetSkeletonFrame().Skeletons
                                   where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                   select skeletons).FirstOrDefault();

                if (latestSkeleton == null || previouSkeleton == null || oldestSkeleton == null)
                {
                    conditionResult = (int)ResultCodes.NullTrackedSkeleton;
                    break;
                }

                //Negative condition to break
                if (latestSkeleton.Joints[JointID.AnkleLeft].Position.X >= previouSkeleton.Joints[JointID.AnkleLeft].Position.X)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    leftFrameBuffer.Clear();
                    break;
                }

                //Good to check if gesture passes
                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                    (latestSkeleton.Joints[JointID.AnkleLeft].Position.X - oldestSkeleton.Joints[JointID.AnkleLeft].Position.X) <= -thresholdDistance)
                {
                    logger.Debug("LeftMove: Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    conditionResult = (int)ResultCodes.GestureDetected;
                    leftFrameBuffer.Clear();
                    break;
                }

            } while (false);

            return conditionResult;
        }

        private bool CancelLeftRightMoveGesture(NuiElement newGestureData)
        {
            bool conditionResult = false;
            NuiElement latestElement = null;
            SkeletonData latestSkeleton = null;
            float threshold;

            do
            {
                latestElement = newGestureData;


                latestSkeleton = (from skeletons in latestElement.GetSkeletonFrame().Skeletons
                                  where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                   select skeletons).FirstOrDefault();



                threshold = Math.Abs(latestSkeleton.Joints[JointID.FootLeft].Position.X - latestSkeleton.Joints[JointID.FootRight].Position.X);

                if (threshold < 0.4)
                {
                    conditionResult = true;
                    logger.Debug("Legs are close together");
                }
                else
                {
                    conditionResult = false;
                }
            } while (false);

            return conditionResult;
        }

        private void RaiseGestureEvent(int gestureId, int keyAction, int persistance)
        {
            logger.Debug("Raising Event for the keyboard");
            KeyboardEventData data = new KeyboardEventData();

            data.SetGestureID( gestureId);
            data.SetKeyboardAction(keyAction);
            data.SetKeyboardPersistance(persistance);

            this.RaiseKeyboardEvent(data);
        }
    }
}
