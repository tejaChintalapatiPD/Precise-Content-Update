using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Precise_Content_Update
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public string displayIPAddress = "192.168.29.101";
        public int dynamicElementID = 1;
        public string displayContent = "Hello World";
        public int displayBrightness = 1;

        public string DisplayIPAddress
        {
            get => displayIPAddress;
            set
            {
                displayIPAddress = value;
            }
        }

        
        public int DynamicElementID
        {
            get => dynamicElementID;
            set
            {
                dynamicElementID = value;
            }
        }

        public string DisplayContent
        {
            get => displayContent;
            set
            {
                displayContent = value;
            }
        }

        public int DisplayBrightness
        {
            get => displayBrightness;
            set
            {
                displayBrightness = value;
            }
        }

        public UInt16 calculateChecksum(List<byte> inputBuffer, UInt16 offset)
        {
            try
            {
                UInt16 calculatedCRC = 0x1D0F;

                for (int i = 0; i < inputBuffer.Count - offset; i++)
                {
                    calculatedCRC = (ushort)(calculatedCRC ^ (inputBuffer[i + offset] << 8));

                    for (int j = 0; j < 8; j++)
                    {
                        if ((calculatedCRC & 0x8000) != 0)
                            calculatedCRC = (ushort)((calculatedCRC << 1) ^ 0x1021);
                        else
                            calculatedCRC <<= 1;
                    }
                }

                return calculatedCRC;
            }
            catch (Exception calculateChecksumException)
            {
                Debug.Print("Excpetion raised in: MainPage.xaml.cs -> calculateChecksum -> calculateChecksumException. Exception Message: " + calculateChecksumException.Message);
                return 0xFFFF;
            }
        }

        public String SendDataOnTCP(byte[] messageToTransmit, int messageToTransmitLength, String IPAddress, int PortNumber, bool ExpectingAcknowledgement)
        {
            try
            {
                TcpClient TCPClientConnection = new TcpClient(AddressFamily.InterNetwork);

                try
                {
                    TCPClientConnection.ConnectAsync(IPAddress, PortNumber);
                    Thread.Sleep(100);
                    if (TCPClientConnection.Connected)
                    {
                        NetworkStream stream = TCPClientConnection.GetStream();

                        stream.Write(messageToTransmit, 0, messageToTransmitLength);

                        if (ExpectingAcknowledgement)
                        {
                            // Buffer to store the response bytes.
                            Byte[] responseDataInBytes = new Byte[512];

                            // String to store the response ASCII representation.
                            String responseData = String.Empty;

                            stream.ReadTimeout = 2000;
                            // Read the first batch of the TcpServer response bytes.
                            Int32 bytes = stream.Read(responseDataInBytes, 0, responseDataInBytes.Length);
                            responseData = Encoding.ASCII.GetString(responseDataInBytes, 0, bytes);

                            stream.Dispose();
                            TCPClientConnection.Dispose();

                            return responseData;
                        }
                        else
                        {
                            stream.Dispose();
                            TCPClientConnection.Dispose();

                            return null;
                        }
                    }
                }
                catch (Exception TCPClientConnectionException)
                {
                    Debug.Print("Excpetion raised in: TCPCommunications.cs -> SendDataOnTCP -> TCPClientConnection.ConnectAsync. Exception Message: " + TCPClientConnectionException.Message);
                    return null;
                }
            }
            catch (Exception SendDataOnTCPException)
            {
                Debug.WriteLine("Excpetion raised in: TCPCommunications.cs -> SendDataOnTCP. Exception Message: " + SendDataOnTCPException.Message);
                return null;
            }

            return null;
        }

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var CountUpdatePacket = new List<byte>(); //Here, I initialised as list because arrays needs a fixed size, while lists can be of unknown size.
            UInt16 totalNumberOfCommands = 0x00;
            UInt16 currentCommandLength = 0x00;
            int currentCommandLengthIndex = 0x00;

            #region Header
            //Start of the content update packet - 0xA5C1
            CountUpdatePacket.Add(0xA5);
            CountUpdatePacket.Add(0xC1);

            //CRC
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);

            //Total packet length
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);

            //Current retry count
            CountUpdatePacket.Add(0x00);

            //Total number of commands
            CountUpdatePacket.Add(0x01);

            //Reserved
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);

            #endregion

            #region Global Parameters
            /***** GLOBAL PARAMETERS START *****/

            //Resetting the current command length.
            currentCommandLength = 0x00;

            //Commnad ID of Global Parameters is 0x01
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x01);

            //We'll use this value to update the CMD_LEN after we finish creating this command.
            currentCommandLengthIndex = CountUpdatePacket.Count;

            //Command Length
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);

            //Display Brightness
            CountUpdatePacket.Add((byte)(Brightness.Value)); // Portrait/Landscape
            currentCommandLength += 1;

            //Update Colour
            CountUpdatePacket.Add(0x01);
            currentCommandLength += 1;

            //Update the command Length
            CountUpdatePacket[currentCommandLengthIndex] = (byte)(currentCommandLength >> 8);
            currentCommandLengthIndex += 1; //Point to the next byte. CMD_LEN is 2 byte wide.
            CountUpdatePacket[currentCommandLengthIndex] = (byte)(currentCommandLength & 0xFF);

            //Increment number of commands in the packet.
            totalNumberOfCommands += 1;

            /***** GLOBAL PARAMETERS END *****/
            #endregion

            #region Send Data

            /***** ZONE CONTENT PARAMETERS START *****/

            //Resetting the current command length.
            currentCommandLength = 0x00;

            //Commnad ID of Timeout Parameters is 0x02
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x02);

            //We'll use this value to update the CMD_LEN after we finish creating this command.
            currentCommandLengthIndex = CountUpdatePacket.Count;

            //Command Length
            CountUpdatePacket.Add(0x00);
            CountUpdatePacket.Add(0x00);

            //Serial ID
            CountUpdatePacket.Add((byte)((byte)SerialID.Value >> 8));
            currentCommandLength += 1;
            CountUpdatePacket.Add((byte)((byte)SerialID.Value & 0xFF));
            currentCommandLength += 1;

            //Foreground Colour Red
            CountUpdatePacket.Add(FGColourPicker.Color.R);
            currentCommandLength += 1;
            //Foreground Colour Green
            CountUpdatePacket.Add(FGColourPicker.Color.G);
            currentCommandLength += 1;
            //Foreground Colour Blue
            CountUpdatePacket.Add(FGColourPicker.Color.B);
            currentCommandLength += 1;

            //Background Colour Red
            CountUpdatePacket.Add(BGColourPicker.Color.R);
            currentCommandLength += 1;
            //Background Colour Green
            CountUpdatePacket.Add(BGColourPicker.Color.G);
            currentCommandLength += 1;
            //Background Colour Blue
            CountUpdatePacket.Add(BGColourPicker.Color.B);
            currentCommandLength += 1;

            UInt16 verticalAlignment = 0x00;
            UInt16 horizontalAlignment = 0x00;

            //Vertical Alignment
            switch (VerticalAlignmentCombobox.SelectedItem)
            {
                case "Default":
                    verticalAlignment = 0x0F;
                    break;
                case "Top":
                    verticalAlignment = 0x01;
                    break;
                case "Bottom":
                    verticalAlignment = 0x02;
                    break;
                default:
                    verticalAlignment = 0x00;
                    break;
            }

            //Horzontal Alignment
            switch (HorizontalAlignmentCombobox.SelectedItem)
            {
                case "Default":
                    horizontalAlignment = 0x0F;
                    break;
                case "Left":
                    horizontalAlignment = 0x01;
                    break;
                case "Right":
                    horizontalAlignment = 0x02;
                    break;
                default:
                    horizontalAlignment = 0x00;
                    break;
            }

            //Alignment
            CountUpdatePacket.Add((byte)((verticalAlignment << 4) | (horizontalAlignment)));
            currentCommandLength += 1;

            //Font ID
            switch (FontCombobox.SelectedItem)
            {
                case "5x5":
                    CountUpdatePacket.Add(0x30);
                    currentCommandLength += 1;
                    break;
                case "6x7":
                    CountUpdatePacket.Add(0x31);
                    currentCommandLength += 1;
                    break;
                case "8x14":
                    CountUpdatePacket.Add(0x32);
                    currentCommandLength += 1;
                    break;
                case "9x11":
                    CountUpdatePacket.Add(0x3A);
                    currentCommandLength += 1;
                    break;
                case "9x15":
                    CountUpdatePacket.Add(0x33);
                    currentCommandLength += 1;
                    break;
                case "9x16":
                    CountUpdatePacket.Add(0x34);
                    currentCommandLength += 1;
                    break;
                case "10x26":
                    CountUpdatePacket.Add(0x3C);
                    currentCommandLength += 1;
                    break;
                case "16x26":
                    CountUpdatePacket.Add(0x36);
                    currentCommandLength += 1;
                    break;
                case "16x32":
                    CountUpdatePacket.Add(0x38);
                    currentCommandLength += 1;
                    break;
                default:
                    break;
            }

            //Appear Animation
            switch (AppearAnimationCombobox.SelectedItem)
            {
                case "No Animation":
                    CountUpdatePacket.Add(0xFF);
                    currentCommandLength += 1;
                    break;
                case "Jump In":
                    CountUpdatePacket.Add(0x00);
                    currentCommandLength += 1;
                    break;
                case "Move Left":
                    CountUpdatePacket.Add(0x01);
                    currentCommandLength += 1;
                    break;
                case "Move Right":
                    CountUpdatePacket.Add(0x02);
                    currentCommandLength += 1;
                    break;
                case "Move Up":
                    CountUpdatePacket.Add(0x03);
                    currentCommandLength += 1;
                    break;
                case "Move Down":
                    CountUpdatePacket.Add(0x04);
                    currentCommandLength += 1;
                    break;
                case "Drag Left":
                    CountUpdatePacket.Add(0x05);
                    currentCommandLength += 1;
                    break;
                case "Drag Right":
                    CountUpdatePacket.Add(0x06);
                    currentCommandLength += 1;
                    break;
                case "Drag Up":
                    CountUpdatePacket.Add(0x07);
                    currentCommandLength += 1;
                    break;
                case "Drag Down":
                    CountUpdatePacket.Add(0x08);
                    currentCommandLength += 1;
                    break;
                default:
                    CountUpdatePacket.Add(0xFF);
                    currentCommandLength += 1;
                    break;
            }

            //Clear Animation
            switch (ClearAnimationCombobox.SelectedItem)
            {
                case "No Animation":
                    CountUpdatePacket.Add(0xFF);
                    currentCommandLength += 1;
                    break;
                case "Jump In":
                    CountUpdatePacket.Add(0x00);
                    currentCommandLength += 1;
                    break;
                case "Move Left":
                    CountUpdatePacket.Add(0x01);
                    currentCommandLength += 1;
                    break;
                case "Move Right":
                    CountUpdatePacket.Add(0x02);
                    currentCommandLength += 1;
                    break;
                case "Move Up":
                    CountUpdatePacket.Add(0x03);
                    currentCommandLength += 1;
                    break;
                case "Move Down":
                    CountUpdatePacket.Add(0x04);
                    currentCommandLength += 1;
                    break;
                case "Drag Left":
                    CountUpdatePacket.Add(0x05);
                    currentCommandLength += 1;
                    break;
                case "Drag Right":
                    CountUpdatePacket.Add(0x06);
                    currentCommandLength += 1;
                    break;
                case "Drag Up":
                    CountUpdatePacket.Add(0x07);
                    currentCommandLength += 1;
                    break;
                case "Drag Down":
                    CountUpdatePacket.Add(0x08);
                    currentCommandLength += 1;
                    break;
                default:
                    CountUpdatePacket.Add(0xFF);
                    currentCommandLength += 1;
                    break;
            }

            //Animation Speed
            CountUpdatePacket.Add(0x00);
            currentCommandLength += 1;
            CountUpdatePacket.Add(0x01);
            currentCommandLength += 1;

            #region Content Type Text
            //Zone Type - 0x00: ASCII, 0x01: Symbol, 0x02: Number
            CountUpdatePacket.Add(0x00);
            currentCommandLength += 1;

            string messageToTransmit = Content.Text;

            //Content Length
            CountUpdatePacket.Add((byte)Content.Text.Length);
            currentCommandLength += 1;

            byte[] messageToTransmitBytes = Encoding.ASCII.GetBytes(messageToTransmit);

            foreach (var currentByte in messageToTransmitBytes)
            {
                CountUpdatePacket.Add(currentByte);
                currentCommandLength += 1;
            }

            #endregion

            #region Content Type Symbol
            ////Zone Type - 0x00: ASCII, 0x01: Symbol, 0x02: Number
            //CountUpdatePacket.Add(0x01);
            //currentCommandLength += 1;

            ////Content Length
            //CountUpdatePacket.Add(0x01);
            //currentCommandLength += 1;

            ////Content
            //CountUpdatePacket.Add(0x01); // Portrait/Landscape
            //currentCommandLength += 1;
            #endregion

            //Update the command Length
            CountUpdatePacket[currentCommandLengthIndex] = (byte)(currentCommandLength >> 8);
            currentCommandLengthIndex += 1; //Point to the next byte. CMD_LEN is 2 byte wide.
            CountUpdatePacket[currentCommandLengthIndex] = (byte)(currentCommandLength & 0xFF);

            //Increment number of commands in the packet.
            totalNumberOfCommands += 1;

            /***** ZONE CONTENT PARAMETERS END *****/

            #region Update Header
            //Updating the header
            //Updating the packet length
            CountUpdatePacket[4] = (byte)(CountUpdatePacket.Count >> 8);
            CountUpdatePacket[5] = (byte)(CountUpdatePacket.Count & 0xFF);

            //Updating #Commands in this packet - In header
            CountUpdatePacket[7] = (byte)totalNumberOfCommands;

            //Calculate Checksum
            UInt16 CRCOfPacket = calculateChecksum(CountUpdatePacket, 4);

            CountUpdatePacket[2] = (byte)(CRCOfPacket >> 8);
            CountUpdatePacket[3] = (byte)(CRCOfPacket & 0xFF);
            #endregion

            var testingMessageAcknowledgement = SendDataOnTCP(CountUpdatePacket.ToArray(), CountUpdatePacket.Count, SignIPAddress.Text, 9520, true);

            if (testingMessageAcknowledgement != null && testingMessageAcknowledgement.Length > 0)
            {
            }

            #endregion
        }
    }
}
