using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;
using log4net;
using log4net.Repository.Hierarchy;

namespace KIKSoftwareProject.Gestures
{
    /// <summary>
    /// Provides methods to perform the Skeleton points transformations
    /// </summary>
    class SkeletonTransformation
    {
        TransformedJoints glbTransformedSkeleton = new TransformedJoints();
        private log4net.ILog kinectEventLogger = null;

        /// <summary>
        /// Configure the log manager
        /// </summary>
        void ConfigureLogManager()
        {

            Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
            Logger rootLogger = hierarchy.Root;

            Logger coreLogger = hierarchy.GetLogger("KIKSoftwareProject.SkeletonTransformation") as Logger;
            coreLogger.Parent = rootLogger;

            log4net.Appender.RollingFileAppender fileAppender = new log4net.Appender.RollingFileAppender();
            fileAppender.File = "Transformation_log.txt";
            fileAppender.AppendToFile = true;
            fileAppender.MaximumFileSize = "100MB";
            fileAppender.MaxSizeRollBackups = 5;
            fileAppender.Layout = new log4net.Layout.PatternLayout(@"%date [%thread] %-5level %logger - %message%newline");
            fileAppender.StaticLogFileName = true;
            fileAppender.ActivateOptions();

            coreLogger.RemoveAllAppenders();
            coreLogger.AddAppender(fileAppender);
            hierarchy.Configured = true;

            kinectEventLogger = LogManager.GetLogger("KIKSoftwareProject.SkeletonTransformation");

            log4net.LogManager.GetRepository().Threshold = log4net.Core.Level.Debug;

        }
        public SkeletonTransformation()
        {
            ConfigureLogManager();
        
        }


        public class TransformedJoints
        {
            public Dictionary<JointID, Vector> jointsDictionary = new Dictionary<JointID, Vector>() { 
            {JointID.HipCenter, new Vector()},
            {JointID.Spine, new Vector()},
            {JointID.ShoulderCenter,new Vector()},
            {JointID.Head,new Vector()},
            {JointID.ShoulderLeft,new Vector()},
            {JointID.ElbowLeft,new Vector()},
            {JointID.WristLeft,new Vector()},
            {JointID.HandLeft,new Vector()},
            {JointID.ShoulderRight,new Vector()},
            {JointID.ElbowRight,new Vector()},
            {JointID.WristRight,new Vector()},
            {JointID.HandRight,new Vector()},
            {JointID.HipLeft,new Vector()},
            {JointID.KneeLeft,new Vector()},
            {JointID.AnkleLeft,new Vector()},
            {JointID.FootLeft,new Vector()},
            {JointID.HipRight,new Vector()},
            {JointID.KneeRight,new Vector()},
            {JointID.AnkleRight,new Vector()},
            {JointID.FootRight,new Vector()}
        };
        }

        /// <summary>
        /// Give two points p1 and p2. 
        /// will calculate p3 = projection of p2 on z-plane of p1.
        /// then returns angle made between (p3-p2) and (p3-p1)
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="z1"></param>
        /// <param name="x2"></param>
        /// <param name="y2"></param>
        /// <param name="z2"></param>
        /// <returns>theta</returns>
        float CalculateTheta (float x1, float y1, float z1, float x2, float y2, float z2)
        {
            float x3 = x2;
            float y3 = y2;
            float z3 = z1;

            float d1 = 0.0f;
            float d2 = 0.0f;
            float d3 = 0.0f;

            float resultTheta = 0.0f;
            
            //distances between points
            //d1 between p2 and p3
            //d2 between p1 and p3
            //d3 between p1 and p2
            d1 = (float) Math.Sqrt((x2 - x3) * (x2 - x3) + (y2 - y3) * (y2 - y3) + (z2 - z3) * (z2 - z3));
            d2 = (float)Math.Sqrt((x1 - x3) * (x1 - x3) + (y1 - y3) * (y1 - y3) + (z1 - z3) * (z1 - z3));
            d3 = (float)Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2) + (z1 - z2) * (z1 - z2));

            float costheta = (d2 * d2 + d3 * d3 - d1 * d1) / (2 * d2 * d3);
            resultTheta = (float) Math.Acos(costheta);
            if (z1 > z2)
            {
                return resultTheta;
            }
            else
            {
                return -resultTheta;
            }
        }

        public Dictionary<JointID, Vector> TransformSkeleton(SkeletonFrame frame)
        {
            SkeletonData latestSkeleton = (from skeletons in frame.Skeletons
             where skeletons.TrackingState == SkeletonTrackingState.Tracked
             select skeletons).FirstOrDefault();
            
            float theta = CalculateTheta(latestSkeleton.Joints[JointID.ShoulderRight].Position.X,
                latestSkeleton.Joints[JointID.ShoulderRight].Position.Y,
                latestSkeleton.Joints[JointID.ShoulderRight].Position.Z,
                latestSkeleton.Joints[JointID.ShoulderLeft].Position.X,
                latestSkeleton.Joints[JointID.ShoulderLeft].Position.Y,
                latestSkeleton.Joints[JointID.ShoulderLeft].Position.Z);
            
                kinectEventLogger.Debug("theta = " + theta);

            foreach (Joint newJoint in latestSkeleton.Joints)
            {
                glbTransformedSkeleton.jointsDictionary[glbTransformedSkeleton.jointsDictionary.Keys.First(v => v.Equals(newJoint.ID))] = newJoint.Position;                
            }

            Vector translate = latestSkeleton.Joints[JointID.HipCenter].Position;

            Dictionary<JointID, Vector>.KeyCollection allKeys = this.glbTransformedSkeleton.jointsDictionary.Keys;

            int keyCount = allKeys.Count;

            for (int i = 0; i < keyCount; i++)
            {

                Vector newVector = new Vector();


                kinectEventLogger.Debug("Before translate.X = " + translate.X);
                kinectEventLogger.Debug("Before translate.Y = " + translate.Y);
                kinectEventLogger.Debug("Before translate.Z = " + translate.Z);

                //Translate to origin
                newVector.X = this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].X - translate.X;
                newVector.Y = this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Y - translate.Y;
                newVector.Z = this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Z - translate.Z;

                kinectEventLogger.Debug("Before this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].X = " + this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].X);
                kinectEventLogger.Debug("Before this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Y = " + this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Y);
                kinectEventLogger.Debug("Before this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Z = " + this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Z);

                kinectEventLogger.Debug("Before newVector.X = " + newVector.X);
                kinectEventLogger.Debug("Before newVector.Y = " + newVector.Y);
                kinectEventLogger.Debug("Before newVector.Z = " + newVector.Z);

             
                ////rotate at origin along y
                newVector.X = newVector.X * (float)Math.Cos(theta) + newVector.Z * (float)Math.Sin(theta);
                newVector.Z = -newVector.X * (float)Math.Sin(theta) + newVector.Z * (float)Math.Cos(theta);

                kinectEventLogger.Debug("After translate.X = " + translate.X);
                kinectEventLogger.Debug("After translate.Y = " + translate.Y);
                kinectEventLogger.Debug("After translate.Z = " + translate.Z);

                //Translate back
                newVector.X = newVector.X + translate.X;
                newVector.Y = newVector.Y + translate.Y;
                newVector.Z = newVector.Z + translate.Z;

                kinectEventLogger.Debug("After this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].X = " + this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].X);
                kinectEventLogger.Debug("After this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Y = " + this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Y);
                kinectEventLogger.Debug("After this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Z = " + this.glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]].Z);

                kinectEventLogger.Debug("After newVector.X = " + newVector.X);
                kinectEventLogger.Debug("After newVector.Y = " + newVector.Y);
                kinectEventLogger.Debug("After newVector.Z = " + newVector.Z);

                //Store the transformed points in global 
                glbTransformedSkeleton.jointsDictionary[allKeys.ToArray()[i]] = newVector;


            }

            return glbTransformedSkeleton.jointsDictionary;
        }
    }
}
