using FFMpegCore.Arguments;

namespace FFMpegCore.Extensions.Arguments
{
    public class FilterComplexArgument : IArgument
    {
        private readonly string _filter;
        public string Text => $"-filter_complex {_filter}";

        public FilterComplexArgument(string filter)
        {
            _filter = filter;
        }
    }
}
