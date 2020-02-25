using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Input.Preview;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI;

namespace GazeTalker
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private GazeInputSourcePreview gazeInputSource;
        private GazeDeviceWatcherPreview gazeDeviceWatcher;
        private UIElement focusElement;
        private SpeechSynthesizer synth = new SpeechSynthesizer();

        DispatcherTimer timerGaze = new DispatcherTimer();
        bool timerStarted = false;


        char[,] hiragana = {
                { 'あ','い','う','え','お'},
                { 'か','き','く','け','こ'},
                { 'さ','し','す','せ','そ'},
                { 'た','ち','つ','て','と'},
                { 'な','に','ぬ','ね','の'},
                { 'は','ひ','ふ','へ','ほ'},
                { 'ま','み','む','め','も'},
                { 'や','ゆ','よ','※','※'},
                { 'ら','り','る','れ','ろ'},
                { 'わ','を','ん','※','※'},
                { '濁','ー','　','←','話'},
            };
        String[] dakuten = {
            "あぁ", "いぃ", "うぅ", "えぇ", "おぉ",
            "かが", "きぎ", "くぐ", "けげ", "こご",
            "さざ", "しじ", "すず", "せぜ", "そぞ",
            "ただ", "ちぢ", "つづっ", "てで", "とど",
            "はばぱ", "ひびぴ", "ふぶぷ", "へべぺ", "ほぼぽ",
            "やゃ", "ゆゅ", "よょ"
        };

        /// <summary>
        /// Initialize the app.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            for (int x = 0; x < 11; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    Button b = new Button();
                    b.Click += ButtonClicked;
                    b.FontSize = 100;
                    b.Width = 150;
                    char c = hiragana[x, y];
                    if (c == '※') {
                        b.Visibility = Visibility.Collapsed;
                        b.IsEnabled = false;
                    }
                    else
                    {
                        b.Content = c;
                    }

                    if (c == '濁' || c == 'ー' || c == '　' || c == '←' || c == '話')
                    {
                        b.Background = new SolidColorBrush(Colors.Green);
                    }

                    LettersArea.Children.Add(b);
                    Grid.SetRow(b, y);
                    Grid.SetColumn(b, x);
                }
            }
        }

        private void ButtonClicked(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            switch (b.Content)
            {
                case '濁':
                    handleDakuten();
                    break;

                case '←':
                    String s = InputField.Text;
                    if (s.Length > 0)
                    {
                        InputField.Text = s.Substring(0, s.Length - 1);
                    }
                    break;

                case '話':
                    Speak(InputField.Text);
                    InputField.Text = "";
                    break;

                default:
                    InputField.Text += b.Content;
                    break;
            }
        }

        private void handleDakuten()
        {
            String s = InputField.Text;
            if (s.Length == 0)
            {
                return;
            }
            String chr = s.Substring(s.Length - 1);
            String str = s.Substring(0, s.Length - 1);
            for (int i = 0; i < dakuten.Length; i++)
            {
                String choices = dakuten[i];
                int j = choices.IndexOf(chr);
                if (j >= 0)
                {
                    j = (j + 1) % choices.Length;
                    InputField.Text = str + choices[j];
                    return;
                }
            }
        }

        private async void Speak(String text)
        {
            SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);
            Speech.SetSource(stream, stream.ContentType);
            Speech.Play();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            StartGazeDeviceWatcher();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            StopGazeDeviceWatcher();
        }

        /// <summary>
        /// Start gaze watcher and declare watcher event handlers.
        /// </summary>
        private void StartGazeDeviceWatcher()
        {
            if (gazeDeviceWatcher == null)
            {
                gazeDeviceWatcher = GazeInputSourcePreview.CreateWatcher();
                gazeDeviceWatcher.Added += this.DeviceAdded;
                gazeDeviceWatcher.Updated += this.DeviceUpdated;
                gazeDeviceWatcher.Start();
            }
        }

        /// <summary>
        /// Shut down gaze watcher and stop listening for events.
        /// </summary>
        private void StopGazeDeviceWatcher()
        {
            if (gazeDeviceWatcher != null)
            {
                gazeDeviceWatcher.Stop();
                gazeDeviceWatcher.Added -= this.DeviceAdded;
                gazeDeviceWatcher.Updated -= this.DeviceUpdated;
                gazeDeviceWatcher = null;
            }
        }


        /// <summary>
        /// Eye-tracking device connected (added, or available when watcher is initialized).
        /// </summary>
        /// <param name="sender">Source of the device added event</param>
        /// <param name="e">Event args for the device added event</param>
        private void DeviceAdded(GazeDeviceWatcherPreview source,
            GazeDeviceWatcherAddedPreviewEventArgs args)
        {
            // Set up gaze tracking.
            TryEnableGazeTrackingAsync(args.Device);
        }

        /// <summary>
        /// Initial device state might be uncalibrated, 
        /// but device was subsequently calibrated.
        /// </summary>
        /// <param name="sender">Source of the device updated event</param>
        /// <param name="e">Event args for the device updated event</param>
        private void DeviceUpdated(GazeDeviceWatcherPreview source,
            GazeDeviceWatcherUpdatedPreviewEventArgs args)
        {
            // Set up gaze tracking.
            TryEnableGazeTrackingAsync(args.Device);
        }

        private void TimerGaze_Tick(object sender, object e)
        {
            // Increment progress bar.
            GazeRadialProgressBar.Value += 1;

            // If progress bar reaches maximum value, reset and relocate.
            if (GazeRadialProgressBar.Value >= 100)
            {
                //SetGazeTargetLocation();
                Button b = focusElement as Button;

                if (b != null)
                {
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(b);
                    peer.Invoke();
                }
                GazeRadialProgressBar.Value = 0;
            }
        }

        /// <summary>
        /// GazeEntered handler.
        /// </summary>
        /// <param name="sender">Source of the gaze entered event</param>
        /// <param name="e">Event args for the gaze entered event</param>
        private void GazeEntered(
            GazeInputSourcePreview sender,
            GazeEnteredPreviewEventArgs args)
        {
            // Show ellipse representing gaze point.
            eyeGazePositionEllipse.Visibility = Visibility.Visible;

            // Mark the event handled.
            args.Handled = true;
        }

        /// <summary>
        /// GazeExited handler.
        /// Call DisplayRequest.RequestRelease to conclude the 
        /// RequestActive called in GazeEntered.
        /// </summary>
        /// <param name="sender">Source of the gaze exited event</param>
        /// <param name="e">Event args for the gaze exited event</param>
        private void GazeExited(
            GazeInputSourcePreview sender,
            GazeExitedPreviewEventArgs args)
        {
            // Hide gaze tracking ellipse.
            eyeGazePositionEllipse.Visibility = Visibility.Collapsed;

            // Mark the event handled.
            args.Handled = true;
        }

        /// <summary>
        /// GazeMoved handler translates the ellipse on the canvas to reflect gaze point.
        /// </summary>
        /// <param name="sender">Source of the gaze moved event</param>
        /// <param name="e">Event args for the gaze moved event</param>
        private void GazeMoved(GazeInputSourcePreview sender, GazeMovedPreviewEventArgs args)
        {
            // Update the position of the ellipse corresponding to gaze point.
            if (args.CurrentPoint.EyeGazePosition != null)
            {
                double gazePointX = args.CurrentPoint.EyeGazePosition.Value.X;
                double gazePointY = args.CurrentPoint.EyeGazePosition.Value.Y;

                double ellipseLeft = gazePointX - (eyeGazePositionEllipse.Width / 2.0f);
                double ellipseTop = gazePointY - (eyeGazePositionEllipse.Height / 2.0f);

                // Translate transform for moving gaze ellipse.
                TranslateTransform translateEllipse = new TranslateTransform
                {
                    X = ellipseLeft,
                    Y = ellipseTop
                };

                eyeGazePositionEllipse.RenderTransform = translateEllipse;

                // The gaze point screen location.
                Point gazePoint = new Point(gazePointX, gazePointY);

                UIElement element = GetButtonAtGaze(gazePoint, LettersArea);
                if (element != focusElement)
                {
                    timerGaze.Stop();
                    GazeRadialProgressBar.Value = 0;
                    timerStarted = false;

                    focusElement = element;
                    if (focusElement != null)
                    {
                        MoveGazeRadialProgressBar(focusElement, LettersArea);
                    }
                }

                // Basic hit test to determine if gaze point is on progress bar.
                bool hitRadialProgressBar =
                    DoesElementContainPoint(
                        gazePoint,
                        GazeRadialProgressBar.Name,
                        GazeRadialProgressBar);

                // Use progress bar thickness for visual feedback.
                if (hitRadialProgressBar)
                {
                    GazeRadialProgressBar.Thickness = 10;
                }
                else
                {
                    GazeRadialProgressBar.Thickness = 4;
                }
                // Mark the event handled.
                args.Handled = true;
            }
        }

        private UIElement GetButtonAtGaze(Point gazePoint, UIElement rootElement)
        {
            IEnumerable<UIElement> elementStack =
              VisualTreeHelper.FindElementsInHostCoordinates(gazePoint, rootElement, true);
            foreach (UIElement item in elementStack)
            {
                if (item is Button)
                {
                    return item;
                }
            }
            return null;
        }

        private void MoveGazeRadialProgressBar(UIElement element, UIElement rootElement)
        {
            GeneralTransform gt = element.TransformToVisual(rootElement);
            TranslateTransform translateTarget = new TranslateTransform();
            Point screenPoint;
            screenPoint = gt.TransformPoint(new Point(-50, -50));
            translateTarget.X = screenPoint.X;
            translateTarget.Y = screenPoint.Y;

            GazeRadialProgressBar.RenderTransform = translateTarget;
            GazeRadialProgressBar.Visibility = Visibility.Visible;
            GazeRadialProgressBar.Value = 0;
        }

        /// <summary>
        /// Return whether the gaze point is over the progress bar.
        /// </summary>
        /// <param name="gazePoint">The gaze point screen location</param>
        /// <param name="elementName">The progress bar name</param>
        /// <param name="uiElement">The progress bar UI element</param>
        /// <returns></returns>
        private bool DoesElementContainPoint(
            Point gazePoint, string elementName, UIElement uiElement)
        {
            // Use entire visual tree of progress bar.
            IEnumerable<UIElement> elementStack =
              VisualTreeHelper.FindElementsInHostCoordinates(gazePoint, uiElement, true);
            foreach (UIElement item in elementStack)
            {
                //Cast to FrameworkElement and get element name.
                if (item is FrameworkElement feItem)
                {
                    if (feItem.Name.Equals(elementName))
                    {
                        if (!timerStarted)
                        {
                            // Start gaze timer if gaze over element.
                            timerGaze.Start();
                            timerStarted = true;
                        }
                        return true;
                    }
                }
            }

            // Stop gaze timer and reset progress bar if gaze leaves element.
            timerGaze.Stop();
            GazeRadialProgressBar.Value = 0;
            timerStarted = false;
            return false;
        }

        /// <summary>
        /// Initialize gaze tracking.
        /// </summary>
        /// <param name="gazeDevice"></param>
        private async void TryEnableGazeTrackingAsync(GazeDevicePreview gazeDevice)
        {
            // If eye-tracking device is ready, declare event handlers and start tracking.
            if (IsSupportedDevice(gazeDevice))
            {
                timerGaze.Interval = new TimeSpan(0, 0, 0, 0, 20);
                timerGaze.Tick += TimerGaze_Tick;

                //SetGazeTargetLocation();

                // This must be called from the UI thread.
                gazeInputSource = GazeInputSourcePreview.GetForCurrentView();

                gazeInputSource.GazeEntered += GazeEntered;
                gazeInputSource.GazeMoved += GazeMoved;
                gazeInputSource.GazeExited += GazeExited;
            }
            // Notify if device calibration required.
            else if (gazeDevice.ConfigurationState ==
                     GazeDeviceConfigurationStatePreview.UserCalibrationNeeded ||
                     gazeDevice.ConfigurationState ==
                     GazeDeviceConfigurationStatePreview.ScreenSetupNeeded)
            {
                // Device isn't calibrated, so invoke the calibration handler.
                System.Diagnostics.Debug.WriteLine("Your device needs to calibrate. Please wait for it to finish.");
                await gazeDevice.RequestCalibrationAsync();
            }
            // Notify if device calibration underway.
            else if (gazeDevice.ConfigurationState ==
                GazeDeviceConfigurationStatePreview.Configuring)
            {
                // Device is currently undergoing calibration.  
                // A device update is sent when calibration complete.
                System.Diagnostics.Debug.WriteLine("Your device is being configured. Please wait for it to finish");
            }
            // Device is not viable.
            else if (gazeDevice.ConfigurationState == GazeDeviceConfigurationStatePreview.Unknown)
            {
                // Notify if device is in unknown state.  
                // Reconfigure/recalbirate the device.  
                System.Diagnostics.Debug.WriteLine("Your device is not ready. Please set up your device or reconfigure it.");
            }
        }

        private bool IsSupportedDevice(GazeDevicePreview gazeDevice)
        {
            return (gazeDevice.CanTrackEyes &&
                     gazeDevice.ConfigurationState ==
                     GazeDeviceConfigurationStatePreview.Ready);
        }
    }
}
