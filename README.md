KinectForPCGames
================
KIK project - Kinect Interface for (K)Computer games

This project uses Microsoft Kinect hardware and Microsoft development SDK for Kinect (beta 2) to provide live gestures and sound commands support for playing and interacting with computer games.

Key features of the project -:
1) Recording and interpreting user movements and gestures into different movements in the game
2) Recording sound commands from the user and performing different actions in the game
3) Live video feed of the user movement, Skeleton movements and also screen with transformed user skeleton in case the user rotates
4) Application of different transformations to record gestures correctly when user moves away from the Kinect sensor
5) Complete multi-threaded support to perform all the different tasks.

Developed with game 'Call of Duty' as a test games. Following is the list of gestures supported -:
1) Jump Gesture
2) Movement of the hand in a imaginary 3D box in front of the user to change view in the game (to replace mouse movements)
3) Move left and right
4) Move front and back
5) Shooting gesture
6) Pausing the game
7) Starting the game

Sound commands supported -:
1) Jump
2) Reload
3) Aim
4) Knife
5) Grenade
6) Menu
7) Pause
8) Select
9) Okay
10) Enter
11) up
12) Down
13) left
14) Right


Note: Tested and verified with Kinect SDK beta2
Dependencies: log4net.dll for logging purpose
