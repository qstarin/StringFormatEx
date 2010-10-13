using System;
using System.IO;
using System.Text;
using StringFormatEx.Plugins.Core;



namespace StringFormatEx.Extensions
{
    public static class ExtendedStringFormatterExtensions
    {
        public static string FormatEx(this string format, IFormatProvider formatProvider, params object[] args)
        {
            return ExtendedStringFormatter.Default.FormatEx(formatProvider, format, args);
        }

        public static string FormatEx(this string format, params object[] args)
        {
            return ExtendedStringFormatter.Default.FormatEx(format, args);
        }


        public static void FormatEx(this string format, Stream output, IFormatProvider formatProvider, params object[] args)
        {
           ExtendedStringFormatter.Default.FormatEx(output, formatProvider, format, args);
        }
        
        public static void FormatEx(this string format, Stream output, params object[] args)
        {
           ExtendedStringFormatter.Default.FormatEx(output, format, args);
        }


        public static void FormatEx(this string format, TextWriter output, IFormatProvider formatProvider, params object[] args)
        {
           ExtendedStringFormatter.Default.FormatEx(output, formatProvider, format, args);
        }
        
        public static void FormatEx(this string format, TextWriter output, params object[] args)
        {
           ExtendedStringFormatter.Default.FormatEx(output, format, args);
        }


        public static void FormatEx(this string format, StringBuilder output, IFormatProvider formatProvider, params object[] args)
        {
           ExtendedStringFormatter.Default.FormatEx(output, formatProvider, format, args);
        }
        
        public static void FormatEx(this string format, StringBuilder output, params object[] args)
        {
           ExtendedStringFormatter.Default.FormatEx(output, format, args);
        }

    }
}