using System;
using System.Windows;
using GLib;
using Gst;
using Gst.App;
using Gst.Video;
using Thread = System.Threading.Thread;

namespace gst_wpf_cs
{
    /// <summary>
    /// Interaction logic for MainWPlayClicked    /// </summary>
    public partial class MainWindow : Window
    {
        private MainLoop _mainLoop;
        private Thread _mainGLibThread;
        private IntPtr _windowHandle;
        private VideoOverlayAdapter _adapter;
        private Pipeline _pipe;

        private const string BusMessageError = "message::error";
        private const string BusMessageEos = "message::eos";
        private const string BusMessageStateChanged = "message::state-changed";
        private const string BusMessageApplication = "message::application";

        public MainWindow()
        {
            InitializeComponent();
            
            Gst.Application.Init();
            GtkSharp.GstreamerSharp.ObjectManager.Initialize();
            _mainLoop = new GLib.MainLoop();
            _mainGLibThread = new System.Threading.Thread(_mainLoop.Run);
            _mainGLibThread.Start();

            InitGStreamerPipeline();
        }

        protected override void OnClosed(EventArgs e)
        {
            _pipe.SetState(State.Null);
            _pipe.Dispose();
            _mainLoop.Quit();
            base.OnClosed(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            _windowHandle = VideoPanel.Handle;
            base.OnActivated(e);
        }

        private void InitGStreamerPipeline()
        {
            _pipe = (Pipeline) Parse.Launch(
                "udpsrc port=5004 !  application/x-rtp, encoding-name=JPEG,payload=26 !  rtpjpegdepay ! jpegdec ! timeoverlay halignment=2 ! autovideosink"); 
            
            _pipe.Connect("video-tags-changed", TagsCb);
            _pipe.Connect("audio-tags-changed", TagsCb);
            _pipe.Connect("text-tags-changed", TagsCb);

            var bus = _pipe.Bus;
            bus.AddSignalWatch();
            bus.EnableSyncMessageEmission();
            bus.SyncMessage += OnBusSyncMessage;
            bus.Connect(BusMessageError, ErrorCb);
            bus.Connect(BusMessageEos, EosCb);
            bus.Connect(BusMessageStateChanged, StateChangedCb);
            bus.Connect(BusMessageApplication, ApplicationCb);

            GLib.Timeout.Add(1000, RefreshPlaybinInfo);
        }

        private void OnBusSyncMessage(object o, SyncMessageArgs sargs)
        {
            var message = sargs.Message;
            
            if (!Gst.Video.Global.IsVideoOverlayPrepareWindowHandleMessage(message))
                return;

            var src = message.Src as Element;
            if (src == null)
                return;

            try
            {
                src["force-aspect-ratio"] = true;
            }
            catch { /* ignored */ }

            _adapter = new VideoOverlayAdapter(src.Handle);
            _adapter.WindowHandle = _windowHandle;
            _adapter.HandleEvents(true);
        }

        private bool RefreshPlaybinInfo()
        {
            _pipe.QueryDuration(Format.Time, out var durationTime);
            _pipe.QueryPosition(Format.Time, out var pos);

            var stateChangeReturn = _pipe.GetState(out var state, out var pending, 100);
            
            UpdateUI(
                pos / Gst.Constants.SECOND,
                durationTime / Gst.Constants.SECOND,
                 state 
            );
            
            return true;
        }

        private void UpdateUI(long currTime, long totTime, State state)
        {
            Dispatcher.Invoke(() =>
            {
                DurationLabel.Content = $"Duration: {currTime}/{totTime} sec";
                PlaybinStateLabel.Content = $"State [{(state == State.Null ? "Uninitialised" : state.ToString())}]";
            });
        }

        private void TagsCb(object o, SignalArgs args)
        {
            var playbin = (Gst.Element)o;
            var s = new Structure("tags-changed");
            playbin.PostMessage(Gst.Message.NewApplication(playbin, s));
        }
        
        private void ApplicationCb(object o, SignalArgs args)
        {
            var msg = (Gst.Message) args.Args[0];
            if (msg.Structure.Name == "tags-changed")
            {
                // Handle re-analysis of the streams
                // See https://github.com/ttustonic/GStreamerSharpSamples/blob/master/WpfSamples/BasicTutorial05.xaml.cs#L173
                // This may be required when we want to broadcast and select between multiple video streams from the same source
            }
        }

        private void StateChangedCb(object o, SignalArgs args)
        {
            var msg = (Gst.Message) args.Args[0];
            msg.ParseStateChanged(out var oldstate, out var newstate, out var pending);
        }

        private void EosCb(object o, SignalArgs args)
        {
            Console.WriteLine("End of stream");
            _pipe.SetState(State.Ready);
        }

        private void ErrorCb(object o, SignalArgs args)
        {
            var msg = (Gst.Message) args.Args[0];
            msg.ParseError(out var exc, out var debug);
            
            Console.WriteLine($"Error received from element {msg.Src}: {exc.Message}");
            Console.WriteLine($"Debug info: {debug ?? "None"}");

            _pipe.SetState(State.Ready);
        }

        private void PlayClicked(object sender, RoutedEventArgs e)
        {
            var state = _pipe.SetState(Gst.State.Playing);
            Console.WriteLine(state.ToString());
        }

        private void PauseClicked(object sender, RoutedEventArgs e)
        {
            var state = _pipe.SetState(Gst.State.Paused);
            Console.WriteLine(state.ToString());
        }

        private void StopClicked(object sender, RoutedEventArgs e)
        {
            var state = _pipe.SetState(Gst.State.Ready);
            Console.WriteLine(state.ToString());
        }
    }
}