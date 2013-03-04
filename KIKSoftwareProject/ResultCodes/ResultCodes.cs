using System;
using System.Text;

namespace KIKSoftwareProject
{

    /// <summary>
    /// Different result values
    /// </summary>
    enum ResultCodes : int
    {
        Success = 0,
        NuiIntializationFailed,
        OutOfMemory,
        UnableToWakeUpThread,
        VideoProcessorFailed,
        KeyPressFailed,
        KeyReleaseFailed,
        GameNotFound,
        GameSetFocusFail,
        KeyboardCommandInvalid,  
        SoundProcessorFailed,
        GestureDetected,
        GestureDetectionFailed,
        MouseInputFailed,
        NullTrackedSkeleton,
        InvalidPersistance
    }

    /// <summary>
    /// Maximum concurrent requests to be handled
    /// </summary>
    enum SemaphoreConstants : int
    {
        MAX_CONCURRENT_REQUESTS = 10000
    }

    /// <summary>
    /// Keyboard action presistance value
    /// </summary>
    enum KeyboardPersistance : int
    {
        PRESS = 0,
        PRESS_AND_RELEASE,
        RELEASE,
        UNKNOWN
    }

    /// <summary>
    /// Keyboard action
    /// </summary>
    enum KeyboardAction : int
    {
        MOVE_RIGHT = 0,        
        MOVE_LEFT,
        MOVE_UP,
        MOVE_DOWN,
        JUMP,
        LONG_JUMP,
        SHOOT,
        STOP,
        ESC,
        UP_ARROW,
        DOWN_ARROW,
        LEFT_ARROW,
        RIGHT_ARROW,
        RELOAD,
        ENTER,
        KNIFE,
        GRENADE
    }

    enum MouseScalingFactor: int
    {
        SCALE_FACTOR = 65535
    }

    enum MouseButton : int
    {
        RIGHT_MOUSE_BUTTON = 0,
        LEFT_MOUSE_BUTTON,
        WHEEL_MOVE_UP,
        WHEEL_MOVE_DOWN,
        MOUSE_MOVE
    }

    enum MousePresistance : int
    {
        RELEASE = 0,
        PRESS_AND_HOLD,
        PRESS_AND_RELEASE,
        DOUBLE_CLICK_HOLD
    }
    
    enum SkeletalDistance : int
    {
        //START_DISTANCE = 16000,
        MOVE_FORWARD = 13000,
        MOVE_BACKWARD = 19000
    }

    enum GestureID : int
    {
        LEFT_LEG_MOVED = 0,
        RIGHT_LEG_MOVED
    }
    
    /// <summary>
    /// Provides methods to convert result codes to strings
    /// </summary>
    class ResultCodeToString
    {
        
        public String ToString(ResultCodes code)
        {
            StringBuilder resultCodeToString = new StringBuilder();

            do
            {
                if (code == ResultCodes.NuiIntializationFailed)
                {
                    resultCodeToString.Append("Error - Unable to initialize the Kinect sensors. ");
                    resultCodeToString.Append("Make sure Kinect is connected to the computer");
                    break;
                }

                if (code == ResultCodes.OutOfMemory)
                {
                    resultCodeToString.Append("Error: System is out of memory");
                    break;
                }

            } while (false);

            return resultCodeToString.ToString();
        }

    }
}
