using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegCore.Extensions.Arguments
{
    public class CodecArgument : IArgument
    {
        public readonly string Codec;
        public readonly string Stream;

        public string Text => $"-c:{ Stream } { Codec }";

        public CodecArgument(string codec, string stream)
        {
            Codec = codec;
            Stream = stream;
        }
    }
}