using FFMpegEncoder.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegEncoder.Infrastructure.Metrics
{
    public static class FFMpegProgressMeter
    {

        internal static readonly Meter Meter = new Meter("FFMpegProgress1", "1.0.0");


        internal static ObservableGauge<double>? FFmepgProgressTime;
        public static void RegisterFFmepgProgressTimeObserve(Func<double> observeValue)
        {
            FFmepgProgressTime = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-time", observeValue, "ms");
        }
        
        internal static ObservableGauge<double>? FFmepgProgressFps;
        public static void RegisterFFmepgProgressFpsObserve(Func<double> observeValue)
        {
            FFmepgProgressFps = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-fps", observeValue, "fps");
        }
        
        internal static ObservableGauge<Int64>? FFmepgProgressFrame;
        public static void RegisterFFmepgProgressFrameObserve(Func<Int64> observeValue)
        {
            FFmepgProgressFrame = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-frame", observeValue);
        }
        
        internal static ObservableGauge<Int64>? FFmepgProgressSize;
        public static void RegisterFFmepgProgressSizeObserve(Func<Int64> observeValue)
        {
            FFmepgProgressSize = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-size", observeValue, "byte");
        }
       
        internal static ObservableGauge<int>? FFmepgProgressBitrate;
        public static void RegisterFFmepgProgressBitrateObserve(Func<int> observeValue)
        {
            FFmepgProgressBitrate = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-bitrate", observeValue, "bits/s");
        }
        
        internal static ObservableGauge<int>? FFmepgProgressDup;
        public static void RegisterFFmepgProgressDupObserve(Func<int> observeValue)
        {
            FFmepgProgressDup = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-dup", observeValue, "frames");
        }
       
        internal static ObservableGauge<int>? FFmepgProgressDrop;
        public static void RegisterFFmepgProgressDropObserve(Func<int> observeValue)
        {
            FFmepgProgressDrop = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-drop", observeValue, "frames");
        }
        
        internal static ObservableGauge<double>? FFmepgProgressSpeed;
        public static void RegisterFFmepgProgressSpeedObserve(Func<double> observeValue)
        {
            FFmepgProgressSpeed = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress-speed", observeValue);
        }
       
        internal static ObservableGauge<int>? FFmepgProgressProgress;
        public static void RegisterFFmepgProgressProgressObserve(Func<int> observeValue)
        {
            FFmepgProgressProgress = FFMpegProgressMeter.Meter.CreateObservableGauge("ffmpeg-progress", observeValue);
        }


        internal static Counter<int> FFMpegInstanceRestart { get; } = FFMpegProgressMeter.Meter.CreateCounter<int>("ffmpeg-instance-restart");





        internal static ObservableCounter<double>? ProcessorTimes;

        static FFMpegProgressMeter()
        {
            ProcessorTimes = FFMpegProgressMeter.Meter.CreateObservableCounter($"process.cpu.time", FFMpegProgressMeter.GetProcessorTimes, "s", "Processor time of this process");
        }


        private static IEnumerable<Measurement<double>> GetProcessorTimes()
        {
            var process = Process.GetCurrentProcess();
            return new[]
            {
                new Measurement<double>(process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "user")),
                new Measurement<double>(process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "system")),
            };
        }


    }
}
