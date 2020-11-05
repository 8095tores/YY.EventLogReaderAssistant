﻿using System;
using System.Data.SQLite;
using System.Globalization;
using System.Text.RegularExpressions;
using YY.EventLogReaderAssistant.Helpers;

namespace YY.EventLogReaderAssistant.Models
{
    [Serializable]
    public class RowData
    {
        private static readonly Regex _regexDataUuid;

        static RowData()
        {
            _regexDataUuid = new Regex(@"[\d]+:[\dA-Za-zА-Яа-я]{32}");
        }

        #region Public Members

        public DateTime Period { get; set; }
        public long RowId { get; set; }
        public Severity Severity { get; set; }
        public long? ConnectId { get; set; }
        public long? Session { get; set; }
        public TransactionStatus TransactionStatus { get; set; }
        public DateTime? TransactionDate { get; set; }
        public long? TransactionId { get; set; }
        public Users User { get; set; }
        public Computers Computer { get; set; }
        public Applications Application { get; set; }
        public Events Event { get; set; }
        public string Comment { get; set; }
        public Metadata Metadata { get; set; }
        public string Data { get; set; }
        public string DataUuid { get; set; }
        public string DataPresentation { get; set; }
        public WorkServers WorkServer { get; set; }
        public PrimaryPorts PrimaryPort { get; set; }
        public SecondaryPorts SecondaryPort { get; set; }

        #endregion

        #region Public Methods

        internal void FillBySqliteReader(EventLogLGDReader reader, SQLiteDataReader sqlReader)
        {
            DateTime rowPeriod = sqlReader.GetInt64OrDefault(1).ToDateTimeFormat();
            RowId = sqlReader.GetInt64OrDefault(0);
            Period = rowPeriod;
            ConnectId = sqlReader.GetInt64OrDefault(2);
            Session = sqlReader.GetInt64OrDefault(3);
            TransactionStatus = reader.GetTransactionStatus(sqlReader.GetInt64OrDefault(4));
            TransactionDate = sqlReader.GetInt64OrDefault(5).ToNullableDateTimeElFormat();
            TransactionId = sqlReader.GetInt64OrDefault(6);
            User = reader.GetUserByCode(sqlReader.GetInt64OrDefault(7));
            Computer = reader.GetComputerByCode(sqlReader.GetInt64OrDefault(8));
            Application = reader.GetApplicationByCode(sqlReader.GetInt64OrDefault(9));
            Event = reader.GetEventByCode(sqlReader.GetInt64OrDefault(10));
            PrimaryPort = reader.GetPrimaryPortByCode(sqlReader.GetInt64OrDefault(11));
            SecondaryPort = reader.GetSecondaryPortByCode(sqlReader.GetInt64OrDefault(12));
            WorkServer = reader.GetWorkServerByCode(sqlReader.GetInt64OrDefault(13));
            Severity = reader.GetSeverityByCode(sqlReader.GetInt64OrDefault(14));
            Comment = sqlReader.GetStringOrDefault(15);
            Data = sqlReader.GetStringOrDefault(16).FromWin1251ToUtf8();
            DataUuid = GetDataUuid(Data);
            DataPresentation = sqlReader.GetStringOrDefault(17);
            Metadata = reader.GetMetadataByCode(sqlReader.GetInt64OrDefault(18));
        }

        internal void FillByStringParsedData(EventLogLGFReader reader, string[] parseResult)
        {
            string transactionSourceString = parseResult[2].RemoveBraces();

            RowId = reader.CurrentFileEventNumber;
            Period = DateTime.ParseExact(parseResult[0], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            TransactionStatus = reader.GetTransactionStatus(parseResult[1]);
            TransactionDate = GetTransactionDate(transactionSourceString);
            TransactionId = GetTransactionId(transactionSourceString);
            User = reader.GetUserByCode(parseResult[3]);
            Computer = reader.GetComputerByCode(parseResult[4]);
            Application = reader.GetApplicationByCode(parseResult[5]);
            ConnectId = parseResult[6].ToInt32();
            Event = reader.GetEventByCode(parseResult[7]);
            Severity = reader.GetSeverityByCode(parseResult[8]);
            Comment = parseResult[9].RemoveQuotes();
            Metadata = reader.GetMetadataByCode(parseResult[10]);
            Data = GetData(parseResult[11]);
            DataPresentation = parseResult[12].RemoveQuotes();
            WorkServer = reader.GetWorkServerByCode(parseResult[13]);
            PrimaryPort = reader.GetPrimaryPortByCode(parseResult[14]);
            SecondaryPort = reader.GetSecondaryPortByCode(parseResult[15]);
            Session = parseResult[16].ToInt64();
            DataUuid = GetDataUuid(Data);
        }

        #endregion

        #region Private Members

        private string GetDataUuid(string sourceData)
        {
            string dataUuid = string.Empty;

            MatchCollection matches = _regexDataUuid.Matches(sourceData);
            if (matches.Count > 0)
            {
                string[] dataPartsUuid = sourceData.Split(':');
                dataUuid = dataPartsUuid.Length == 2 ? dataPartsUuid[1].Replace("}", string.Empty) : string.Empty;
            }

            return dataUuid;
        }
        private DateTime? GetTransactionDate(string sourceString)
        {
            DateTime? transactionDate = null;

            long transDate = sourceString.Substring(0, sourceString.IndexOf(",", StringComparison.Ordinal)).From16To10();
            try
            {
                if (transDate != 0) transactionDate = new DateTime().AddSeconds((double)transDate / 10000);
            }
            catch
            {
                transactionDate = null;
            }

            return transactionDate;
        }
        private long? GetTransactionId(string sourceString)
        {
            long? transactionId = sourceString.Substring(sourceString.IndexOf(",", StringComparison.Ordinal) + 1).From16To10();

            return transactionId;
        }
        private string GetData(string sourceString)
        {
            string data = sourceString;

            if (data == "{\"U\"}")
                data = string.Empty;

            else if (data.StartsWith("{"))
            {
                string[] parsedObjects = LogParserLGF.ParseEventLogString(data);
                if (parsedObjects != null && parsedObjects.Length == 2)
                {
                    if (parsedObjects[0] == "\"S\"" || parsedObjects[0] == "\"R\"")
                        data = parsedObjects[1].RemoveQuotes();
                }
            }

            return data;
        }

        #endregion
    }
}
