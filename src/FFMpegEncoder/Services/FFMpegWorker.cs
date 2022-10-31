using FFMpegCore.Pipes;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Extensions;
using FFMpegCore.Enums;
using Microsoft.Extensions.Options;
using sdi2srt_ffmpeg.Models;
using System.Threading.Channels;
using System.Xml.Linq;
using System.Linq;
using System.Runtime;
using System.Runtime.Intrinsics.X86;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using FFMpegCore.Builders.MetaData;
using Instances;
using FFMpegCore.Exceptions;
using Microsoft.VisualBasic;
using Microsoft.Extensions.Logging;

namespace sdi2srt_ffmpeg.Services
{
    public class FFMpegWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<FFMpegWorker> _logger;
        private FFMpegOptions _settings;
        private CancellationTokenSource? _ffmpegProcessorTokenSource;

        public FFMpegWorker(ILogger<FFMpegWorker> logger, IOptionsMonitor<FFMpegOptions> settings)
        {
            _logger = logger;
            _settings = settings.CurrentValue;
            settings.OnChange(settings => 
            {
                _logger.LogInformation("Setting changes detected");
                _settings = settings;

                // stop current ffmpeg instance
                _ffmpegProcessorTokenSource?.Cancel();
            });
        }

        public override void Dispose()
        {
            _ffmpegProcessorTokenSource?.Cancel();
            base.Dispose();
        }

        private (string fps, string bitrate, string time) FFMpegParseProgress(string line)
        {
            string fps = "0 fps";
            string bitrate = "0 Kbps";
            string time = "00:00:00";
            if (line.IndexOf("bitrate=") != -1 && line.IndexOf("fps=") != -1 && line.IndexOf("time=") != -1)
            {
                string[] array = line.Split(new char[]
                {
                        ' '
                });
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i].IndexOf("fps=") != -1)
                    {
                        fps = array[i + 1];
                    }
                    else if (array[i].IndexOf("bitrate=") != -1)
                    {
                        bitrate = array[i].Split(new char[]
                        {
                                '='
                        })[1];
                    }
                    else if (array[i].IndexOf("time=") != -1)
                    {
                        time = array[i].Split(new char[]
                        {
                                '='
                        })[1];
                    }
                }
            }
            return (fps, bitrate, time);
        }


        private static FFMpegArguments FromDecklinkInput(string Name, int? Channels, string? Format, string? RawFormat, string? VideoInput, string? AudioInput, bool? DrawBars, int? QueueSize, bool? ReadInputAtNativeFrameRate)
        {
            return FFMpegArguments.FromDeviceInput("\"" + Name + "\"",
                (opt) =>
                {
                    if (ReadInputAtNativeFrameRate.HasValue)
                        if (ReadInputAtNativeFrameRate.Value)
                            opt.WithCustomArgument("-re");
                    if (Channels.HasValue)
                        opt.WithCustomArgument($"-channels { Channels.Value }");
                    if (!string.IsNullOrEmpty(Format))
                        opt.WithCustomArgument($"-format_code {Format}");
                    if (!string.IsNullOrEmpty(RawFormat))
                        opt.WithCustomArgument($"-raw_format {RawFormat}");
                    if (!string.IsNullOrEmpty(VideoInput))
                        opt.WithCustomArgument($"-video_input {VideoInput}");
                    if (!string.IsNullOrEmpty(AudioInput))
                        opt.WithCustomArgument($"-audio_input {AudioInput}");
                    if (DrawBars.HasValue)
                        opt.WithCustomArgument($"-draw_bars { (DrawBars.Value ? "1" : "0") }");
                    if (QueueSize.HasValue)
                        opt.WithCustomArgument($"-queue_size {QueueSize.Value}");
                    opt.ForceFormat("decklink");
                });
        }

        private static FFMpegArguments FromTestGeneratorInput(string Name = @"testsrc=size=1920x1080:rate=25[out0];sine[out1]", bool? ReadInputAtNativeFrameRate = true)
        {
            return FFMpegArguments.FromDeviceInput("\"" + Name + "\"",
                (opt) =>
                {
                    if (ReadInputAtNativeFrameRate.HasValue)
                        if (ReadInputAtNativeFrameRate.Value)
                            opt.WithCustomArgument("-re");
                    opt.ForceFormat("lavfi");
                });
        }

        public static void WriteStreamToDisk(Stream stream, string outpt)
        {
            var buf = new byte[stream.Length];
            stream.Read(buf);
            var f = File.Create(outpt, buf.Length);
            f.Write(buf);
            f.Close();
        }


        private static FFMpegArgumentProcessor? GetFFMpegArgumentsProcessor(FFMpegOptions options, Action<string>? onError = null)
        {
            /*
                if (!string.IsNullOrEmpty(_settings.CurrentValue.Raw))
                {
                    var processArguments = new ProcessArguments(GlobalFFOptions.GetFFMpegBinaryPath(), _settings.CurrentValue.Raw);
                    processArguments.OutputDataReceived += (e, data) =>
                    {
                        _logger.LogInformation($"OutputDataReceived: {data}");
                    };

                    var result = await processArguments.StartAndWaitForExitAsync(stoppingToken);   
                    if (result.ExitCode != 0)
                        throw new FFMpegException(FFMpegExceptionType.Process, string.Join("\r\n", result.OutputData));
                }

*/

            FFMpegArguments? fFMpegArguments = null;
            switch (options.Input.Device)
            {
                case "decklink":
                    fFMpegArguments = FFMpegWorker.FromDecklinkInput(
                        Name: options.Input.Name,
                        Channels: options.Input.Channels,
                        Format: options.Input.Format,
                        RawFormat: options.Input.RawFormat,
                        VideoInput: options.Input.VideoInput,
                        AudioInput: options.Input.AudioInput,
                        DrawBars: options.Input.DrawBars,
                        QueueSize: options.Input.QueueSize,
                        ReadInputAtNativeFrameRate: options.Input.ReadInputAtNativeFrameRate
                    );
                    break;
                case "lavfi":
                    fFMpegArguments = FFMpegWorker.FromTestGeneratorInput(
                        Name: options.Input.Name,
                        ReadInputAtNativeFrameRate: options.Input.ReadInputAtNativeFrameRate
                    );
                    break;
            }


            var ffmpegArgs =  fFMpegArguments?.WithGlobalOptions((opt) =>
            {
                opt.WithVerbosityLevel(options.LogLevel);
            })
            .OutputToUrl("\"" + options.Muxer.MpegTS?.Output.ToUri() + "\"", opt =>
            {
                #region 'filter_complex'
                if (options.FilterComplex.Count > 0)
                {
                    var filterComplex = string.Join(";", options.FilterComplex);
                    opt.WithCustomArgument($"-filter_complex \"{filterComplex}\"");
                }
                #endregion

                #region 'maps'
                foreach (var map in options.Maps)
                {
                    opt.WithMap(map);
                }
                #endregion

                #region 'codecs'
                int stream = 0;
                foreach (var encoder in options.Encoders)
                {
                    var s = Convert.ToString(stream);
                    // -c:?
                    opt.WithCodec(encoder.Codec, s);

                    // -b:?
                    if (encoder.Bitrate.HasValue)
                        opt.WithBitrate(encoder.Bitrate.Value, s);

                    // -preset:?
                    if (!string.IsNullOrEmpty(encoder.Preset))
                        if (Enum.TryParse<Speed>(encoder.Preset, true, out Speed result))
                            opt.WithCustomArgument($"-preset:{s} {result.ToString().ToLowerInvariant()}");

                    // -profile:?
                    if (!string.IsNullOrEmpty(encoder.Profile))
                        opt.WithCustomArgument($"-profile:{s} {encoder.Profile}");

                    // -level:?
                    if (!string.IsNullOrEmpty(encoder.Level))
                        opt.WithCustomArgument($"-level:{s} {encoder.Level}");

                    // -tag:?
                    if (!string.IsNullOrEmpty(encoder.Tag))
                        opt.WithCustomArgument($"-tag:{s} {encoder.Tag}");

                    //
                    foreach (FFMpegArgument arg in encoder.CustomArguments)
                    {
                        opt.WithCustomArgument($"-{arg.Name}:{s} {arg.Value}");
                    }

                    // -flags:?
                    if (encoder.Flags.Count > 0)
                    {
                        var flags = string.Join("", encoder.Flags);
                        opt.WithCustomArgument($"-flags:{s} {flags}");
                    }

                    stream++;
                }
                #endregion

                #region 'BitstreamFilter'
                if (options.BitstreamFilter.TryGetValue("video", out List<string>? bsf_video))
                {
                    foreach (var item in bsf_video)
                    {
                        opt.WithCustomArgument($"-bsf:v {item}");
                    }
                }

                if (options.BitstreamFilter.TryGetValue("audio", out List<string>? bsf_audio))
                {
                    foreach (var item in bsf_audio)
                    {
                        opt.WithCustomArgument($"-bsf:a {item}");
                    }
                }
                #endregion

                #region 'global'
                foreach (FFMpegArgument arg in options.GlobalOptions)
                {
                    opt.WithCustomArgument($"-{arg.Name} {arg.Value}");
                }
                #endregion

                #region 'muxer:mpegts'
                if (options.Muxer.MpegTS != null)
                {
                    opt.ForceFormat("mpegts");

                    // -mpegts_transport_stream_id
                    if (options.Muxer.MpegTS.TransportStreamId.HasValue)
                        opt.WithCustomArgument($"-mpegts_transport_stream_id {options.Muxer.MpegTS.TransportStreamId.Value}");

                    // -mpegts_original_network_id 
                    if (options.Muxer.MpegTS.OriginalNetworkId.HasValue)
                        opt.WithCustomArgument($"-mpegts_original_network_id {options.Muxer.MpegTS.OriginalNetworkId.Value}");

                    // -mpegts_service_id
                    if (options.Muxer.MpegTS.ServiceId.HasValue)
                        opt.WithCustomArgument($"-mpegts_service_id {options.Muxer.MpegTS.ServiceId.Value}");

                    // -muxrate
                    if (options.Muxer.MpegTS.MuxRate.HasValue)
                        opt.WithCustomArgument($"-muxrate {options.Muxer.MpegTS.MuxRate.Value}k");

                    // -mpegts_start_pid
                    if (options.Muxer.MpegTS.StartPid.HasValue)
                        opt.WithCustomArgument($"-mpegts_start_pid {options.Muxer.MpegTS.StartPid.Value}");

                    // -mpegts_pmt_start_pid
                    if (options.Muxer.MpegTS.PmtStartPid.HasValue)
                        opt.WithCustomArgument($"-mpegts_pmt_start_pid {options.Muxer.MpegTS.PmtStartPid.Value}");

                    // -pcr_period
                    if (options.Muxer.MpegTS.PcrPeriod.HasValue)
                        opt.WithCustomArgument($"-pcr_period {options.Muxer.MpegTS.PcrPeriod.Value}");

                    // -pat_period 
                    if (!string.IsNullOrEmpty(options.Muxer.MpegTS.PatPeriod))
                        opt.WithCustomArgument($"-pat_period {options.Muxer.MpegTS.PatPeriod}");

                    // -sdt_period
                    if (!string.IsNullOrEmpty(options.Muxer.MpegTS.SdtPeriod))
                        opt.WithCustomArgument($"-sdt_period {options.Muxer.MpegTS.SdtPeriod}");

                    // -nit_period
                    if (!string.IsNullOrEmpty(options.Muxer.MpegTS.NitPeriod))
                        opt.WithCustomArgument($"-nit_period {options.Muxer.MpegTS.NitPeriod}");

                    // -metadata
                    foreach (KeyValuePair<string, string> arg in options.Muxer.MpegTS.Metadata)
                    {
                        opt.WithCustomArgument($"-metadata {arg.Key}={arg.Value}");
                    }

                    // -mpegts_flags
                    if (options.Muxer.MpegTS.Flags.Count > 0)
                    {
                        var flags = string.Join("", options.Muxer.MpegTS.Flags);
                        opt.WithCustomArgument($"-mpegts_flags {flags}");
                    }
                }
                #endregion
            })
            .NotifyOnError((error) =>
            {
                onError?.Invoke(error);
            });

            return ffmpegArgs;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FFMpegWorker running at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                this._ffmpegProcessorTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                CancellationToken token = _ffmpegProcessorTokenSource.Token;


                _logger.LogInformation("FFMpegWorker.ProcessAsynchronously.Start");
                try
                {
                    var ffmpegProcessor = FFMpegWorker.GetFFMpegArgumentsProcessor(_settings);
                    if (ffmpegProcessor == null)
                    {
                        throw new Exception($"Failed create FFMpeg Arguments Processor");
                    }

                    _logger.LogInformation($"FFMpegWorker.Arguments: {ffmpegProcessor?.Arguments}");

                    await ffmpegProcessor.CancellableThrough(token, 5000).ProcessAsynchronously(true).WaitAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning($"FFMpegWorker.ProcessAsynchronously.TaskCanceled");
                }
                catch (System.OperationCanceledException)
                {
                    _logger.LogWarning($"FFMpegWorker.ProcessAsynchronously.OperationCanceled");
                }
                catch (Exception e)
                {
                    _logger.LogError($"FFMpegWorker.ProcessAsynchronously.Exception: { e.Message } {e.GetType()}");
                }


                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}