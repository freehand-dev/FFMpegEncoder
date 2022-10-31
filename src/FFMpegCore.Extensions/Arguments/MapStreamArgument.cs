using FFMpegCore.Arguments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegCore.Extensions.Arguments
{
    public class MapStreamArgument : IArgument
    {
        private readonly string _stream;
        public string Text => $"-map {_stream}";

        public MapStreamArgument(string stream)
        {
            _stream = stream;
        }
    }
}
