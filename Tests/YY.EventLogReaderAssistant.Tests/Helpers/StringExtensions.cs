using System;
using System.Collections.Generic;
using System.Text;

namespace YY.EventLogReaderAssistant.Tests.Helpers
{
    public static class StringExtensions
    {
        public static DateTime ToDateTime(this string sourceValue)
        {
            try
            {
                return DateTime.Parse(sourceValue);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
