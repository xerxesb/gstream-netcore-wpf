using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GLib;
using Gst;
using Gst.Video;
using Application = System.Windows.Application;
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
        private Element _playbin;
        private VideoOverlayAdapter _adapter;
        private bool _isRender;
        private (int x, int y, int w, int h) _videoRect;

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
            
            VideoPanel.SizeChanged += VideoPanelOnSizeChanged;
        }

        protected override void OnClosed(EventArgs e)
        {
            var x = 1;
            base.OnClosed(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            _windowHandle = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            base.OnActivated(e);
        }

        private void InitGStreamerPipeline()
        {
            _playbin = ElementFactory.Make("playbin", "playbin");
            _playbin["uri"] = "http://mirrors.standaloneinstaller.com/video-sample/jellyfish-25-mbps-hd-hevc.mp4";
            
            _playbin.Connect("video-tags-changed", TagsCb);
            _playbin.Connect("audio-tags-changed", TagsCb);
            _playbin.Connect("tags-tags-changed", TagsCb);

            var bus = _playbin.Bus;
            bus.AddSignalWatch();
            bus.EnableSyncMessageEmission();
            bus.SyncMessage += OnBusSyncMessage;
            bus.Connect(BusMessageError, ErrorCb);
            bus.Connect(BusMessageEos, EosCb);
            bus.Connect(BusMessageStateChanged, StateChangedCb);
            bus.Connect(BusMessageApplication, ApplicationCb);

            GLib.Timeout.Add(1, RefreshUI);
        }

        private void OnBusSyncMessage(object o, SyncMessageArgs sargs)
        {
            // var bus = (Bus) o;
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
            catch { }

            var overlay = (src as Gst.Bin)?.GetByInterface(VideoOverlayAdapter.GType);
            if (overlay == null)
                return;
            
            _adapter = new VideoOverlayAdapter(overlay.Handle);
            _adapter.WindowHandle = _windowHandle;
            _adapter.SetRenderRectangle(_videoRect.x, _videoRect.y, _videoRect.w, _videoRect.h);
            _adapter.HandleEvents(true);
            _isRender = true;
        }

        private void VideoPanelOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Size size = new Size(this.Width, this.Height);
            var p = VideoPanel.TransformToAncestor(this).Transform(new Point(0, 0));
            var s = VideoPanel.RenderSize;
            _videoRect = ((int) p.X, (int) p.Y, (int) s.Width, (int) s.Height);
            if (_adapter != null)
                _adapter.SetRenderRectangle(_videoRect.x, _videoRect.y, _videoRect.w, _videoRect.h);
        }

        private bool RefreshUI()
        {
            return true;
        }

        private void TagsCb(object o, SignalArgs args)
        {
        }
        
        private void ApplicationCb(object o, SignalArgs args)
        {
        }

        private void StateChangedCb(object o, SignalArgs args)
        {
        }

        private void EosCb(object o, SignalArgs args)
        {
        }

        private void ErrorCb(object o, SignalArgs args)
        {
        }

        private void PlayClicked(object sender, RoutedEventArgs e)
        {
            var state = _playbin.SetState(Gst.State.Playing);
            Console.WriteLine(state.ToString());
        }

        private void PauseClicked(object sender, RoutedEventArgs e)
        {
            var state = _playbin.SetState(Gst.State.Paused);
            Console.WriteLine(state.ToString());
        }

        private void StopClicked(object sender, RoutedEventArgs e)
        {
            var state = _playbin.SetState(Gst.State.Ready);
            Console.WriteLine(state.ToString());
        }
    }
}