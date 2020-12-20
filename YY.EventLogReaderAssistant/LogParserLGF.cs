using YY.EventLogReaderAssistant.Models;
using YY.EventLogReaderAssistant.Helpers;
using System;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("YY.EventLogReaderAssistant.Tests")]
namespace YY.EventLogReaderAssistant
{
    internal sealed class LogParserLGF
    {
        #region Private Static Members

        private static readonly int _commentPartNumber = EventLogRowPartLGF.Comment.AsInt();
        private static readonly int _dataPartNumber = EventLogRowPartLGF.Data.AsInt();
        private static readonly int _dataPresentationPartNumber = EventLogRowPartLGF.DataPresentation.AsInt();
        private static readonly Regex _regexEndOfComment = new Regex("\",[\\d]+.(\\n|\\r|\\r\\n){([\\w\\W]+|)},\"([\\w\\W]+|)\",[\\d]+,[\\d]+,[\\d]+,[\\d]+,[\\d]+,([\\d]+,|)(\\n|\\r|\\r\\n){[\\d]+}(\\n|\\r|\\r\\n)(}|,|)");
        private static readonly Regex _regexEndOfData                                           = new Regex("},\"([\\w\\W]+|)\",[\\d]+,[\\d]+,[\\d]+,[\\d]+,[\\d]+,([\\d]+,|)(\\n|\\r|\\r\\n){[\\d]+}(\\n|\\r|\\r\\n)(}|,|)");
        private static readonly Regex _regexEndOfDataPresentation                                                 = new Regex(",[\\d]+,[\\d]+,[\\d]+,[\\d]+,[\\d]+,([\\d]+,|)(\\n|\\r|\\r\\n){[\\d]+}(\\n|\\r|\\r\\n)(}|,|)");

        #endregion

        #region Static Methods

        public static bool ItsBeginOfEvent(string sourceString)
        {
            if (sourceString == null)
                return false;

            return Regex.IsMatch(sourceString, @"^{\d{4}\d{2}\d{2}\d+,");
        }
        public static bool ItsEndOfEvent(string sourceString, ref int count, ref bool textBlockOpen)
        {
            if (sourceString == null)
                return false;

            string bufferString = sourceString;

            for (int i = 0; i <= bufferString.Length - 1; i++)
            {
                string simb = bufferString.Substring(i, 1);
                if (simb == "\"")
                {
                    textBlockOpen = !textBlockOpen;
                }
                else if (simb == "}" & !textBlockOpen)
                {
                    count -= 1;
                }
                else if (simb == "{" & !textBlockOpen)
                {
                    count += 1;
                }
            }

            return (count == 0);
        }

        #endregion

        #region Private Member Variables

        private readonly EventLogLGFReader _reader;

        #endregion

        #region Constructor

        public LogParserLGF(EventLogLGFReader reader)
        {
            _reader = reader;
        }

        #endregion

        #region Public Methods

        public LogParserReferencesLGF GetEventLogReferences()
        {
            LogParserReferencesLGF referencesInfo = new LogParserReferencesLGF(_reader);

            return referencesInfo;
        }
        public RowData Parse(string eventSource)
        {
            string[] parseResult = ParseEventLogString(eventSource, LogParserModeLGF.EventLogRow);
            RowData dataRow = null;

            if (parseResult != null)
            {
                dataRow = new RowData();
                dataRow.FillByStringParsedData(_reader, parseResult);
            }

            return dataRow;
        }
        public static string[] ParseEventLogString(string sourceString, LogParserModeLGF mode = LogParserModeLGF.Common)
        {
            string[] resultStrings = null;
            string preparedString = sourceString.Substring(1, (sourceString.EndsWith(",") ? sourceString.Length - 3 : sourceString.Length - 2)) + ",";
            string bufferString = string.Empty;
            int i = 0, partNumber = 0, delimiterIndex = GetDelimiterIndex(preparedString, false, LogParserModeLGF.Common, 0, out var forceAddResult);
            
            while (delimiterIndex > 0)
            {
                partNumber += 1;
                if (mode == LogParserModeLGF.EventLogRow
                    && (i == _commentPartNumber || i == _dataPartNumber))
                {
                    bufferString += preparedString.Substring(0, delimiterIndex);
                } else
                    bufferString += preparedString.Substring(0, delimiterIndex).Trim();
                preparedString = preparedString.Substring(delimiterIndex + 1);
                bool isSpecialString = IsSpecialString(bufferString, partNumber);

                if (AddResultString(ref resultStrings, ref i, ref bufferString, isSpecialString, mode, forceAddResult))
                {
                    i += 1;
                    bufferString = string.Empty;
                    partNumber = 0;
                    isSpecialString = false;
                }
                else
                    bufferString += ",";

                delimiterIndex = GetDelimiterIndex(preparedString, isSpecialString, mode, i, out forceAddResult);
            }

            return resultStrings;
        }

        #endregion

        #region Private Methods

        private static bool AddResultString(ref string[] resultStrings, ref int i, ref string bufferString, bool isSpecialString, LogParserModeLGF mode, bool forceAddResult = false)
        {
            bool output = false;

            if (forceAddResult || IsCorrectLogPart(bufferString, isSpecialString))
            {
                Array.Resize(ref resultStrings, i + 1);
                bufferString = bufferString.RemoveDoubleQuotes();

                if (mode == LogParserModeLGF.EventLogRow && i == _commentPartNumber)
                {
                    // Текст комментария заменяем двойные кавычки (экранирование) на обычные и обрезаем "по краям"
                    // Плюс убираем символ '\r' - возврат каретки
                    bufferString = bufferString
                        .Replace("\"\"", "\"")
                        .RemoveCarriageReturnSymbol()
                        .Trim();
                } else if (mode == LogParserModeLGF.EventLogRow && i == _dataPartNumber)
                {
                    // Для текста данных только обрезаем "по краям" незначащие символы
                    // Плюс убираем символ '\r' - возврат каретки
                    bufferString = bufferString
                        .RemoveCarriageReturnSymbol()
                        .Trim();
                }
                else if (mode == LogParserModeLGF.EventLogRow && i == _dataPresentationPartNumber)
                {
                    // Для текста представления данных удаляем служебные символы и обрезаем "по краям"
                    // Плюс убираем символ '\r' - возврат каретки
                    bufferString = bufferString//.RemoveSpecialSymbols()
                        .Replace("\"\"", "\"")
                        .RemoveCarriageReturnSymbol()
                        .Trim();
                }
                else
                {
                    if (isSpecialString && !string.IsNullOrEmpty(bufferString))
                        bufferString = bufferString.RemoveSpecialSymbols();
                }

                resultStrings[i] = bufferString;
                output = true;
            }

            return output;
        }
        private static bool IsSpecialString(string sourceString, int partNumber)
        {
            bool isSpecialString = partNumber == 1 &&
                                   !string.IsNullOrEmpty(sourceString)
                                   && sourceString[0] == '\"';

            return isSpecialString;
        }
        private static bool IsCorrectLogPart(string sourceString, bool isSpecialString)
        {
            int counterBeginCurlyBrace = 0, counterEndCurlyBrace = 0;
            int counterSlash = CountSubstring(sourceString, "\"") % 2;

            if (!isSpecialString)
            {
                counterBeginCurlyBrace = CountSubstring(sourceString, "{");
                counterEndCurlyBrace = CountSubstring(sourceString, "}");
            }

            return counterBeginCurlyBrace == counterEndCurlyBrace & counterSlash == 0;
        }
        private static int GetDelimiterIndex(string sourceString, bool isSpecialString, LogParserModeLGF mode, int partIndex, out bool forceAddResult)
        {
            forceAddResult = false;

            if (mode == LogParserModeLGF.EventLogRow && partIndex == _commentPartNumber)
            {
                var matchResult = _regexEndOfComment.Match(sourceString);
                forceAddResult = true;
                return matchResult.Index + 1;
            }
            
            if (mode == LogParserModeLGF.EventLogRow && partIndex == _dataPartNumber)
            {
                var matchResult = _regexEndOfData.Match(sourceString);
                forceAddResult = true;
                return matchResult.Index + 1;
            }
            
            if (mode == LogParserModeLGF.EventLogRow && partIndex == _dataPresentationPartNumber)
            {
                var matchResult = _regexEndOfDataPresentation.Match(sourceString);
                forceAddResult = true;
                return matchResult.Index;
            }
            
            if (isSpecialString)
            {
                return sourceString.IndexOf("\",", StringComparison.Ordinal) + 1;
            }
            
            return sourceString.IndexOf(",", StringComparison.Ordinal);
        }
        private static int CountSubstring(string sourceString, string sourceSubstring)
        {
            int countSubstring = (sourceString.Length - sourceString.Replace(sourceSubstring, "").Length) / sourceSubstring.Length;

            return countSubstring;
        }

        #endregion
    }
}
