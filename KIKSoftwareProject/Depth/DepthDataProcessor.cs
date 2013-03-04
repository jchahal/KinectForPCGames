using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Microsoft.Research.Kinect.Nui;

namespace KIKSoftwareProject
{
    /// <summary>
    /// Processes the Depth data received from the Kinect
    /// </summary>
    class DepthDataProcessor
    {

        static readonly Object _threadLock = new Object();
        private Thread depthProcessorThread = null;
        private Semaphore handleRequests = null;
        private Queue<DepthData> dataQueue = null;
        private volatile bool threadExit;
        private Semaphore requestDepthData = null;
        KeyboardInputProcessor keyboardProcessor = null;
        
        public DepthDataProcessor(KeyboardInputProcessor keyboard)
        {
            keyboardProcessor = keyboard;
            handleRequests = new Semaphore(0, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);
            requestDepthData = new Semaphore((int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS, (int)SemaphoreConstants.MAX_CONCURRENT_REQUESTS);

            dataQueue = new Queue<DepthData>();
            threadExit = false;
            depthProcessorThread = new Thread(DepthProcessorModule);
            depthProcessorThread.Name = "DepthProcessorThread";
            depthProcessorThread.Start();            
        }

        public class DepthData
        {
            private ImageFrame imageFrameData = null;            

            public ImageFrame ImageFrame
            {
                get { return imageFrameData; }
                set { imageFrameData = value; }
            }
        }

        public void ExitThread()
        {
            threadExit = true;
            handleRequests.Release();
        }

        public int AddToQueue(DepthData depthData)
        {

            //Decrement the semaphore to make sure the spot is available
            this.requestDepthData.WaitOne();

            lock (_threadLock)
            {
                dataQueue.Enqueue(depthData);
            }

            //Increament the semaphore to indicate there is work to do
            int previousCount = handleRequests.Release();

            return previousCount;
        }       

        private DepthData RemoveFromQueue()
        {
            DepthData resultValue = null;

            lock (_threadLock)
            {
                resultValue = dataQueue.Dequeue();
            }

            //Increament the semaphore to indicate the spot is available
            int previousCount = this.requestDepthData.Release();

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

        private void DepthProcessorModule()
        {
            int processResult = (int)ResultCodes.Success;
            try
            {
                do
                {
                    handleRequests.WaitOne();

                    if (GetQueueCount() > 0)
                    {
                        DepthData newDepthData = RemoveFromQueue();
                        processResult = ProcessNewDepthData(newDepthData);
                    }

                } while (!threadExit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }

        }

        private int ProcessNewDepthData(DepthData newDepthdata)
        {

            PlanarImage Image = newDepthdata.ImageFrame.Image;

            CheckPlayerDepth(Image.Bits);           

            return (int)ResultCodes.Success;
        }

        private int CheckPlayerDepth(byte[] depthFrame16)
        {
            int[] playerMaxVal = new int[8];

            for (int j = 0; j < 8; j++)
            {

                playerMaxVal[j] = -1;

            }
            
            for (int i16 = 0; i16 < depthFrame16.Length; i16 += 2 )            
            {
                //Get player ID (if present) from the lower 3 bits i.e. bits 0-2 for each pixel
                int player = depthFrame16[i16] & 0x07;

                //Compute the depth (in mm) from bits 3-13 for each pixel given in the byte array
                int realDepth = (depthFrame16[i16 + 1] << 5) | (depthFrame16[i16] >> 3);
                playerMaxVal[0] = -1;

                switch (player)
                {
                    case 0:
                        Trace.WriteLine("Lenght: " + realDepth);
                        break;
                    case 1:
                        Trace.WriteLine("Player 1 lenght: " + realDepth);
                        if (realDepth > playerMaxVal[1])
                        {
                            playerMaxVal[1] = realDepth;
                        }
                        break;
                    case 2:
                        Trace.WriteLine("Player 2 lenght: " + realDepth);
                        if (realDepth > playerMaxVal[2])
                        {
                            playerMaxVal[2] = realDepth;
                        }
                        break;
                    case 3:
                        Trace.WriteLine("Player 3 lenght: " + realDepth);
                        if (realDepth > playerMaxVal[3])
                        {
                            playerMaxVal[3] = realDepth;
                        }
                        break;
                    case 4:
                        Trace.WriteLine("Player 4 lenght: " + realDepth);
                        if (realDepth > playerMaxVal[4])
                        {
                            playerMaxVal[4] = realDepth;
                        }
                        break;
                    case 5:
                        Trace.WriteLine("Player 5 lenght: " + realDepth);
                        if (realDepth > playerMaxVal[5])
                        {
                            playerMaxVal[5] = realDepth;
                        }

                        break;
                    case 6:
                        Trace.WriteLine("Player 6 lenght: " + realDepth);
                        if (realDepth > playerMaxVal[6])
                        {
                            playerMaxVal[6] = realDepth;
                        }
                        break;
                    case 7:
                        Trace.WriteLine("Player 7 lenght: " + realDepth);
                        if (realDepth > playerMaxVal[7])
                        {
                            playerMaxVal[7] = realDepth;
                        }
                        break;
                }
                int i = 0;

                foreach (int val in playerMaxVal)
                {
                    if (val != -1)
                    {
                        Trace.WriteLine("PLayer " + i + " Max depth value reached = " + val);
                    }
                    i++;
                }
            }

            return (int)ResultCodes.Success;
        }        
    }
}
