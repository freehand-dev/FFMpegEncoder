using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegEncoder.Models
{
    public class FFMpegOutputProgress
    {
        public int? Frame {get; set; }
        public double? FPS { get; set; }
        public string? Size { get; set; }
        public string? Bitrate { get; set; }
        public TimeSpan? Time { get; set; }
        public int? Dup { get; set; }
        public int? Drop { get; set; }
        public string? Speed { get; set; }
    }
}
