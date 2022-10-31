using FFMpegCore.Arguments;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace sdi2srt_ffmpeg.Models
{
    public class FFMpegOptions
    {
        public string Sentinel { get; set; } = Guid.NewGuid().ToString();   
        public VerbosityLevel LogLevel { get; set; } = VerbosityLevel.Error;
        public string? Raw { get; set; }
        public FFMpegInputOptions Input { get; set; }
        public List<string> FilterComplex { get; set; }
        public List<string> Maps { get; set; }
        public List<FFMpegEncoderOptions> Encoders { get; set; }
        public Dictionary<string, List<string>> BitstreamFilter { get; set; }
        public List<FFMpegArgument> GlobalOptions { get; set; }
        public FFMpegMuxerOptions Muxer { get; set; }

        public FFMpegOptions()
        {
            this.Input = new FFMpegInputOptions();
            this.Muxer = new FFMpegMuxerOptions();
            this.Encoders = new List<FFMpegEncoderOptions>();
            this.BitstreamFilter = new Dictionary<string, List<string>>();
            this.GlobalOptions = new List<FFMpegArgument>();
            this.FilterComplex = new List<string>();
            this.Maps = new List<string>();
        }
    }


    public class FFMpegMuxerOutputOptions
    {
        public string Protocol { get; set; } = "udp";
        public string Address { get; set; } = "225.0.0.1";
        public int? Port { get; set; } = 1234;

        public List<FFMpegArgument> Options { get; set; }

        /// <summary>
        /// https://ffmpeg.org/ffmpeg-protocols.html#toc-udp
        /// </summary>
        /// <returns></returns>
        public string ToUri()
        {

            var builder = new UriBuilder();
            builder.Scheme = Protocol;
            builder.Host = Address;
            if (Port.HasValue)
                builder.Port = Port.Value;

            string query = string.Join("&", Options.Select(kvp => $"{kvp.Name}={kvp.Value}"));

            string url = builder.ToString().Trim('\\', '/') + (!string.IsNullOrEmpty(query) ? $"?{query}" : string.Empty);
            return url;
        }

        public FFMpegMuxerOutputOptions()
        {
            this.Options = new List<FFMpegArgument>();
        }
        
    }

    /// <summary>
    /// https://ffmpeg.org/ffmpeg-formats.html#toc-mpegts-1
    /// </summary>
    public class FFMpegMuxerMpegTsOptions
    {
        /// <summary>
        /// mpegts_transport_stream_id integer
        /// Set the ‘transport_stream_id’. This identifies a transponder in DVB.Default is 0x0001.
        /// </summary>
        public int? TransportStreamId { get; set; }

        /// <summary>
        /// mpegts_original_network_id integer
        /// Set the ‘original_network_id’. This is unique identifier of a network in DVB.Its main use is in the unique identification of a service through the path ‘Original_Network_ID, Transport_Stream_ID’. Default is 0x0001.
        /// </summary>
        public int? OriginalNetworkId { get; set; }

        /// <summary>
        /// mpegts_service_id integer
        /// Set the ‘service_id’, also known as program in DVB.Default is 0x0001.
        /// </summary>
        public int? ServiceId { get; set; }

        /// <summary>
        /// muxrate integer
        /// Set a constant muxrate.Default is VBR.
        /// </summary>
        public int? MuxRate { get; set; }

        /// <summary>
        /// mpegts_start_pid integer
        /// Set the first PID for elementary streams.Default is 0x0100, minimum is 0x0020, maximum is 0x1ffa. This option has no effect in m2ts mode where the elementary stream PIDs are fixed.
        /// </summary>
        public int? StartPid { get; set; }

        /// <summary>
        /// mpegts_pmt_start_pid integer
        /// Set the first PID for PMTs.Default is 0x1000, minimum is 0x0020, maximum is 0x1ffa. This option has no effect in m2ts mode where the PMT PID is fixed 0x0100.
        /// </summary>
        public int? PmtStartPid { get; set; }

        /// <summary>
        /// mpegts_pmt_start_pid integer
        /// Set the first PID for PMTs.Default is 0x1000, minimum is 0x0020, maximum is 0x1ffa. This option has no effect in m2ts mode where the PMT PID is fixed 0x0100.
        /// </summary>
        public int? PcrPeriod { get; set; }

        /// <summary>
        /// pat_period duration
        /// Maximum time in seconds between PAT/PMT tables.Default is 0.1.
        /// </summary>
        public string? PatPeriod { get; set; }

        /// <summary>
        /// sdt_period duration
        /// Maximum time in seconds between SDT tables.Default is 0.5.
        /// </summary>
        public string? SdtPeriod { get; set; }

        /// <summary>
        /// nit_period duration
        /// Maximum time in seconds between NIT tables.Default is 0.5.
        /// </summary>
        public string? NitPeriod { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
        public List<string> Flags { get; set; }

        public FFMpegMuxerOutputOptions Output { get; set; }

        public FFMpegMuxerMpegTsOptions()
        {
            this.Output = new FFMpegMuxerOutputOptions();
            this.Metadata = new Dictionary<string, string>();
            this.Flags = new List<string>();
        }
    }

    public class FFMpegMuxerOptions
    {
        public FFMpegMuxerMpegTsOptions? MpegTS { get; set; }
    }

    public class FFMpegInputOptions 
    {
        public string Device { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool? ReadInputAtNativeFrameRate { get; set; }
        public int? Channels { get; set; }
        public string? Format { get; set; }
        public string? RawFormat { get; set; }
        public string? VideoInput { get; set; }
        public string? AudioInput { get; set; }
        public bool? DrawBars { get; set; }
        public int? QueueSize { get; set; }
    }

    public class FFMpegEncoderOptions
    {
        public string Codec { get; set; } = string.Empty;
        public string? Preset { get; set; }
        public string? Profile { get; set; }
        public string? Level { get; set; }
        public string? Tag { get; set; }
        public int? Bitrate { get; set; }
        public List<string> Flags { get; set; }
        public List<FFMpegArgument> CustomArguments { get; set; }

        public FFMpegEncoderOptions()
        {
            this.CustomArguments = new List<FFMpegArgument>();
            this.Flags = new List<string>();
        }
    }


    public class FFMpegArgument
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

}
