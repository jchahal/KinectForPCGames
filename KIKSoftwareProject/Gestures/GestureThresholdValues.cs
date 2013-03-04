using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// class holds the threshold values for different gestures
    /// </summary>
    public class GestureThresholdValues
    {
        public class MoveLeftRightThresholds
        {
            private int frameBufferSize = 60;
            private int thresholdGestureTime = 1200;
            private float thresholdDistance = 0.35f;

            public int ThresholdGestureTime
            {
                get { return this.thresholdGestureTime; }               
            }

            public int ThresholdFrameBufferSize
            {
                get { return this.frameBufferSize; }
            }

            public float ThresholdDistance
            {
                get { return this.thresholdDistance; }
            }
        }

        public class JumpGesture
        {
            private int frameBufferSize = 60;
            private int thresholdGestureTime = 800;
            private float thresholdDistance = 0.3f;

            public int ThresholdGestureTime
            {
                get { return this.thresholdGestureTime; }
            }

            public int ThresholdFrameBufferSize
            {
                get { return this.frameBufferSize; }
            }

            public float ThresholdDistance
            {
                get { return this.thresholdDistance; }
            }
        }

        public class ShootingGestures
        {

            private int frameBufferSize = 60;
            private int thresholdGestureTime = 1200;

            private float aimThresholdY = 0.0f;

            private float shootThresholdDistance = 0.0f;
            private float shootThresholdXY = 0.0f;

            private float insideBoxThresholdX = 0.0f;
            private float insideBoxThresholdY = 0.0f;
            private float insideBoxThresholdZ = 0.0f;

            private float jackThresholdY = 0.0f;

            private bool isThresholdSet = false;

            public int ThresholdGestureTime
            {
                get { return this.thresholdGestureTime; }
            }

            public int ThresholdFrameBufferSize
            {
                get { return this.frameBufferSize; }
            }

            public float AimThresholdY
            {
                get { return this.aimThresholdY; }
            }

            public float ShootThresholdDistance
            {
                get { return this.shootThresholdDistance; }
            }

            public float ShootThresholdXY
            {
                get { return this.shootThresholdXY; }
            }

            public float InsideBoxThresholdX
            {
                get { return this.insideBoxThresholdX; }
            }

            public float InsideBoxThresholdY
            {
                get { return this.insideBoxThresholdY; }
            }

            public float InsideBoxThresholdZ
            {
                get { return this.insideBoxThresholdZ; }
            }

            public float JackThresoldY
            {
                get { return this.jackThresholdY; }
            }

            public bool IsThresholdSet
            {
                get { return this.isThresholdSet; }
                set { this.isThresholdSet = value; }
            }

            public void SetThresholdValues(SkeletonData trackedSkeleton)
            {
                if (trackedSkeleton == null)
                {
                    return;
                }

                Joint rightHand = trackedSkeleton.Joints[JointID.HandRight];
                Joint rightShoulder = trackedSkeleton.Joints[JointID.ShoulderRight];
                
                this.aimThresholdY = Math.Abs(rightShoulder.Position.X - trackedSkeleton.Joints[JointID.ShoulderLeft].Position.X) * 0.8f;

                this.shootThresholdDistance = (Math.Abs(trackedSkeleton.Joints[JointID.ShoulderRight].Position.X - trackedSkeleton.Joints[JointID.ShoulderLeft].Position.X)) * 0.20f;

                this.shootThresholdXY = (Math.Abs(trackedSkeleton.Joints[JointID.ShoulderRight].Position.X - trackedSkeleton.Joints[JointID.ShoulderLeft].Position.X)) * 0.15f;

                this.insideBoxThresholdX = (Math.Abs(trackedSkeleton.Joints[JointID.ShoulderRight].Position.X - trackedSkeleton.Joints[JointID.ShoulderLeft].Position.X)) * 0.35f;

                this.insideBoxThresholdY = this.insideBoxThresholdX;

                this.insideBoxThresholdZ = (Math.Abs(trackedSkeleton.Joints[JointID.ShoulderRight].Position.X - trackedSkeleton.Joints[JointID.ShoulderLeft].Position.X)) * 0.2f;

                this.jackThresholdY = Math.Abs(rightShoulder.Position.X - trackedSkeleton.Joints[JointID.ShoulderLeft].Position.X);

                this.isThresholdSet = true;
            }
        }
    }
}

