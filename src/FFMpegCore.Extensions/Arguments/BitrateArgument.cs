using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegCore.Extensions.Arguments
{
    public class BitrateArgument : IArgument
    {
        public readonly int Bitrate;
        public readonly string Stream;

        public string Text => $"-b:{ Stream } {Bitrate}k";

        public BitrateArgument(int bitrate, string stream)
        {
            Bitrate = bitrate;
            Stream = stream;
        }
    }
}