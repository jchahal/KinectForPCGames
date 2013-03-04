using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace KIKSoftwareProject
{
    /// <summary>
    /// Provides the methods to setup the game to receive inputs from the Window's api's
    /// </summary>
    class GameDataProcessor
    {
        /// <summary>
        /// Sets the window to foreground and activates it
        /// </summary>
        /// <param name="hwnd">handle of the window</param>
        /// <returns></returns>
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        /// <summary>
        /// Activates the window pointed by the window handle
        /// </summary>
        /// <param name="handle">handle of the window to be activated</param>
        /// <returns></returns>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetActiveWindow(IntPtr handle);

        /// <summary>
        /// Brings the window to top
        /// </summary>
        /// <param name="handle">handle of the window to bring to top</param>
        /// <returns></returns>
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool BringWindowToTop(IntPtr handle);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnableWindow(IntPtr handle, bool bEnable);

        [DllImport("user32.dll")]
        static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetFocus();

        private Process gameProcess = null;
        private int sleepTime = 10;
        IntPtr topWindowHandle = IntPtr.Zero;

         
        public class GameInfo
        {

            public GameInfo(String name)
            {
                gameName = name;                
            }
            
            private String gameName;
            private IntPtr gameHandle;

            public String GetGameName()
            {
                return this.gameName;
            }

            public void SetGameName(String name)
            {
                this.gameName = name;
            }

            public IntPtr GetGameHandle()
            {
                return this.gameHandle;
            }

            public void SetGameHandle(IntPtr handle)
            {
                this.gameHandle = handle;
            }       
        }

        /// <summary>
        /// Checks if the current focus is set to the game or not
        /// </summary>
        /// <returns>if current window handle is equal to the keyboard focus then true is returned else false is returned </returns>
        public bool IsFocusSet()
        {
            bool focusSet = false;
            IntPtr currentFocusindowHandle = GetFocus();

            if (!this.topWindowHandle.Equals(currentFocusindowHandle))
            {
                focusSet = false;
            }

            else
            {
                focusSet = true;
            }

            return focusSet;
        }

        public GameInfo GameSetFocus(String gameInterested)
        {
            GameInfo gameResult = null;

            do
            {

                if (String.IsNullOrEmpty(gameInterested))
                {
                    gameResult = null;
                    break; 
                }

                gameResult = new GameInfo(gameInterested);

                if (this.gameProcess == null || this.gameProcess.HasExited)
                {
                    gameResult = null;
                    break;
                }

                //Get the handle to the top child window of the game process
                this.topWindowHandle = GetTopWindow(this.gameProcess.MainWindowHandle);

                //if the parent window doesnt have any child window
                if (topWindowHandle == IntPtr.Zero)
                {
                    topWindowHandle = this.gameProcess.MainWindowHandle;
                }

                //set the game name
                gameResult.SetGameName(gameInterested);

                //set the game handle
                gameResult.SetGameHandle(topWindowHandle);

                //Enable the window to take keyboard or mouse input in case it was disabled
                bool setWindow = EnableWindow(topWindowHandle, true);

                //Bring the Mario window to front
                setWindow = BringWindowToTop(topWindowHandle);

                //Sleep for the operation to take place
                System.Threading.Thread.Sleep(sleepTime);

                if (!setWindow)
                {
                    gameResult = null;
                    break;
                }

                //Set the mario to be the foreground window
                setWindow = SetForegroundWindow(topWindowHandle);

                //Sleep for the operation to successfully be completed
                System.Threading.Thread.Sleep(sleepTime);

                if (!setWindow)
                {
                    gameResult = null;
                    break;
                }

            } while (false);

            return gameResult;
        }

        public int SearchRunningGame(String gameName)
        {
            int searchResult = (int)ResultCodes.Success;
            do
            {
                List<Process> allRunningProcesses = new List<Process>();

                //Add all the currently running processes into the list
                allRunningProcesses.AddRange(Process.GetProcesses());

                this.gameProcess = allRunningProcesses.Find(value => value.ProcessName.Equals(gameName));

                if (this.gameProcess == null)
                {
                    searchResult = (int)ResultCodes.GameNotFound;
                    break;
                }

                searchResult = (int)ResultCodes.Success;

            } while (false);

            return searchResult;
        }
    }
}
