using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Inherits from gesture base and provides the functionality for move front and back gesture
    /// </summary>
    class MoveFrontBackGestures : GestureBase
    {
        List<NuiElement> upFrameBuffer = null;
        List<NuiElement> downFrameBuffer = null;
        int frameBufferSize = 60;
        int thresholdGestureTime = 1200;
        float thresholdDistance = 0.35f;
        bool isMoveGestureDetected = false;

        public MoveFrontBackGestures()
            : base("MoveFrontBackGesturesThread", "MoveFrontBackGestures.txt", "Gestures.MoveFrontBackGestures")
        {
            upFrameBuffer = new List<NuiElement>(frameBufferSize);
            downFrameBuffer = new List<NuiElement>(frameBufferSize);
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
                        logger.Debug("Cancel Front Back move gesture detected");
                        RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_UP, (int)KeyboardPersistance.RELEASE);

                        RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_DOWN, (int)KeyboardPersistance.RELEASE);

                        detectResult = (int)ResultCodes.Success;
                        break;
                    }
                
                }
                
                if ((int)ResultCodes.GestureDetected == DetectUpMoveGesture(newGestureData))
                {
                    //Global detection true
                    isMoveGestureDetected = true;

                    //event to press key and hold
                    RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_DOWN, (int)KeyboardPersistance.RELEASE);

                    RaiseGestureEvent((int)GestureID.RIGHT_LEG_MOVED, (int)KeyboardAction.MOVE_UP, (int)KeyboardPersistance.PRESS);

                    detectResult = (int)ResultCodes.Success;
                    break;
                }

                if ((int)ResultCodes.GestureDetected == DetectDownMoveGesture(newGestureData))
                {
                    //Global detection true
                    isMoveGestureDetected = true;

                    //event to press key and hold
                    RaiseGestureEvent((int)GestureID.LEFT_LEG_MOVED, (int)KeyboardAction.MOVE_UP, (int)KeyboardPersistance.RELEASE);

                    RaiseGestureEvent((int)GestureID.LEFT_LEG_MOVED, (int)KeyboardAction.MOVE_DOWN, (int)KeyboardPersistance.PRESS);

                    detectResult = (int)ResultCodes.Success;
                    break;
                }


            } while (false);

            return detectResult;
        }

        private int DetectUpMoveGesture(NuiElement newGestureData)
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
                upFrameBuffer.Add(newGestureData);

                logger.Debug("UPMove: Frame added to queue and queue count = " + upFrameBuffer.Count);

                if (upFrameBuffer.Count <= 1)//buffer size should be more than 1 element for comparison purpose
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                oldestElement = upFrameBuffer[0];
                previousElement = upFrameBuffer[upFrameBuffer.Count - 2];
                latestElement = upFrameBuffer[upFrameBuffer.Count - 1];

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
                
                
                //Right leg move forward
                if (latestSkeleton.Joints[JointID.AnkleRight].Position.Z >= previouSkeleton.Joints[JointID.AnkleRight].Position.Z ||
                    latestSkeleton.Joints[JointID.AnkleLeft].Position.Z >= previouSkeleton.Joints[JointID.AnkleLeft].Position.Z)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    upFrameBuffer.Clear();
                    logger.Debug("UPMove: Failed condition!!!!!!!!!!!!!!!!!");
                    break;
                }

                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                    (oldestSkeleton.Joints[JointID.AnkleRight].Position.Z - latestSkeleton.Joints[JointID.AnkleRight].Position.Z) >= thresholdDistance)
                {
                    logger.Debug("UPMove:Right leg Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    conditionResult = (int)ResultCodes.GestureDetected;
                    upFrameBuffer.Clear();
                    break;
                }

                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                    (oldestSkeleton.Joints[JointID.AnkleLeft].Position.Z - latestSkeleton.Joints[JointID.AnkleLeft].Position.Z) >= thresholdDistance)
                {
                    logger.Debug("UPMove:Left leg Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    conditionResult = (int)ResultCodes.GestureDetected;
                    upFrameBuffer.Clear();
                    break;
                }

            } while (false);

            return conditionResult;
        }

        private int DetectDownMoveGesture(NuiElement newGestureData)
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
                downFrameBuffer.Add(newGestureData);

                logger.Debug("DownMove: Frame added to queue and queue count = " + downFrameBuffer.Count);

                if (downFrameBuffer.Count <= 1)//buffer size should be more than 1 element for comparison purpose
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                oldestElement = downFrameBuffer[0];
                previousElement = downFrameBuffer[downFrameBuffer.Count - 2];
                latestElement = downFrameBuffer[downFrameBuffer.Count - 1];

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
                if (latestSkeleton.Joints[JointID.AnkleLeft].Position.Z <= previouSkeleton.Joints[JointID.AnkleLeft].Position.Z ||
                    latestSkeleton.Joints[JointID.AnkleRight].Position.Z <= previouSkeleton.Joints[JointID.AnkleRight].Position.Z)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    downFrameBuffer.Clear();
                    break;
                }

                //Good to check if gesture passes
                //Left Leg move down
                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                    (oldestSkeleton.Joints[JointID.AnkleLeft].Position.Z - latestSkeleton.Joints[JointID.AnkleLeft].Position.Z) <= -thresholdDistance)
                {
                    logger.Debug("DownMove: Left leg Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    conditionResult = (int)ResultCodes.GestureDetected;
                    downFrameBuffer.Clear();
                    break;
                }

                //Right leg move down
                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                   (oldestSkeleton.Joints[JointID.AnkleRight].Position.Z - latestSkeleton.Joints[JointID.AnkleRight].Position.Z) <= -thresholdDistance)
                {
                    logger.Debug("DownMove: Right leg Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    conditionResult = (int)ResultCodes.GestureDetected;
                    downFrameBuffer.Clear();
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

                threshold = Math.Abs(latestSkeleton.Joints[JointID.FootLeft].Position.Z - latestSkeleton.Joints[JointID.FootRight].Position.Z);

                if (threshold < 0.4)
                {
                    conditionResult = true;
                    logger.Debug("Cacel gesture activated: Legs are close together!!");
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
            logger.Debug("Raising up down Event for the keyboard");
            KeyboardEventData data = new KeyboardEventData();

            data.SetGestureID( gestureId);
            data.SetKeyboardAction(keyAction);
            data.SetKeyboardPersistance(persistance);

            this.RaiseKeyboardEvent(data);
        }
    }
}
