using FFMpegEncoder.Infrastructure.Metrics;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using System.Threading;

namespace FFMpegEncoder.Services
{
    public class FFMpegProgressListener : BackgroundService    
    {

        private readonly ILogger<FFMpegProgressListener> _logger;

        public static readonly string FFMpegProgresPipeName = Guid.NewGuid().ToString();


        private double _time = 0;
        private double Time { get => Volatile.Read(ref this._time); set => Interlocked.Exchange(ref this._time, value); }
       
        private double _fps = 0;
        private double FPS { get => Volatile.Read(ref this._fps); set => Interlocked.Exchange(ref this._fps, value); }

        private Int64 _frame = 0;
        private Int64 Frame { get => Volatile.Read(ref this._frame); set => Interlocked.Exchange(ref this._frame, value); }

        private Int64 _size = 0;
        private Int64 Size { get => Volatile.Read(ref this._size); set => Interlocked.Exchange(ref this._size, value); }

        private int _bitrate = 0;
        private int Bitrate { get => Volatile.Read(ref this._bitrate); set => Interlocked.Exchange(ref this._bitrate, value); }

        private int _dup = 0;
        private int Dup { get => Volatile.Read(ref this._dup); set => Interlocked.Exchange(ref this._dup, value); }

        private int _drop = 0;
        private int Drop { get => Volatile.Read(ref this._drop); set => Interlocked.Exchange(ref this._drop, value); }

        private double _speed = 0;
        private double Speed { get => Volatile.Read(ref this._speed); set => Interlocked.Exchange(ref this._speed, value); }

        private int _progress = 0;
        private int Progress { get => Volatile.Read(ref this._progress); set => Interlocked.Exchange(ref this._progress, value); }


        public FFMpegProgressListener(ILogger<FFMpegProgressListener> logger)
        {
            _logger = logger;
            #region "OpenTelemetry"
            FFMpegProgressMeter.RegisterFFmepgProgressTimeObserve(() => this.Time);
            FFMpegProgressMeter.RegisterFFmepgProgressFpsObserve(() => this.FPS);
            FFMpegProgressMeter.RegisterFFmepgProgressFrameObserve(() => this.Frame);
            FFMpegProgressMeter.RegisterFFmepgProgressSizeObserve(() => this.Size);
            FFMpegProgressMeter.RegisterFFmepgProgressBitrateObserve(() => 
            {
                Console.WriteLine($"!!!!!!!!!!!RegisterFFmepgProgressBitrateObserve {this.Bitrate}");
                return this.Bitrate;
            });
            FFMpegProgressMeter.RegisterFFmepgProgressDupObserve(() => this.Dup);
            FFMpegProgressMeter.RegisterFFmepgProgressDropObserve(() => this.Drop);
            FFMpegProgressMeter.RegisterFFmepgProgressSpeedObserve(() => this.Speed);
            FFMpegProgressMeter.RegisterFFmepgProgressProgressObserve(() => this.Progress);
            #endregion
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var pipeServer = new NamedPipeServerStream(FFMpegProgressListener.FFMpegProgresPipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    _logger.LogInformation($"NamedPipeServerStream: object created. Pipe name is {FFMpegProgressListener.FFMpegProgresPipeName}");

                    // Wait for a client to connect
                    _logger.LogInformation("NamedPipeServerStream: waiting for client connection...");
                    await pipeServer.WaitForConnectionAsync(stoppingToken);

                    _logger.LogInformation("NamedPipeServerStream: client connected.");

                    using (StreamReader reader = new StreamReader(pipeServer))
                    {
                        do
                        {
                            string? line = await reader.ReadLineAsync();
                            if (line == null || stoppingToken.IsCancellationRequested)
                            {
                                _logger.LogInformation("NamedPipeServerStream: reader break");
                                this.ResetProperty();
                                break;
                            }

                            TryProcessMetric(line);



                        } while (true);
                    }

                    if (pipeServer.IsConnected)
                    {
                        pipeServer.Disconnect();
                    }
                } 
                catch (Exception e)
                {
                    _logger.LogWarning(e.Message);
                }

                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogWarning($"FFMpegProgressListener: stop");
        }


        private void TryProcessMetric(string line)
        {
            try
            {
                ProcessMetric(line);
            } 
            catch (Exception e)
            {
                this._logger.LogWarning($"ProcessMetric({ line }) Exception: {e.Message}");
            }
        }


        /// <summary>
        /// frame=154
        /// fps=23.09
        /// stream_0_0_q=-1.0
        /// bitrate=9061.6kbits/s
        /// total_size=6935132
        /// out_time_us=6122667
        /// out_time_ms=6122667
        /// out_time=00:00:06.122667
        /// dup_frames=0
        /// drop_frames=0
        /// speed=0.918x
        /// progress=end
        /// </summary>
        /// <param name="line"></param>
        private void ProcessMetric(string line)
        {
            string[]? arrayLine = line.Trim().ToLower().Split("=");

            if (arrayLine != null && arrayLine.Length == 2)
            {
                string key = arrayLine[0].Trim();
                string value = arrayLine[1].Trim();

                if (value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // frame
                if (key.Equals("frame", StringComparison.OrdinalIgnoreCase))
                {
                    if (Int32.TryParse(value, out int frameParse))
                    {
                        this.Frame = frameParse;
                    }
                }
                else

                // fps
                if (key.Equals("fps", StringComparison.OrdinalIgnoreCase))
                {
                    if (Double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out Double fpsParse))
                    {
                        this.FPS = fpsParse;
                    }
                }
                else

                // total_size
                if (key.Equals("total_size", StringComparison.OrdinalIgnoreCase))
                {
                    if (Int64.TryParse(value, out Int64 sizeParse))
                    {
                        this.Size = sizeParse;
                    }
                }
                else

                // bitrate
                if (key.Equals("bitrate", StringComparison.OrdinalIgnoreCase))
                {
                    if (Double.TryParse(value.Substring(0, value.IndexOf("kbits/s")), NumberStyles.Number, CultureInfo.InvariantCulture, out Double bitrateParse))
                    {
                        this.Bitrate = Convert.ToInt32(bitrateParse * 1024);
                        Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!ProcessMetric { this.Bitrate }");
                    }
                }
                else

                // out_time_ms
                if (key.Equals("out_time_ms", StringComparison.OrdinalIgnoreCase))
                {
                    if (Int32.TryParse(value, out int parseTime))
                    {
                        this.Time = parseTime;
                    }
                }
                else

                // dup_frames
                if (key.Equals("dup_frames", StringComparison.OrdinalIgnoreCase))
                {
                    if (Int32.TryParse(value, out int dupParse))
                    {
                        this.Dup = dupParse;
                    }
                }
                else

                // drop_frames
                if (key.Equals("drop_frames", StringComparison.OrdinalIgnoreCase))
                {
                    if (Int32.TryParse(value, out int dropParse))
                    {
                        this.Drop = dropParse;
                    }
                }
                else

                // speed
                if (key.Equals("speed", StringComparison.OrdinalIgnoreCase))
                {
                    if (Double.TryParse(value.Substring(0, value.IndexOf("x")), NumberStyles.Number, CultureInfo.InvariantCulture, out double speeParse))
                    {
                        this.Speed = speeParse;
                    }
                }
                else

                // progress
                if (key.Equals("progress", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.Equals("continue", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Progress = 1;
                    }
                    else
                    {
                        this.Progress = 0;
                        this.ResetProperty();
                    }
                }
            }
        }

        private void ResetProperty()
        {
            this.Progress = 0;
            this.Time = 0;
            this.FPS = 0;
            this.Frame = 0;
            this.Size = 0;
            this.Bitrate = 0;
            this.Dup = 0;
            this.Drop = 0;
            this.Speed = 0;
        }
    }
}