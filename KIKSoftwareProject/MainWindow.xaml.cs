using System;
using System.Windows;

namespace KIKSoftwareProject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectNuiInterface kinectNui = null;
        private int displayStopCount = 0;
        private int audioStopCount = 0;
        
        public MainWindow()
        {
            InitializeComponent();            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
            System.Threading.Thread.CurrentThread.Name = "MainThread";
            kinectNui = new KinectNuiInterface(videoStreamToForm, skeletonToForm, transformedSkeletonToForm);              
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            kinectNui.TerminateApplication();
            Environment.Exit(0);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            displayStopCount++;

            if (kinectNui != null)
            {
                if ((this.displayStopCount % 2) != 0)
                {
                    kinectNui.StopVisualThread = true;
                }
                else
                {
                    kinectNui.StopVisualThread = false;
                }
            }

            if (this.displayStopCount >= 2)
            {
                this.displayStopCount = 0;
            }
        }

        private void button1_Click_1(object sender, RoutedEventArgs e)
        {
            audioStopCount++;

            if (kinectNui != null)
            {
                if ((this.audioStopCount % 2) != 0)
                {
                    kinectNui.StopAudioThread = true;
                }
                else
                {
                    kinectNui.StopAudioThread = false;
                }
            }

            if (this.audioStopCount >= 2)
            {
                this.audioStopCount = 0;
            }
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {

        }        
    }
}
