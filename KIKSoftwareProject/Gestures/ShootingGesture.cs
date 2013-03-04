using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Inherits from gesture base and provides the functionality for shooting gesture
    /// </summary>
    class ShootingGesture: GestureBase
    {
        bool aimDetected = false;
        bool shootDetected = false;
        List<NuiElement> shootFrameBuffer = null;
        List<NuiElement> cancelShootFrameBuffer = null;
        int frameBufferSize = 60;
        int thresholdGestureTime = 1200;
        int minthresholdGestureTime = 200;
        SkeletonData trueCondtionSkeleton = null;
        GestureThresholdValues.ShootingGestures thresholds = new GestureThresholdValues.ShootingGestures();


        public ShootingGesture()
            : base("ShootingGesture", "ShootingGesture.txt", "Gesture.ShootingGesture")
        {
            shootFrameBuffer = new List<NuiElement>(frameBufferSize);
            cancelShootFrameBuffer = new List<NuiElement>(frameBufferSize);
        }
        
        /// <summary>
        /// Main thread fuction where worker thread waits for any new data to arrive
        /// </summary>
        protected override void GestureAnalyzer()
        {
            this.logger.Debug("Shooting thread started!!!!!!!!!!!");

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

        /// <summary>
        /// This function is called when new NuiElement data is available to be processed
        /// </summary>
        /// <param name="newGestureData">the NuiElement with skeleton values</param>
        /// <returns>result of the processing</returns>
        private int ProcessNewGestureData(NuiElement newGestureData)
        {
            int gestureResult = (int)ResultCodes.Success;
                        
            SkeletonData latestSkeleton = (from skeletons in newGestureData.GetSkeletonFrame().Skeletons
                                           where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                           select skeletons).FirstOrDefault();
            if (!this.thresholds.IsThresholdSet)
            {
                this.thresholds.SetThresholdValues(latestSkeleton);
            }

            gestureResult = CheckAimConditions(latestSkeleton);

            gestureResult = CheckShootConditions(latestSkeleton, newGestureData);

           

            return gestureResult;
        }

        //Check various conditions to detect shoot gesture
        private int CheckShootConditions(SkeletonData latestSkeleton, NuiElement newGestureData)
        {
            int gestureResult = (int)ResultCodes.Success;

            do
            {
                bool shoot = false;
                bool isInsideBox = false;

                if (!this.shootDetected)
                {
                    shoot = IsShootTrue(newGestureData);
                }

                // This condition becomes true when shoot is triggerd but it was not detected previously
                // If the user is already in shoot mode and still pulls the hand back this condition will not pass 
                // Unless or until he moves his hand out of the box (set shootDetected to false) and
                // does shoot gesture again this condition will not pass. 
                if (shoot && !this.shootDetected) 
                {
                    MouseEventData mouseData = new MouseEventData();
                    mouseData.MouseAction = (int)MouseButton.LEFT_MOUSE_BUTTON;
                    mouseData.XCoordinate = -1;
                    mouseData.YCoordinate = -1;
                    mouseData.KeyPersistance = (int)MousePresistance.DOUBLE_CLICK_HOLD;

                    this.RaiseMouseEvent(mouseData);
                    this.shootDetected = true;
                    logger.Debug(" Shoot detected first time");
                    break;
                }

                isInsideBox = IsInSideShootBox(newGestureData);

                if (this.shootDetected)
                { 
                    logger.Debug("ttt shootinggggg "); 
                }
                
                //Orig end
                //New start
                if (this.shootDetected)
                {

                    if (IsShootcancel(newGestureData))
                    {
                        logger.Debug("ttt Shoot Cancel gesture detected!!");
                        MouseEventData mouseData = new MouseEventData();
                        mouseData.MouseAction = (int)MouseButton.LEFT_MOUSE_BUTTON;
                        mouseData.XCoordinate = -1;
                        mouseData.YCoordinate = -1;
                        mouseData.KeyPersistance = (int)MousePresistance.RELEASE;

                        this.RaiseMouseEvent(mouseData);
                        this.shootDetected = false;
                        this.trueCondtionSkeleton = null;
                        logger.Debug(" Shoot detected while outside the box Negative ");
                        break;
                    }
                  
                }
                //New end
            } while (false);

            return gestureResult;

        }

        private int CheckAimConditions(SkeletonData latestSkeleton)
        {
            int gestureResult = (int)ResultCodes.Success;
             do
            {

                bool aim = false;
                bool isInsideAim = false;

                if (latestSkeleton == null)
                {
                    gestureResult = (int)ResultCodes.OutOfMemory;
                    break;
                }

                if (!this.aimDetected)
                {
                    aim = IsAimTrue(latestSkeleton);
                }


                if (aim && !this.aimDetected)
                {
                    MouseEventData mouseData = new MouseEventData();
                    mouseData.MouseAction = (int)MouseButton.RIGHT_MOUSE_BUTTON;
                    mouseData.XCoordinate = -1;
                    mouseData.YCoordinate = -1;
                    mouseData.KeyPersistance = (int)MousePresistance.PRESS_AND_RELEASE;

                    this.RaiseMouseEvent(mouseData);
                    this.aimDetected = true;
                    
                    break;
                }

                isInsideAim = JackInManojsBox(latestSkeleton);

                if (!isInsideAim && this.aimDetected)
                {
                    MouseEventData mouseData = new MouseEventData();
                    mouseData.MouseAction = (int)MouseButton.RIGHT_MOUSE_BUTTON;
                    mouseData.XCoordinate = -1;
                    mouseData.YCoordinate = -1;
                    mouseData.KeyPersistance = (int)MousePresistance.PRESS_AND_RELEASE;

                    this.RaiseMouseEvent(mouseData);
                    this.aimDetected = false;

                    break;
                }

            } while (false);

            return gestureResult;
        }

        bool JackInManojsBox(SkeletonData trackedSkeleton)
        {
            bool aimResult = false;

            Joint rightHand = trackedSkeleton.Joints[JointID.HandRight];
            Joint rightShoulder = trackedSkeleton.Joints[JointID.ShoulderRight];

            do
            {
                float rightShoulderY = rightShoulder.Position.Y;
                float rightShoulderZ = rightShoulder.Position.Z;

                float rightHandY = rightHand.Position.Y;
                float rightHandZ = rightHand.Position.Z;

                float thresholdY = Math.Abs(rightShoulder.Position.X - trackedSkeleton.Joints[JointID.ShoulderLeft].Position.X);
                thresholdY = thresholdY * 0.7f;

                if (rightHandY < (rightShoulderY - thresholdY) ) 
                {
                    aimResult = false;
                    break;
                }

                aimResult = true;

            } while (false);

            return aimResult;
        }

        bool IsAimTrue(SkeletonData trackedSkeleton)
        {
            bool aimResult = false;

            Joint rightHand = trackedSkeleton.Joints[JointID.HandRight];
            Joint rightShoulder = trackedSkeleton.Joints[JointID.ShoulderRight];

           do
            {
                float rightShoulderY = rightShoulder.Position.Y;
                float rightShoulderZ = rightShoulder.Position.Z;

                float rightHandY = rightHand.Position.Y;
                float rightHandZ = rightHand.Position.Z;

                float thresholdY = this.thresholds.AimThresholdY;
                thresholdY = thresholdY * 0.5f;

                if (rightHandY < (rightShoulderY - thresholdY) || rightHandY > (rightShoulderY + thresholdY))
                {
                    aimResult = false;
                    break;
                }

                aimResult = true;

            } while (false);

            return aimResult;
        }

       private bool IsShootTrue(NuiElement newGestureData)
        {
            bool shootResult = false; 
           
           int conditionResult = (int)ResultCodes.GestureDetectionFailed;
            NuiElement latestElement = null;
            NuiElement oldestElement = null;
            NuiElement previousElement = null;

            SkeletonData latestSkeleton = null;
            SkeletonData oldestSkeleton = null;
            SkeletonData previouSkeleton = null;

            do
            {
                shootFrameBuffer.Add(newGestureData);

                logger.Debug("ShootContinousFire: Frame added to queue and queue count = " + shootFrameBuffer.Count);

                if (shootFrameBuffer.Count <= 1)//buffer size should be more than 1 element for comparison purpose
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    break;
                }

                oldestElement = shootFrameBuffer[0];
                previousElement = shootFrameBuffer[shootFrameBuffer.Count - 2];
                latestElement = shootFrameBuffer[shootFrameBuffer.Count - 1];

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

                float thresholdDistance = this.thresholds.ShootThresholdDistance;
                float thresholdXY = this.thresholds.ShootThresholdXY;

                //Negative condition to break. If the user hand is moving forward, detection failed. Clear buffer. 
                if (latestSkeleton.Joints[JointID.HandRight].Position.Z < previouSkeleton.Joints[JointID.HandRight].Position.Z)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    this.logger.Debug("Latest skeleton right Z = " + latestSkeleton.Joints[JointID.HandRight].Position.Z);
                    this.logger.Debug("Previous skeleton right z = " + previouSkeleton.Joints[JointID.HandRight].Position.Z);
                    this.logger.Debug("Time stamp difference = " + (latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds);
                    this.logger.Debug("Latest Z - Oldest Z = " + (latestSkeleton.Joints[JointID.HandRight].Position.Z - oldestSkeleton.Joints[JointID.HandRight].Position.Z));
                    shootFrameBuffer.Clear();
                    break;
                }

                //Negative condition. Too much variation in Y. 
                if (latestSkeleton.Joints[JointID.HandRight].Position.Y > (oldestSkeleton.Joints[JointID.HandRight].Position.Y + thresholdXY) 
                    || latestSkeleton.Joints[JointID.HandRight].Position.Y < (oldestSkeleton.Joints[JointID.HandRight].Position.Y - thresholdXY))
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    this.logger.Debug("Too much variation in Y");
                    shootFrameBuffer.Clear();
                    break;
                }

                //Negative condition. Too much variation in X.
                if (latestSkeleton.Joints[JointID.HandRight].Position.X > (oldestSkeleton.Joints[JointID.HandRight].Position.X + thresholdXY) 
                    || latestSkeleton.Joints[JointID.HandRight].Position.X < (oldestSkeleton.Joints[JointID.HandRight].Position.X - thresholdXY))
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    this.logger.Debug("Too much variation in X");
                    shootFrameBuffer.Clear();
                    break;
                }

                //NEW CONDITION ADDED TO BLOCK THE HAND FALLING DOWN
                if (latestSkeleton.Joints[JointID.HandRight].Position.Y < latestSkeleton.Joints[JointID.HipCenter].Position.Y - thresholdXY)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    this.logger.Debug("hand is just falling down...below the hip + threashold");
                    shootFrameBuffer.Clear();
                    break;
                }

                //NEW CONDITION ADDED TO BLOCK THE HAND ABOVE THE FACE
                if (latestSkeleton.Joints[JointID.HandRight].Position.Y > latestSkeleton.Joints[JointID.Head].Position.Y + thresholdXY)
                {
                    conditionResult = (int)ResultCodes.GestureDetectionFailed;
                    this.logger.Debug("Too much variation in X");
                    shootFrameBuffer.Clear();
                    break;
                }

                
                // This condition will pass if the user has moved the hand back to threshold distance 
                // within a threshold time. 
                if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds > minthresholdGestureTime &&
                    (latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                    (latestSkeleton.Joints[JointID.HandRight].Position.Z - oldestSkeleton.Joints[JointID.HandRight].Position.Z) > thresholdDistance)
                {
                                      
                    logger.Debug("ShootContinousFire: Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                    this.logger.Debug( "Time diff: " + (latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds + " < " + thresholdGestureTime);
                    this.logger.Debug( "Distance diff: " + (latestSkeleton.Joints[JointID.HandRight].Position.Z - oldestSkeleton.Joints[JointID.HandRight].Position.Z) + " > " + thresholdDistance);

                    conditionResult = (int)ResultCodes.GestureDetected;
                    trueCondtionSkeleton = latestSkeleton;
                    shootFrameBuffer.Clear();
                    break;
                }

            } while (false);


            if (conditionResult == (int)ResultCodes.GestureDetected)
            {
                shootResult = true;
            }
            else
            {
                shootResult = false;
            }

            return shootResult;
        }

        // Cancel shoot condition detector
       private bool IsShootcancel(NuiElement newGestureData)
       {
           bool cancelShootResult = false;

           int conditionResult = (int)ResultCodes.GestureDetectionFailed;
           NuiElement latestElement = null;
           NuiElement oldestElement = null;
           NuiElement previousElement = null;

           SkeletonData latestSkeleton = null;
           SkeletonData oldestSkeleton = null;
           SkeletonData previouSkeleton = null;



           do
           {
               cancelShootFrameBuffer.Add(newGestureData);

               logger.Debug("ShootContinousFire: Frame added to queue and queue count = " + cancelShootFrameBuffer.Count);

               if (cancelShootFrameBuffer.Count <= 1)//buffer size should be more than 1 element for comparison purpose
               {
                   conditionResult = (int)ResultCodes.GestureDetectionFailed;
                   break;
               }

               oldestElement = cancelShootFrameBuffer[0];
               previousElement = cancelShootFrameBuffer[cancelShootFrameBuffer.Count - 2];
               latestElement = cancelShootFrameBuffer[cancelShootFrameBuffer.Count - 1];

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

               float thresholdDistance = this.thresholds.ShootThresholdDistance;
               float thresholdXY = this.thresholds.ShootThresholdXY;

               //Negative condition to break. If the user hand is moving backward, detection failed. Clear buffer. 
               if (latestSkeleton.Joints[JointID.HandRight].Position.Z > previouSkeleton.Joints[JointID.HandRight].Position.Z)
               {
                   conditionResult = (int)ResultCodes.GestureDetectionFailed;
                   cancelShootFrameBuffer.Clear();
                   break;
               }

               //Negative condition. Too much variation in Y. 
               if (latestSkeleton.Joints[JointID.HandRight].Position.Y > (oldestSkeleton.Joints[JointID.HandRight].Position.Y + thresholdXY)
                   || latestSkeleton.Joints[JointID.HandRight].Position.Y < (oldestSkeleton.Joints[JointID.HandRight].Position.Y - thresholdXY))
               {
                   conditionResult = (int)ResultCodes.GestureDetectionFailed;
                   this.logger.Debug("Too much variation in Y");
                   cancelShootFrameBuffer.Clear();
                   break;
               }

               //Negative condition. Too much variation in X.
               if (latestSkeleton.Joints[JointID.HandRight].Position.X > (oldestSkeleton.Joints[JointID.HandRight].Position.X + thresholdXY)
                   || latestSkeleton.Joints[JointID.HandRight].Position.X < (oldestSkeleton.Joints[JointID.HandRight].Position.X - thresholdXY))
               {
                   conditionResult = (int)ResultCodes.GestureDetectionFailed;
                   this.logger.Debug("Too much variation in X");
                   cancelShootFrameBuffer.Clear();
                   break;
               }

               // User has terminated shooting if he drops his hand
               if (latestSkeleton.Joints[JointID.HandRight].Position.Y < latestSkeleton.Joints[JointID.HipCenter].Position.Y - thresholdXY)
               {
                   conditionResult = (int)ResultCodes.GestureDetected;
                   this.logger.Debug("hand is just falling down...below the hip + threashold");
                   cancelShootFrameBuffer.Clear();
                   break;
               }

               // User has terminated shooting if he drops his hand is 2.5 times threshold distance away
               if (latestSkeleton.Joints[JointID.HandRight].Position.Z < latestSkeleton.Joints[JointID.HipCenter].Position.Z - 2.5*thresholdDistance)
               {
                   conditionResult = (int)ResultCodes.GestureDetected;
                   this.logger.Debug("ttt hand is far from body");
                   cancelShootFrameBuffer.Clear();
                   break;
               }

               // This condition will pass if the user has moved the hand back to threshold distance 
               // within a threshold time. 
               if ((latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds > 200 &&
                   (latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds < thresholdGestureTime &&
                   (oldestSkeleton.Joints[JointID.HandRight].Position.Z - latestSkeleton.Joints[JointID.HandRight].Position.Z) > thresholdDistance)
               {
                   logger.Debug("Cancel shoot: Passed distance and timing condition!!!!!!!!!!!!!!!!!");
                   this.logger.Debug("Time diff: " + (latestElement.GetTimeStamp() - oldestElement.GetTimeStamp()).TotalMilliseconds + " < " + thresholdGestureTime);
                   this.logger.Debug("Distance diff: " + (latestSkeleton.Joints[JointID.HandRight].Position.Z - oldestSkeleton.Joints[JointID.HandRight].Position.Z) + " > " + thresholdDistance);

                   conditionResult = (int)ResultCodes.GestureDetected;
                   trueCondtionSkeleton = latestSkeleton;
                   cancelShootFrameBuffer.Clear();
                   break;
               }

           } while (false);


           if (conditionResult == (int)ResultCodes.GestureDetected)
           {
               cancelShootResult = true;
           }
           else
           {
               cancelShootResult = false;
           }

           return cancelShootResult;
       }


       bool IsInSideShootBox(NuiElement newGestureData)
       {
           bool boxResult = false;

           //If the true skeleton has been detected
           if (this.trueCondtionSkeleton != null)
           {
               SkeletonData currentSkeleton = (from skeletons in newGestureData.GetSkeletonFrame().Skeletons
                                               where skeletons.TrackingState == SkeletonTrackingState.Tracked
                                               select skeletons).FirstOrDefault();

               float currentRelativeX = currentSkeleton.Joints[JointID.HandRight].Position.X;
               float currentRelativeY = currentSkeleton.Joints[JointID.HandRight].Position.Y;
               float currentRelativeZ = currentSkeleton.Joints[JointID.HandRight].Position.Z;

               float trueCondtionSkeletonX = trueCondtionSkeleton.Joints[JointID.HandRight].Position.X;
               float trueCondtionSkeletonY = trueCondtionSkeleton.Joints[JointID.HandRight].Position.Y;
               float trueCondtionSkeletonZ = trueCondtionSkeleton.Joints[JointID.HandRight].Position.Z;

               float thresholdX = this.thresholds.InsideBoxThresholdX;
               float thresholdY = this.thresholds.InsideBoxThresholdY;
               float thresholdZ = this.thresholds.InsideBoxThresholdZ;

               this.logger.Debug("threshold X = " + thresholdX);
               this.logger.Debug("threshold Y = " + thresholdY);
               this.logger.Debug("threshold Z = " + thresholdZ);

               //True conditions to be outside the tolerance box 
               if (currentRelativeX > (trueCondtionSkeletonX + thresholdX))
               {
                   
                   trueCondtionSkeleton = null;
                   this.logger.Debug("Not in box because of current relative X = " + currentRelativeX );
               }
               else if (currentRelativeX < (trueCondtionSkeletonX - thresholdX))
               {
                   trueCondtionSkeleton = null;
                   this.logger.Debug("Not in box because of current relative X (-threshold) = " + currentRelativeX);
               }
               else if (currentRelativeY > (trueCondtionSkeletonY + thresholdY))
               {
                   trueCondtionSkeleton = null;
                   this.logger.Debug("Not in box because of current relative Y = " + currentRelativeY);
               }
               else if (currentRelativeY < (trueCondtionSkeletonY - thresholdY))
               {
                   trueCondtionSkeleton = null;
                   this.logger.Debug("Not in box because of current relative Y (-threashold) = " + currentRelativeY);
               }
               else if (currentRelativeZ < (trueCondtionSkeletonZ - thresholdZ))
               {
                   trueCondtionSkeleton = null;
                   this.logger.Debug("ttt Not in box because of current relative Z = " + currentRelativeZ);
               }
               else
               {
                   boxResult = true;
                   this.logger.Debug("Currently inside the shoot box!!!!!!!!!!!!!!!!!!");
               }
           }

           if (trueCondtionSkeleton == null)
           {
               boxResult = false;
           }

           return boxResult;
       }
    }
}
