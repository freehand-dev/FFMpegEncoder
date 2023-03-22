using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FFMpegEncoder.Models
{
    public static class FFMpegOutputParser
    {
        public static bool FFMpegParseMpegTS(string line, out string? id, out string? message)
        {
            id = default;
            message = default;
            Regex mpegtsRegex = new Regex("\\[mpegts\\s@\\s(\\w+)](.+)", RegexOptions.Compiled);
            Match match = mpegtsRegex.Match(line);
            if (match.Success)
            {
                id = match.Groups[1].Value;
                message = match.Groups[2].Value.Trim();
            }
            return match.Success;
        }

        public static bool FFMpegParseSRT(string line, out string? id, out string? message)
        {
            id = default;
            message = default;
            Regex mpegtsRegex = new Regex("\\[srt\\s@\\s(\\w+)](.+)", RegexOptions.Compiled);
            Match match = mpegtsRegex.Match(line);
            if (match.Success)
            {
                id = match.Groups[1].Value;
                message = match.Groups[2].Value.Trim();
            }
            return match.Success;
        }

        public static bool FFMpegParseDecklink(string line, out string? id, out string? message)
        {
            id = default;
            message = default;
            Regex mpegtsRegex = new Regex("\\[decklink\\s@\\s(\\w+)](.+)", RegexOptions.Compiled);
            Match match = mpegtsRegex.Match(line);
            if (match.Success)
            {
                id = match.Groups[1].Value;
                message = match.Groups[2].Value.Trim();
            }
            return match.Success;
        }


        public static FFMpegOutputProgress? FFMpegParseProgress(string line)
        {
            FFMpegOutputProgress? result = new FFMpegOutputProgress();

            // frame
            Match frameMatch = Regex.Match(line, @"frame=\s*([\w\.\:\/]+)\s");
            if (frameMatch.Success)
            {
                if (Int32.TryParse(frameMatch.Groups[1].Value.Trim(), out int frameParse))
                {
                    result.Frame = frameParse;
                }
            }


            // fps
            Match fpsMatch = Regex.Match(line, @"fps=\s*([\w\.\:\/]+)\s");
            if (fpsMatch.Success)
            {
                if (Double.TryParse(fpsMatch.Groups[1].Value.Trim(), out double fpsParse))
                {
                    result.FPS = fpsParse;
                }
            }


            // size
            Match sizeMatch = Regex.Match(line, @"size=\s*([\w\.\:\/]+)\s");
            if (sizeMatch.Success)
            {
                result.Size = sizeMatch.Groups[1].Value.Trim();
            }


            // bitrate
            Match bitrateMatch = Regex.Match(line, @"bitrate=\s*([\w\.\:\/]+)\s");
            if (bitrateMatch.Success)
            {
                result.Bitrate = bitrateMatch.Groups[1].Value.Trim();
            }


            // time
            Match timeMatch = Regex.Match(line, @"time=\s*([\w\.\:\/]+)\s");
            if (timeMatch.Success)
            {
                if (TimeSpan.TryParse(timeMatch.Groups[1].Value.Trim(), CultureInfo.InvariantCulture, out var parseTime))
                {
                    result.Time = parseTime;
                }
            }


            // dup
            Match dupMatch = Regex.Match(line, @"dup=\s*([\w\.\:\/]+)\s");
            if (dupMatch.Success)
            {
                if (Int32.TryParse(dupMatch.Groups[1].Value.Trim(), out int dupParse))
                {
                    result.Dup = dupParse;
                }
            }

            // drop
            Match dropMatch = Regex.Match(line, @"drop=\s*([\w\.\:\/]+)\s");
            if (dropMatch.Success)
            {
                if (Int32.TryParse(dropMatch.Groups[1].Value.Trim(), out int dropParse))
                {
                    result.Drop = dropParse;
                }
            }

            // speed
            Match speedMatch = Regex.Match(line, @"speed=\s*([\w\.\:\/]+)\s");
            if (speedMatch.Success)
            {
                result.Speed = speedMatch.Groups[1].Value.Trim();
            }


            return (frameMatch.Success && bitrateMatch.Success) ? result : null;
        }
    }
}
