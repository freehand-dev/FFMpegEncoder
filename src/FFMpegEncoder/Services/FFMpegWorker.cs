using FFMpegCore.Pipes;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Extensions;
using FFMpegCore.Enums;
using Microsoft.Extensions.Options;
using FFMpegEncoder.Models;
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
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.Metrics;
using FFMpegEncoder.Infrastructure;
using FFMpegEncoder.Infrastructure.Metrics;

namespace FFMpegEncoder.Services
{
    /// <summary>
    /// -loglevel verbose -channels 8 -format_code Hi50 -raw_format uyvy422 -video_input sdi -audio_input embedded -draw_bars 0 -queue_size 1073741824 -f decklink -i "DeckLink Duo (4)" 
    /// -filter_complex "[0:a]channelmap=map=0|1:stereo[ch1];[0:a] channelmap=map=2|3:stereo[ch2]" -map 0:v -map [ch1] -map [ch2] 
    /// -c:0 libx264 -b:0 8000k -preset:0 faster -profile:0 high -level:0 4.0 -minrate:0 8000k -maxrate:0 8000k -bufsize:0 700k -pix_fmt:0 yuv420p -aspect:0 16:9 -x264-params:0 nal-hrd=cbr -top:0 1 -flags:0 +ilme+ildct+cgop 
    /// -c:1 libfdk_aac -b:1 192k -c:2 libfdk_aac -b:2 192k 
    /// -bsf:v h264_mp4toannexb -flush_packets 0 -rtbufsize 2000M 
    /// -f mpegts -mpegts_transport_stream_id 1 -mpegts_original_network_id 1 -mpegts_service_id 1 -muxrate 9000k -mpegts_start_pid 336 -mpegts_pmt_start_pid 4096 -pcr_period 20 -pat_period 0.10 -sdt_period 0.25 -nit_period 0.5 -metadata service_name=OBVan -metadata service_provider=ES -metadata title=BabyBird -mpegts_flags +pat_pmt_at_frames+system_b+nit 
    /// "srt://193.239.153.205:9103?ipttl=15&latency=3000&mode=caller&payload_size=1456&transtype=live"
    /// </summary>
    public class FFMpegWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<FFMpegWorker> _logger;

        private FFMpegOptions _settings;
        private CancellationTokenSource? _ffmpegProcessorTokenSource;

        public FFMpegWorker(ILogger<FFMpegWorker> logger, IOptionsMonitor<FFMpegOptions> settings)
        {
            this._logger = logger;
            this._settings = settings.CurrentValue;

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


            var ffmpegArgs = fFMpegArguments?.WithGlobalOptions((opt) =>
            {
                opt.WithVerbosityLevel(options.LogLevel);
            })
            .OutputToUrl("\"" + options.Muxer.MpegTS?.Output.ToUri() + "\"", opt =>
            {
                // progress
                opt.WithCustomArgument($"-progress \\\\.\\pipe\\{FFMpegProgressListener.FFMpegProgresPipeName}");

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
                    var ffmpegProcessor = FFMpegWorker.GetFFMpegArgumentsProcessor(_settings,
                        #region OnError
                        error =>
                        {
                            try
                            {
                                string? id;
                                string? message;

                                if (FFMpegOutputParser.FFMpegParseSRT(error, out id, out message))
                                {
                                    _logger.LogInformation($"SRT[{id}], message: {message}");
                                }
                                else if (FFMpegOutputParser.FFMpegParseMpegTS(error, out id, out message))
                                {
                                    _logger.LogInformation($"MPEGTS[{id}], message: {message}");
                                }
                                else if (FFMpegOutputParser.FFMpegParseDecklink(error, out id, out message))
                                {
                                    _logger.LogInformation($"Decklink[{id}], message: {message}");
                                }
                            }
                            catch
                            {
                                _logger.LogWarning($"Error parse ffmpeg output: {error}");
                            }                           
                        }
                        #endregion
                    );

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


                // OpenTelemetry metric 
                FFMpegProgressMeter.FFMpegInstanceRestart.Add(1);

                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogWarning($"FFMpegWorker: stop");
        }
    }
}