using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using RealtimeFramework.Messaging;
using Newtonsoft.Json;
using System.Diagnostics;
using Windows.Media.SpeechRecognition;

namespace UWPChat
{
    public class Message
    {
        public string id { get; set; }
        public string text { get; set; }
        public string sentAt { get; set; }
    }

    public sealed partial class MainPage : Page
    {
        private OrtcClient ortcClient;
        private string myID = "ID_" + Windows.Security.Cryptography.CryptographicBuffer.GenerateRandomNumber();

        public MainPage()
        {
            this.InitializeComponent();

            // Establish the Realtime connection
            ortcClient = new RealtimeFramework.Messaging.OrtcClient();
            ortcClient.OnConnected += OnConnected;
            ortcClient.OnException += OnException;

            ortcClient.ClusterUrl = "http://ortc-developers.realtime.co/server/2.1/";
            Log("Connecting to a Realtime server ...");
            ortcClient.Connect("2Ze1dz", "token");
        }

        void OnConnected(object sender)
        {
            Log("Connected to Realtime\n", true);

            // Subscribe the Realtime channel
            ortcClient.Subscribe("chat", true, OnMessageCallback);
        }

        void OnException(object sender, Exception ex)
        {
            Log(ex.Message, true);
        }

        private void OnMessageCallback(object sender, string channel, string message)
        {
            Debug.WriteLine("Received message: " + message);

            Message parsedMessage = JsonConvert.DeserializeObject<Message>(message);
            Messages.Text = parsedMessage.text + "\n" + Messages.Text;

            // check if message is from another user
            if(!parsedMessage.id.Equals(myID))
            {
                // Say the message
                Speak(parsedMessage.text);
            }
        }

        private void Log(string text, bool speakIt = false)
        {
            Messages.Text = text;
            if(speakIt)
            {
                Speak(text);
            }
        }

        private async void Speak(string text)
        {
            MediaElement mediaElement = new MediaElement();
            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            Windows.Media.SpeechSynthesis.SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);
            mediaElement.SetSource(stream, stream.ContentType);
            mediaElement.Play();
        }

        private async void SpokenMessage_Click(object sender, RoutedEventArgs e)
        {
            SpeechRecognizer speechRecognizer = new SpeechRecognizer();
            await speechRecognizer.CompileConstraintsAsync();
            speechRecognizer.UIOptions.AudiblePrompt = "Say the message you want to send ...";
            string spokenMessage = "";

            // Start recognition.
            try
            {
                SpeechRecognitionResult speechRecognitionResult = await speechRecognizer.RecognizeWithUIAsync();
                spokenMessage = speechRecognitionResult.Text;
            }
            catch (System.Runtime.InteropServices.COMException exc) when (exc.HResult == unchecked((int)0x80045509))
            //privacyPolicyHResult
            //The speech privacy policy was not accepted prior to attempting a speech recognition.
            {
                ContentDialog Dialog = new ContentDialog()
                {
                    Title = "The speech privacy policy was not accepted",
                    Content = "You need to turn on a button called 'Get to know me'...",
                    PrimaryButtonText = "Nevermind",
                    SecondaryButtonText = "Show me the setting"
                };

                if (await Dialog.ShowAsync() == ContentDialogResult.Secondary)
                {
                    string uriToLaunch = "ms-settings:privacy-speechtyping";
                    Uri uri = new Uri(uriToLaunch);

                    bool success = await Windows.System.Launcher.LaunchUriAsync(uri);

                    if (!success) await new ContentDialog
                    {
                        Title = "Oops! Something went wrong...",
                        Content = "The settings app could not be opened.",
                        PrimaryButtonText = "Nevermind!"
                    }.ShowAsync();
                }
            }

            if (spokenMessage != "")
            {
                // Send the recognition result text as a Realtime message
                Message message = new Message();
                message.id = myID;
                message.text = spokenMessage;
                message.sentAt = DateTime.Now.ToLocalTime().ToString();

                string jsonMessage = JsonConvert.SerializeObject(message);
                Debug.WriteLine("Sending message: " + jsonMessage);
                ortcClient.Send("chat", jsonMessage);
            }
        }
    }
}
