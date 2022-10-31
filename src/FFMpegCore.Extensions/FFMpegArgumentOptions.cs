using FFMpegCore;
using FFMpegCore.Arguments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegCore.Extensions
{
    public static class FFMpegArgumentOptionsExtensios
    {
        public static FFMpegArgumentOptions WithMap(this FFMpegArgumentOptions arg, string stream)
        {
            return arg.WithArgument(new Arguments.MapStreamArgument(stream));
        }

        public static FFMpegArgumentOptions WithFilterComplex(this FFMpegArgumentOptions arg, string filter)
        {
            return arg.WithArgument(new Arguments.FilterComplexArgument(filter));
        }

        public static FFMpegArgumentOptions WithCodec(this FFMpegArgumentOptions arg, string codec, string stream)
        {
            return arg.WithArgument(new Arguments.CodecArgument(codec, stream));
        }

        public static FFMpegArgumentOptions WithBitrate(this FFMpegArgumentOptions arg, int bitrate, string stream)
        {
            return arg.WithArgument(new Arguments.BitrateArgument(bitrate, stream));
        }

    }
}
