using System;
using Gst;

namespace gstream_netcore_console
{
    class Program
    {
        static void Main(string[] args)
        {
            Application.Init(ref args);

            var pipeline = Parse.Launch("playbin uri=http://mirrors.standaloneinstaller.com/video-sample/jellyfish-25-mbps-hd-hevc.mp4");
            //var pipeline = Parse.Launch("udpsrc port=5004 !  application/x-rtp, encoding-name=JPEG,payload=26 !  rtpjpegdepay ! jpegdec ! timeoverlay halignment=2 ! autovideosink");

            pipeline.SetState(State.Playing);

            var bus = pipeline.Bus;
            var msg = bus.TimedPopFiltered(Constants.CLOCK_TIME_NONE, MessageType.Eos | MessageType.Error);
            pipeline.SetState(State.Null);
        }
    }
}
