/* THIS PROJECT IS CREATED BY LANCE SEIDMAN <lance.compulsivetech.biz> (@LanceSeidman on Twitter)
 * 
 * Description: This project is designed to allow you to communicate with the i-Racer RC Bluetooth
 * Car from SparkFun using a Nokia Lumia 920 Windows Phone 8 Device (tested) or equivlent but is
 * NOT compatible for Windows Phone 7.x (not-tested).
 * 
 * IMPORTANT: THIS PROJECT IS TO LET YOU USE MOTION TO CONTROL THE DEVICE.
 *            BUTTONS EXIST, JUST MAKE THEM VISIBLE.
 * 
 * This is part of my GitHub of projects for Bluetooth & Windows Phone 8. You will hopefully see
 * other projects in the GH eventually.
 * 
 * See it in Action!
 * http://www.youtube.com/TechMeShow - Offical Videos by Me & Demo's + Reviews
 * 
 * Donate to the Project
 * You can send a Donation to lance@compulsivetech.biz if you'd like, it's going to go towards my
 * education/retail products that I am trying to make and offer to teach people how to use Hardware
 * and Software with ANY OS that has Bluetooth.
 * 
 * GitHub Project Site
 * https://github.com/lanceseidman/Arduino-Bluetooth-WinPhone8 - ALL future Open updates for WP8
 * 
 * Supplies/Where to Buy
 * 1). i-Racer (bought from SparkFun: https://www.sparkfun.com/products/11162)
 * 
 * Thank you/Recognition to...
 * Microsoft BizSpark Program
 * Become successful with Microsoft and its Partners... Helping Wearing Digital become a Brand.
 * 
 * Microsoft UserCommunity Team
 * Thank you for Sharing my Projects, Code, and being awesome people...
 * 
 * MSDN/Microsoft + Nokia Development Team
 * I apperciate the outline and understanding of how Bluetooth works a lot better...
 * 
 * SPHERO
 * Awesome product & thank you for your Buffer Function. More important? Making WP8 a Device that
 * can use your product. Exactly what my goal is, to get more WP8 Enabled Hardware.
 * 
 */

using Arduino2WP8.Resources;
using System;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using Microsoft.Phone.Reactive;
using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework;
//using Windows.Devices.Sensors;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Phone.Speech.Recognition;
using Windows.Phone.Speech.Synthesis;
using Microsoft.Devices.Sensors;


namespace Arduino2WP8
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public partial class MainPage : PhoneApplicationPage
    {
        // Let's Define what we'll use throughout our App
        StreamSocket BTSock; // Socket used to Communicate with the Arduino/BT Module
        Accelerometer ACC_Sensor; // Create an Accelerometer to be used in our App
        int isEnabled; // Use to store when we can turn on Sensor, or w/e else

        /* This is used for our Speech/JSON API App
         * SpeechRecognizer mySpeech; // Used for Recognizing App Speech (not yet in this App)
         * SpeechSynthesizer mySpeechSS = new SpeechSynthesizer();
         * 
         * This is used to Grab Content from the Net, we'll be using GZIP in the end
         * WebClient wc = new WebClient(); // Setting up our WebClient so we can just use "wc" and could be used to get an API
        */

        /* Let's Store our Strings (Other Projects)
        string BTStatus = ""; // Used to Store if we can send Message (e.g. yes or no)
        string BT_Received = ""; // We'll use to store Bluetooth Received Data
        string whattosay = ""; // Used later to accept input for Speech 
        */

        // Constructor
        public MainPage()
        {
            InitializeComponent(); // NEVER REMOVE!

            // Let's make sure the Emulator isn't loaded...
            if (Microsoft.Devices.Environment.DeviceType == Microsoft.Devices.DeviceType.Emulator)
            {
                // Send an Error Message to User
                MessageBox.Show("Sorry, Bluetooth isn't compatible in this enviornment. Please use your Phone.", "Error: Device Required", MessageBoxButton.OK);
                return; // Close
            }
            else
            {
                if (Accelerometer.IsSupported)
                {
                    isEnabled = 1; // Passed the Tests, let's go!

                    ACC_Sensor = new Accelerometer(); // Create a new Accelerometer
                    ACC_Sensor.TimeBetweenUpdates = TimeSpan.FromMilliseconds(20); // May wish to change based on Feedback
                    // When the Sensor Value Changes for X,Y,Z go update, send the reading to be read/understood.
                    ACC_Sensor.CurrentValueChanged += new EventHandler<SensorReadingEventArgs<AccelerometerReading>>(accelSensor_CurrentValueChanged);
                    ACC_Sensor.Start(); // Enable/Start the Sensor

                    Loaded += MainPage_Loaded; // We need Async.
                }
                else
                {
                    txtBTStatus.Text = "No Accelerometer Available.";
                    return;
                }
            }
        }

        void accelSensor_CurrentValueChanged(object sender, SensorReadingEventArgs<AccelerometerReading> e)
        {
            Dispatcher.BeginInvoke(() => GoChange(e.SensorReading)); // Send the Data to the Acc. Reading Function
        }

        private void GoChange(AccelerometerReading accelerometerReading)
        {
            if (isEnabled == 1 || BTSock == null)
            {
                // SHOW LIVE UPDATING; SHOULD REMOVE IN REAL WORLD USE
                txtX.Text = accelerometerReading.Acceleration.X.ToString("0.00");
                txtY.Text = accelerometerReading.Acceleration.Y.ToString("0.00");
                txtZ.Text = accelerometerReading.Acceleration.Z.ToString("0.00");

                // ALL VALUES CAN BE CHANGED TO HOW YOU SEE FIT. THIS WAS BEST FROM MY RESEARCH/TESTING.
                if (accelerometerReading.Acceleration.Y < -0.1)
                {
                    txtTurnStatus.Text = "Right";
                    BT2Arduino_Send("\x6C"); // Fast Right
                }
                else
                {
                    if (accelerometerReading.Acceleration.Y < 0.8)
                    {
                        txtTurnStatus.Text = "Left";
                        BT2Arduino_Send("\x5C"); // Fast Left
                    }
                }
                if (accelerometerReading.Acceleration.Z > -0.05)
                {
                    txtTurnStatus.Text = "Reverse";
                    BT2Arduino_Send("\x6C"); // Fast Reverse
                }
                else
                {
                    if (accelerometerReading.Acceleration.Z < -0.20)
                        txtTurnStatus.Text = "Forward";
                    BT2Arduino_Send("\x1C"); // Fast Forward
                }
            }
        }

        private async void  MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            PeerFinder.Start(); // Start/Turn on PeerFinder
            PeerFinder.AlternateIdentities["Bluetooth:Paired"] = ""; // Find/Get All Paired BT Devices
            var peers = await PeerFinder.FindAllPeersAsync(); // Make peers the container for All BT Devices
            txtBTStatus.Text = "Finding Paired Devices..."; // Tell UI what is going on in case it's Slow

            // GET & SHOW ONLY SPECIFIC PAIRED DEVICE
            for (int i = 0; i < peers.Count; i++)
            {
                if (peers[i].DisplayName.Contains("DaguCar"))
                {
                    lstBTPaired.Items.Add(peers[i].DisplayName); // Add the DaguCar since it was found.
                }
                else // We can't find it. Is it Turned on? Named something else?
                {
                    txtBTStatus.Text = "Can't find RC Car"; // Alert we can't find the Device.
                }
            }

            // EXTRA'S, TIPS & LEARNING TRICKS BELOW
            
            /* GET & SHOW ALL FOUND PAIRED DEVICES
             * 
             * You can uncomment this if you want to show ALL Paired Devices...
             * In this Sample, we just want 1 Device to show up, our RC Car.
             
            if (peers.Count < 0)
            {
                txtBTStatus.Text = "Can't find any Devices";
                return;
            }
            else
            {
                for (int i = 0; i < peers.Count; i++)
                {
                    lstBTPaired.Items.Add(peers[i].DisplayName); // Add all found Paired to List.
                }
            }*/


            /* Only want 1st Paired Device to Show? Uncomment...
             * 
             * Not too sure why you'd want this but if so, please add Error control to it!
             
               lstBTPaired.Items.Add(peers[0].DisplayName); // 1 Paired Device to Show 
             
            */

           /* YOU CAN USE THIS TO SHOW AMOUNT OF DEVICES FOUND
            
           if (peers.Count <= 2) // No real need for this, unless you have multiple RC Car's.
            {
                txtBTStatus.Text = "Found " + peers.Count + " Devices";
            }
            */

        }

        private async void BT2Arduino_Send (string WhatToSend) 
        {
            if (BTSock == null) // If we don't have a connection, Send Error Control
            {
               // MessageBox.Show("Please connect to a device first."); // Alert the user with a Notification (Optional)
                txtBTStatus.Text = "No RC Car Connection. Waiting..."; // Alert the UI
                return; // Stop
            }
            else
                if (BTSock != null) // Since we have a Connection
                {
                    var datab = GetBufferFromByteArray(UTF8Encoding.UTF8.GetBytes(WhatToSend)); // Create Buffer/Packet for Sending
                    
                    await BTSock.OutputStream.WriteAsync(datab); // Send our Message to Connected Arduino
                   // txtBTStatus.Text = "Message Sent (" + WhatToSend + ")"; // Show what we sent to Device to UI (good for Debug)
                   
                }
        }

       
        // FUNCTION PROVIDED BY SPHERO
        private IBuffer GetBufferFromByteArray(byte[] package)
        {
            using (DataWriter dw = new DataWriter())
            {
                dw.WriteBytes(package);
                return dw.DetachBuffer();
            }
        }

        /* Used for our Speech App
         * private async void GoSpeak()
        {
            // Assuming we set what we wanted to Say, this will Speak it (e.g. whattosay = "Hello everyone!";)
            await mySpeechSS.SpeakTextAsync(whattosay);
            
        }*/

        private async void lstBTPaired_Tap_1(object sender, GestureEventArgs e)
        {
            if (lstBTPaired.SelectedItem == null) // To prevent errors, make sure something is Selected
            {
                //btnConnectArduino.IsEnabled = false; // Make sure it's False if you want to use a Button
                txtBTStatus.Text = "No Device Selected! Try again..."; // Set UI Output
                return;
            }
            else
            {
                if (lstBTPaired.SelectedItem != null) // Just making sure something was Selected
               

                    // btnConnectArduino.IsEnabled = true; // Since an item is Selected, Enable Connect Button (If using a Button)

                    /* This is a trick to Grab the Item and Remove '(' and ')' if using the Hostname & want just the Contents (00:00:00)
                    // Of course we don't HAVE to do this, but this is a C# Trick/Hack to learn String Functions
                     
                    string ba = lstBTPaired.SelectedItem.ToString(); // Store the Tapped/Selected Item
                    int found = 0; // Set the Found to 0
                    found = ba.IndexOf("("); // Let's get the Index of the "(" in the String (ba)
                    ba = ba.Substring(found + 1); // Use Substring with the IndexOf
                    ba = ba.Replace(")", ""); // Now remove the last ")" in the String to be "00:00:00:00:00"
                      
                    Test our Hack by Uncommenting Below...
                    MessageBox.Show(ba); - This is just to make sure we did it right */

                    PeerFinder.AlternateIdentities["Bluetooth:Paired"] = ""; // Grab Paired Devices
                    var PF = await PeerFinder.FindAllPeersAsync(); // Store Paired Devices

                    BTSock = new StreamSocket(); // Create a new Socket Connection
                    await BTSock.ConnectAsync(PF[lstBTPaired.SelectedIndex].HostName, "1"); // Connect using Socket to Selected Item
                    // BT2Arduino_Send("HELLO"); // Normally we send this to start any settings in our remote device.

                    /* Once Connected, let's give a HELLO
                     * - USED FOR OUR ORIGINAL PROJECT
                    var datab = GetBufferFromByteArray(Encoding.UTF8.GetBytes("HELLO")); // Create Buffer/Packet for Sending
                    await BTSock.OutputStream.WriteAsync(datab); // Send Arduino Buffer/Packet Message

                    btnSendCommand.IsEnabled = true; // Allow commands to be sent via Command Button (Enabled)
                    */
                    
                }
        }

        /*
        private void btnSendCommand_Click(object sender, RoutedEventArgs e)
        {
            // In this Demo, our Device code knows to look for "3" to turn off/on LED/Motor for 4 Seconds
            BT2Arduino_Send("3"); // This will send using the GoSend Feature
        }*/

        // TO USE THESE BUTTONS, TURN VISIBILITY BACK ON.
        private void btnUp_Tap_1(object sender, GestureEventArgs e)
        {
            BT2Arduino_Send("\x1C");
        }

        private void btnLeft_Tap_1(object sender, GestureEventArgs e)
        {
            BT2Arduino_Send("\x5C");
        }

        private void btnRight_Tap_1(object sender, GestureEventArgs e)
        {
            BT2Arduino_Send("\x6C");
        }

        private void btnDown_Tap_1(object sender, GestureEventArgs e)
        {
            BT2Arduino_Send("\x2C");
        }

        private void btnSTOP_Tap_1(object sender, GestureEventArgs e)
        {
            BT2Arduino_Send("\x00");
        }

    }
}