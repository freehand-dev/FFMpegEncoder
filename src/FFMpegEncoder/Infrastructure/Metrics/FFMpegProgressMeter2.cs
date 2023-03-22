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
    public class FFMpegProgressMeter2 : IDisposable
    {
        public static readonly string MeterName = "FFMpegProgress";
        public static readonly string MeterVersion = "1.0.0";


        public readonly Meter _meter = new Meter("FFMpegProgress", "1.0.0");

        private double _time = 0;
        public void SetProgressTime(double time) => Interlocked.Exchange(ref this._time, time);
        public double GetProgressTime() => Volatile.Read(ref this._time);

        private double _fps = 0;
        public void SetProgressFps(double fps) => Interlocked.Exchange(ref this._fps, fps);
        public double GetProgressFps() => Volatile.Read(ref this._fps);

        private int _frame = 0;
        public void SetProgressFrame(int frame) => Interlocked.Exchange(ref this._frame, frame);
        public int GetProgressFrame() => Volatile.Read(ref this._frame);
        
        private int _size = 0;
        public void SetProgressSize(int size) => Interlocked.Exchange(ref this._size, size);
        public int GetProgressSize() => Volatile.Read(ref this._size);

        private int _bitrate = 0;
        public void SetProgressBitrate(int bitrate) => Interlocked.Exchange(ref this._bitrate, bitrate);
        public int GetProgressBitrate() => Volatile.Read(ref this._bitrate);
        
        private int _dup = 0;
        public void SetProgressDup(int dup) => Interlocked.Exchange(ref this._dup, dup);
        public int GetProgressDup() => Volatile.Read(ref this._dup);

        private int _drop = 0;
        public void SetProgressDrop(int drop) => Interlocked.Exchange(ref this._drop, drop);
        public int GetProgressDrop() => Volatile.Read(ref this._drop);

        private double _speed = 0;
        public void SetProgressSpeed(double speed) => Interlocked.Exchange(ref this._speed, speed);
        public double GetProgressSpeed() => Volatile.Read(ref this._speed);

        private int _progress = 0;
        public void SetProgressProgress(int progress) => Interlocked.Exchange(ref this._progress, progress);
        public int GetProgressProgress() => Volatile.Read(ref this._progress);


        /// <summary>
        /// Initializes a new instance of the <see cref="FFMpegProgressMeter"/> class.
        /// </summary>
        public FFMpegProgressMeter2()
        {
            // ffmpeg progress
            this._meter = new Meter(FFMpegProgressMeter2.MeterName, FFMpegProgressMeter2.MeterVersion);
            this._meter.CreateObservableGauge($"ffmpeg-progress-time", () => this.GetProgressTime(), "ms");
            this._meter.CreateObservableGauge($"ffmpeg-progress-fps", () => this.GetProgressFps(), "fps");
            this._meter.CreateObservableGauge($"ffmpeg-progress-frame", () => this.GetProgressFrame());
            this._meter.CreateObservableGauge($"ffmpeg-progress-size", () => this.GetProgressSize(), "byte");
            this._meter.CreateObservableGauge($"ffmpeg-progress-bitrate", () => this.GetProgressBitrate(), "bits/s");
            this._meter.CreateObservableGauge($"ffmpeg-progress-dup", () => this.GetProgressDup(), "frames");
            this._meter.CreateObservableGauge($"ffmpeg-progress-drop", () => this.GetProgressDrop(), "frames");
            this._meter.CreateObservableGauge($"ffmpeg-progress-speed", () => this.GetProgressSpeed());
            this._meter.CreateObservableGauge($"ffmpeg-progress", () => this.GetProgressProgress());


            // system
            this._meter.CreateObservableGauge($"process.threadpool.thread.count", () => ThreadPool.ThreadCount, description: "ThreadPool Thread Count");
            this._meter.CreateObservableGauge($"process.cpu.count", () => Environment.ProcessorCount, description: "The number of available logical CPUs");
            this._meter.CreateObservableGauge($"process.memory.usage", () => Process.GetCurrentProcess().WorkingSet64, "By", "The amount of physical memory in use");
            this._meter.CreateObservableGauge($"process.memory.virtual", () => Process.GetCurrentProcess().VirtualMemorySize64, "By", "The amount of committed virtual memory");
            this._meter.CreateObservableCounter($"process.cpu.time", this.GetProcessorTimes, "s", "Processor time of this process");

        }

        private IEnumerable<Measurement<double>> GetProcessorTimes()
        {
            var process = Process.GetCurrentProcess();
            return new[]
            {
                new Measurement<double>(process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "user")),
                new Measurement<double>(process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "system")),
            };
        }

        public void Dispose()
        {
            this._meter?.Dispose();
        }

    }
}
