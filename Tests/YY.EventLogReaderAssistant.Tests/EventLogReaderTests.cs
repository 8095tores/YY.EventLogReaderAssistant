﻿using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using Xunit;
using YY.EventLogReaderAssistant.EventArguments;
using YY.EventLogReaderAssistant.Helpers;
using YY.EventLogReaderAssistant.Models;

namespace YY.EventLogReaderAssistant.Tests
{
    [CollectionDefinition("Event Log Test", DisableParallelization = true)]

    [Collection("Event Log Test")]
    public class EventLogReaderTests
    {
        #region Private Member Variables

        private readonly string _sampleDatabaseFileLGF;
        private readonly string _sampleDatabaseFileLgd;
        private readonly string _sampleDatabaseFileLgdReadReferencesIfChanged;
        private readonly string _sampleDatabaseFileLgdReadWithDelay;
        private readonly string _sampleDatabaseFileLGFBrokenFile;
        private readonly string _sampleDatabaseFileLGFOnChanging;
        private readonly string _sampleDatabaseFileLGFReadWithDelay;

        private OnErrorEventArgs _lastErrorData;
        private long _eventCountSuccess;
        private long _eventCountError;

        public OnErrorEventArgs LastErrorData { get => _lastErrorData; set => _lastErrorData = value; }

        #endregion

        #region Constructor

        public EventLogReaderTests()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            var sampleDataDirectory = Path.Combine(currentDirectory, "SampleData");
            _sampleDatabaseFileLGF = Path.Combine(sampleDataDirectory, "LGFFormatEventLog", "1Cv8.lgf");
            _sampleDatabaseFileLgd = Path.Combine(sampleDataDirectory, "SQLiteFormatEventLog", "1Cv8.lgd");
            _sampleDatabaseFileLgdReadReferencesIfChanged = Path.Combine(
                sampleDataDirectory, "SQLiteFormatEventLog", "1Cv8_ReadRefferences_IfChanged_Test.lgd");
            _sampleDatabaseFileLgdReadWithDelay = Path.Combine(sampleDataDirectory, "SQLiteFormatEventLogReadWithDelay", "1Cv8.lgd");
            _sampleDatabaseFileLGFBrokenFile = Path.Combine(sampleDataDirectory, "LGFFormatEventLogBrokenFile", "1Cv8.lgf");
            _sampleDatabaseFileLGFOnChanging = Path.Combine(sampleDataDirectory, "LGFFormatEventLogOnChanging", "1Cv8.lgf");
            _sampleDatabaseFileLGFReadWithDelay = Path.Combine(sampleDataDirectory, "LGFFormatEventLogReadWithDelay", "1Cv8.lgf");

            _eventCountSuccess = 0;
            _eventCountError = 0;
        }

        #endregion

        #region Public Methods

        [Fact]
        public void GetCount_NewFormat_LGD_Test()
        {
            GetCount_Test(_sampleDatabaseFileLgd);
        }
        [Fact]
        public void GetCount_OldFormat_LGF_Test()
        {
            GetCount_Test(_sampleDatabaseFileLGF);
        }
        [Fact]
        public void GetAndSetPosition_NewFormat_LGD_Test()
        {
            GetAndSetPosition_Test(_sampleDatabaseFileLgd);
        }
        [Fact]
        public void GetAndSetPosition_OldFormat_LGF_Test()
        {
            GetAndSetPosition_Test(_sampleDatabaseFileLGF);
        }
        [Fact]
        public void SetBadStreamPosition_Addition_OldFormat_LGF_Test()
        {
            SetBadStreamPosition_LGF_Format_Test(_sampleDatabaseFileLGF, 3);
        }
        [Fact]
        public void SetBadStreamPosition_Subtraction_OldFormat_LGF_Test()
        {
            SetBadStreamPosition_LGF_Format_Test(_sampleDatabaseFileLGF, -3);
        }
        [Fact]
        public void OnErrorHandlerBrokenEvent_OldFormat_LGF_Test()
        {
            _eventCountSuccess = 0;
            _eventCountError = 0;
            long totalCount;

            using (EventLogReader reader = EventLogReader.CreateReader(_sampleDatabaseFileLGFBrokenFile))
            {
                totalCount = reader.Count();

                reader.OnErrorEvent += Reader_OnErrorEvent;
                reader.AfterReadEvent += Reader_AfterReadEvent;

                bool dataExist;
                do
                {
                    dataExist = reader.Read();
                } while (dataExist);       
            }

            Assert.Equal(totalCount, (_eventCountSuccess + _eventCountError));
            Assert.Equal(1, _eventCountError);
            Assert.Equal(4, _eventCountSuccess);
        }
        [Fact]
        public void CountLogFiles_NewFormat_LGD_Test()
        {
            GetCountLogFiles_Test(_sampleDatabaseFileLgd);
        }
        [Fact]
        public void CountLogFiles_OldFormat_LGF_Test()
        {
            GetCountLogFiles_Test(_sampleDatabaseFileLGF);
        }
        [Fact]
        public void GoToEvent_NewFormat_LGD_Test()
        {
            GoToEvent_Test(_sampleDatabaseFileLgd);
        }
        [Fact]
        public void GoToEvent_OldFormat_LGF_Test()
        {
            GoToEvent_Test(_sampleDatabaseFileLGF);
        }
        [Fact]
        public void ReadReferences_IfChanged_OldFormat_LGF_Test()
        {
            ReadRefferences_IfChanged_Test(_sampleDatabaseFileLGF);
        }
        [Fact]
        public void ReadReferences_IfChanged_NewFormat_LGD_Test()
        {
            ReadRefferences_IfChanged_Test(_sampleDatabaseFileLgdReadReferencesIfChanged);
        }
        [Fact]
        public void CheckIdAfterSetPosition_OldFormat_LGF_Test()
        {
            CheckIdAfterSetPosition_Test(_sampleDatabaseFileLGF);
        }
        [Fact]
        public void CheckIdAfterSetPosition_NewFormat_LGD_Test()
        {
            CheckIdAfterSetPosition_Test(_sampleDatabaseFileLgd);
        }
        [Fact]
        public void CheckIdAfterGoToEvent_OldFormat_LGF_Test()
        {
            CheckIdAfterGoToEvent_Test(_sampleDatabaseFileLGF);
        }
        [Fact]
        public void CheckIdAfterGoToEvent_NewFormat_LGD_Test()
        {
            CheckIdAfterGoToEvent_Test(_sampleDatabaseFileLgd);
        }
        [Fact]
        public void ReadOnChanging_OldFormat_LFG_Test()
        {
            DateTime newLogRecordPeriod = DateTime.Now;
            RowData lastRowData;

            using (EventLogReader reader = EventLogReader.CreateReader(_sampleDatabaseFileLGFOnChanging))
            {
                reader.SetReadDelay(0);
                long totalEvents = reader.Count();
                long currentEventNumber = 0;
                
                bool dataExist;
                do
                {
                    dataExist = reader.Read();
                    lastRowData = reader.CurrentRow;
                    currentEventNumber += 1;
                    
                    if(totalEvents == currentEventNumber)
                    {
                        string descriptionNewEvent = "Новое событие в процессе чтения!";
                        string newLogRecordPeriodAsString = newLogRecordPeriod.ToString("yyyyMMddHHmmss");

                        using (StreamWriter sw = File.AppendText(reader.CurrentFile))
                        {
                            sw.WriteLine(",");
                            sw.WriteLine($"{{{newLogRecordPeriodAsString},N,");
                            sw.WriteLine($"{{0,0}},1,1,2,2,3,N,\"{descriptionNewEvent}\",3,");
                            sw.WriteLine($"{{\"S\",\"{descriptionNewEvent}\"}},\"\",1,1,0,2,0,");
                            sw.WriteLine("{0}");
                            sw.WriteLine("}");
                        }

                        reader.Read();
                        lastRowData = reader.CurrentRow;
                        break;
                    }
                } while (dataExist);
            }

            Assert.NotNull(lastRowData);
            Assert.Equal(newLogRecordPeriod.Date, lastRowData.Period.Date);
            Assert.Equal(newLogRecordPeriod.Hour, lastRowData.Period.Hour);
            Assert.Equal(newLogRecordPeriod.Minute, lastRowData.Period.Minute);
            Assert.Equal(newLogRecordPeriod.Second, lastRowData.Period.Second);
        }
        [Fact]
        public void ReadOnChanging_WithReadDelay_OldFormat_LFG_Test()
        {
            DateTime newLogRecordPeriod = DateTime.Now.AddHours(1);
            RowData lastRowData;

            using (EventLogReader reader = EventLogReader.CreateReader(_sampleDatabaseFileLGFReadWithDelay))
            {
                long totalEvents = reader.Count();
                long currentEventNumber = 0;

                reader.SetReadDelay(1000);
                bool dataExist;
                do
                {
                    dataExist = reader.Read();
                    lastRowData = reader.CurrentRow;
                    currentEventNumber += 1;

                    if (totalEvents == currentEventNumber)
                    {
                        string descriptionNewEvent = "Новое событие в процессе чтения!";
                        string newLogRecordPeriodAsString = newLogRecordPeriod.ToString("yyyyMMddHHmmss");

                        using (StreamWriter sw = File.AppendText(reader.CurrentFile))
                        {
                            sw.WriteLine(",");
                            sw.WriteLine($"{{{newLogRecordPeriodAsString},N,");
                            sw.WriteLine($"{{0,0}},1,1,2,2,3,N,\"{descriptionNewEvent}\",3,");
                            sw.WriteLine($"{{\"S\",\"{descriptionNewEvent}\"}},\"\",1,1,0,2,0,");
                            sw.WriteLine("{0}");
                            sw.WriteLine("}");
                        }

                        reader.Read();
                        lastRowData = reader.CurrentRow;
                        break;
                    }
                } while (dataExist);
            }
                        
            Assert.Null(lastRowData);
        }
        [Fact]
        public void ReadOnChanging_WithReadDelay_NewFormat_LGD_Test()
        {
            DateTime newLogRecordPeriod = DateTime.Now.AddHours(1);
            RowData lastRowData = null;

            #region addNewRecord

            string lgdConnectionString = SQLiteExtensions.GetConnectionString(_sampleDatabaseFileLgdReadWithDelay, false);
            using (SQLiteConnection connection = new SQLiteConnection(lgdConnectionString))
            {
                connection.Open();
                string queryText = String.Format(
                    "Select\n" +
                    "    el.RowId,\n" +
                    "    el.Date AS Date,\n" +
                    "    el.ConnectId,\n" +
                    "    el.Session,\n" +
                    "    el.TransactionStatus,\n" +
                    "    el.TransactionDate,\n" +
                    "    el.TransactionId,\n" +
                    "    el.UserCode AS UserCode,\n" +
                    "    el.ComputerCode AS ComputerCode,\n" +
                    "    el.appCode AS ApplicationCode,\n" +
                    "    el.eventCode AS EventCode,\n" +
                    "    el.primaryPortCode AS PrimaryPortCode,\n" +
                    "    el.secondaryPortCode AS SecondaryPortCode,\n" +
                    "    el.workServerCode AS WorkServerCode,\n" +
                    "    el.Severity AS SeverityCode,\n" +
                    "    el.Comment AS Comment,\n" +
                    "    el.Data AS Data,\n" +
                    "    el.DataPresentation AS DataPresentation,\n" +
                    "    elm.metadataCode AS MetadataCode\n" +
                    "From\n" +
                    "    EventLog el\n" +
                    "    left join EventLogMetadata elm on el.RowId = elm.eventLogID\n" +
                    "    left join MetadataCodes mc on elm.metadataCode = mc.code\n" +
                    "Where RowID = (SELECT MAX(RowID) from EventLog)\n");

                long rowId = 0, connectId = 0, session = 0,
                        transactionStatus = 0, transactionDate = 0, transactionId = 0,
                        user = 0, computer = 0, application = 0, @event = 0, primaryPort = 0,
                        secondaryPort = 0, workServer = 0, severity = 0;
                string comment = string.Empty, data = string.Empty, dataPresentation = string.Empty;

                using (SQLiteCommand sqliteCmd = new SQLiteCommand(queryText, connection))
                {
                    using (SQLiteDataReader sqliteReader = sqliteCmd.ExecuteReader())
                    {
                        while (sqliteReader.Read())
                        {
                            rowId = sqliteReader.GetInt64OrDefault(0);
                            connectId = sqliteReader.GetInt64OrDefault(2);
                            session = sqliteReader.GetInt64OrDefault(3);
                            transactionStatus = sqliteReader.GetInt64OrDefault(4);
                            transactionDate = sqliteReader.GetInt64OrDefault(5);
                            transactionId = sqliteReader.GetInt64OrDefault(6);
                            user = sqliteReader.GetInt64OrDefault(7);
                            computer = sqliteReader.GetInt64OrDefault(8);
                            application = sqliteReader.GetInt64OrDefault(9);
                            @event = sqliteReader.GetInt64OrDefault(10);
                            primaryPort = sqliteReader.GetInt64OrDefault(11);
                            secondaryPort = sqliteReader.GetInt64OrDefault(12);
                            workServer = sqliteReader.GetInt64OrDefault(13);
                            severity = sqliteReader.GetInt64OrDefault(14);
                            comment = sqliteReader.GetStringOrDefault(15);
                            data = sqliteReader.GetStringOrDefault(16);
                            dataPresentation = sqliteReader.GetStringOrDefault(17);
                        }
                    }
                }

                string queryInsertLog =
                    "INSERT INTO EventLog " +
                    "(" +
                    "   RowId, " +
                    "   Date, " +
                    "   ConnectId, " +
                    "   Session, " +
                    "   TransactionStatus, " +
                    "   TransactionDate, " +
                    "   TransactionId, " +
                    "   UserCode, " +
                    "   ComputerCode, " +
                    "   appCode, " +
                    "   eventCode, " +
                    "   primaryPortCode, " +
                    "   secondaryPortCode, " +
                    "   workServerCode, " +
                    "   Severity, " +
                    "   Comment, " +
                    "   Data, " +
                    "   DataPresentation " +
                    ") " +
                    "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)";

                using (SQLiteCommand insertSql = new SQLiteCommand(queryInsertLog, connection))
                {
                    long newRowId = rowId + 1;
                    long newPeriod = newLogRecordPeriod.ToLongDateTimeFormat();

                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, newRowId));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, newPeriod));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, connectId));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, session));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, transactionStatus));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, transactionDate));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, transactionId));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, user));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, computer));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, application));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, @event));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, primaryPort));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, secondaryPort));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, workServer));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, severity));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.String, comment));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.String, data));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.String, dataPresentation));
                    insertSql.ExecuteNonQuery();
                }
            }

            #endregion

            using (EventLogReader reader = EventLogReader.CreateReader(_sampleDatabaseFileLgdReadWithDelay))
            {
                bool dataExist;
                do
                {
                    dataExist = reader.Read();
                    if(dataExist)
                        lastRowData = reader.CurrentRow;
                } while (dataExist);
            }

            Assert.NotNull(lastRowData);
            Assert.NotEqual(newLogRecordPeriod, lastRowData.Period);
        }

        #endregion

        #region Private Methods

        private void CheckIdAfterSetPosition_Test(string eventLogPath)
        {
            int checkIdSteps = 5;
            RowData rowAfterSteps;
            EventLogPosition positionAfterSteps;
            RowData rowAfterSetPosition = null;

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                for (int i = 0; i < checkIdSteps; i++)                
                    reader.Read();
                rowAfterSteps = reader.CurrentRow;
                positionAfterSteps = reader.GetCurrentPosition();

                reader.Reset();
                reader.SetCurrentPosition(positionAfterSteps);
                if (reader.Read())
                {
                    reader.GetCurrentPosition();
                    rowAfterSetPosition = reader.CurrentRow;
                }
            }

            Assert.NotNull(rowAfterSteps);
            Assert.NotNull(rowAfterSetPosition);
            Assert.Equal(rowAfterSteps.RowId, rowAfterSetPosition.RowId - 1);
        }
        private void CheckIdAfterGoToEvent_Test(string eventLogPath)
        {
            int checkIdSteps = 5;
            RowData rowAfterSteps;
            long eventNumberAfterSteps;
            RowData rowAfterGoToEvent = null;
            long eventNumberAfterGoToEvent = -1;

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                for (int i = 0; i < checkIdSteps; i++)
                    reader.Read();
                rowAfterSteps = reader.CurrentRow;
                eventNumberAfterSteps = reader.CurrentFileEventNumber;

                reader.Reset();
                reader.GoToEvent(eventNumberAfterSteps);
                if (reader.Read())
                {
                    eventNumberAfterGoToEvent = reader.CurrentFileEventNumber;
                    rowAfterGoToEvent = reader.CurrentRow;
                }
            }

            Assert.NotNull(rowAfterSteps);
            Assert.NotNull(rowAfterGoToEvent);
            Assert.NotEqual(-1, eventNumberAfterSteps);
            Assert.NotEqual(-1, eventNumberAfterGoToEvent);
            Assert.Equal(rowAfterSteps.RowId, rowAfterGoToEvent.RowId - 1);
        }
        private void GetCount_Test(string eventLogPath)
        {
            long countRecords;
            long countRecordsStepByStep = 0;            

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                countRecords = reader.Count();

                while (reader.Read())
                {
                    countRecordsStepByStep += 1;
                }
            }

            Assert.NotEqual(0, countRecords);
            Assert.NotEqual(0, countRecordsStepByStep);
            Assert.Equal(countRecords, countRecordsStepByStep);
        }      
        private void GetAndSetPosition_Test(string eventLogPath)
        {
            long countRecords;
            long countRecordsStepByStep = 0;
            long countRecordsStepByStepAfterSetPosition = 0;

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                countRecords = reader.Count();

                while (reader.Read())
                {
                    countRecordsStepByStep += 1;
                }

                reader.Reset();
                EventLogPosition position = reader.GetCurrentPosition();

                bool dataExist;
                do
                {
                    dataExist = reader.Read();
                } while (dataExist);

                reader.SetCurrentPosition(position);
                while (reader.Read())
                {
                    countRecordsStepByStepAfterSetPosition += 1;
                }
            }

            Assert.NotEqual(0, countRecords);
            Assert.NotEqual(0, countRecordsStepByStep);
            Assert.NotEqual(0, countRecordsStepByStepAfterSetPosition);
            Assert.Equal(countRecords, countRecordsStepByStep);
            Assert.Equal(countRecords, countRecordsStepByStepAfterSetPosition);
        }
        private void GetCountLogFiles_Test(string eventLogPath)
        {
            long countLogFiles = 0;

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                reader.Reset();

                if (reader is EventLogLGFReader readerLGF)
                {
                    while (readerLGF.CurrentFile != null)
                    {
                        reader.NextFile();
                        countLogFiles += 1;
                    }
                }
                else if (reader is EventLogLGDReader)
                {
                    countLogFiles = 1;
                }
            }

            Assert.NotEqual(0, countLogFiles);
        }
        private void GoToEvent_Test(string eventLogPath)
        {
            string dataAfterGoEvent = string.Empty;
            string dataAfterSetPosition = string.Empty;

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                reader.GoToEvent(5);
                EventLogPosition eventPosition = reader.GetCurrentPosition();
                if (reader.Read())
                    dataAfterGoEvent = reader.CurrentRow.Data;

                reader.Reset();

                reader.SetCurrentPosition(eventPosition);
                if (reader.Read())
                    dataAfterSetPosition = reader.CurrentRow.Data;
            }

            Assert.Equal(dataAfterGoEvent, dataAfterSetPosition);
        }
        private void ReadRefferences_IfChanged_Test(string eventLogPath)
        {
            DateTime lastReadReferencesDateBeforeRead;
            DateTime lastReadReferencesDate;

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                lastReadReferencesDateBeforeRead = reader.ReferencesReadDate;
                Thread.Sleep(2000);

                if (reader is EventLogLGFReader lgfReader)
                {
                    #region LGF

                    using StreamWriter sw = File.AppendText(lgfReader.CurrentFile);
                    string descriptionNewEvent = "Новое событие в процессе чтения!";
                    DateTime newLogRecordPeriod = DateTime.Now;
                    string newLogRecordPeriodAsString = newLogRecordPeriod.ToString("yyyyMMddHHmmss");

                    sw.WriteLine(",");
                    sw.WriteLine($"{{{newLogRecordPeriodAsString},N,");
                    sw.WriteLine($"{{0,0}},1,1,2,2,3,N,\"{descriptionNewEvent}\",3,");
                    sw.WriteLine($"{{\"S\",\"{descriptionNewEvent}\"}},\"\",1,1,0,2,0,");
                    sw.WriteLine("{0}");
                    sw.WriteLine("}");

                    #endregion
                }
                else if (reader is EventLogLGDReader)
                {
                    #region LGD

                    string lgdConnectionString = SQLiteExtensions.GetConnectionString(eventLogPath, false);
                    using SQLiteConnection connection = new SQLiteConnection(lgdConnectionString);
                    connection.Open();
                    string queryText = String.Format(
                        "Select\n" +
                        "    el.RowId,\n" +
                        "    el.Date AS Date,\n" +
                        "    el.ConnectId,\n" +
                        "    el.Session,\n" +
                        "    el.TransactionStatus,\n" +
                        "    el.TransactionDate,\n" +
                        "    el.TransactionId,\n" +
                        "    el.UserCode AS UserCode,\n" +
                        "    el.ComputerCode AS ComputerCode,\n" +
                        "    el.appCode AS ApplicationCode,\n" +
                        "    el.eventCode AS EventCode,\n" +
                        "    el.primaryPortCode AS PrimaryPortCode,\n" +
                        "    el.secondaryPortCode AS SecondaryPortCode,\n" +
                        "    el.workServerCode AS WorkServerCode,\n" +
                        "    el.Severity AS SeverityCode,\n" +
                        "    el.Comment AS Comment,\n" +
                        "    el.Data AS Data,\n" +
                        "    el.DataPresentation AS DataPresentation,\n" +
                        "    elm.metadataCode AS MetadataCode\n" +
                        "From\n" +
                        "    EventLog el\n" +
                        "    left join EventLogMetadata elm on el.RowId = elm.eventLogID\n" +
                        "    left join MetadataCodes mc on elm.metadataCode = mc.code\n" +
                        "Where RowID = (SELECT MAX(RowID) from EventLog)\n");
                    using SQLiteCommand sqliteCmd = new SQLiteCommand(queryText, connection);
                    long rowId = 0, connectId = 0, session = 0,
                        transactionStatus = 0, transactionDate = 0, transactionId = 0,
                        user = 0, computer = 0, application = 0, @event = 0, primaryPort = 0,
                        secondaryPort = 0, workServer = 0, severity = 0;
                    string comment = string.Empty, data = string.Empty, dataPresentation = string.Empty;

                    using (SQLiteDataReader sqliteReader = sqliteCmd.ExecuteReader())
                    {
                        while (sqliteReader.Read())
                        {
                            rowId = sqliteReader.GetInt64OrDefault(0);
                            connectId = sqliteReader.GetInt64OrDefault(2);
                            session = sqliteReader.GetInt64OrDefault(3);
                            transactionStatus = sqliteReader.GetInt64OrDefault(4);
                            transactionDate = sqliteReader.GetInt64OrDefault(5);
                            transactionId = sqliteReader.GetInt64OrDefault(6);
                            user = sqliteReader.GetInt64OrDefault(7);
                            computer = sqliteReader.GetInt64OrDefault(8);
                            application = sqliteReader.GetInt64OrDefault(9);
                            @event = sqliteReader.GetInt64OrDefault(10);
                            primaryPort = sqliteReader.GetInt64OrDefault(11);
                            secondaryPort = sqliteReader.GetInt64OrDefault(12);
                            workServer = sqliteReader.GetInt64OrDefault(13);
                            severity = sqliteReader.GetInt64OrDefault(14);
                            comment = sqliteReader.GetStringOrDefault(15);
                            data = sqliteReader.GetStringOrDefault(16);
                            dataPresentation = sqliteReader.GetStringOrDefault(17);
                            sqliteReader.GetInt64OrDefault(18);
                        }
                    }

                    string queryInsertLog =
                        "INSERT INTO EventLog " +
                        "(" +
                        "   RowId, " +
                        "   Date, " +
                        "   ConnectId, " +
                        "   Session, " +
                        "   TransactionStatus, " +
                        "   TransactionDate, " +
                        "   TransactionId, " +
                        "   UserCode, " +
                        "   ComputerCode, " +
                        "   appCode, " +
                        "   eventCode, " +
                        "   primaryPortCode, " +
                        "   secondaryPortCode, " +
                        "   workServerCode, " +
                        "   Severity, " +
                        "   Comment, " +
                        "   Data, " +
                        "   DataPresentation " +
                        ") " +
                        "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)";
                    
                    using SQLiteCommand insertSql = new SQLiteCommand(queryInsertLog, connection);
                    long newRowId = rowId + 1;
                    long newPeriod = DateTime.Now.ToLongDateTimeFormat();

                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, newRowId));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, newPeriod));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, connectId));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, session));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, transactionStatus));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, transactionDate));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, transactionId));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, user));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, computer));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, application));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, @event));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, primaryPort));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, secondaryPort));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, workServer));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.Int64, severity));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.String, comment));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.String, data));
                    insertSql.Parameters.Add(new SQLiteParameter(DbType.String, dataPresentation));
                    insertSql.ExecuteNonQuery();

                    #endregion
                }

                bool dataExist;
                do
                {
                    dataExist = reader.Read();
                } while (dataExist);
                lastReadReferencesDate = reader.ReferencesReadDate;
            }

            Assert.NotEqual(DateTime.MinValue, lastReadReferencesDate);
            Assert.NotEqual(DateTime.MinValue, lastReadReferencesDateBeforeRead);
            Assert.True(lastReadReferencesDateBeforeRead < lastReadReferencesDate);
        }
        private void SetBadStreamPosition_LGF_Format_Test(string eventLogPath, long changeStreamPosition)
        {
            long correctRowId;
            long fixedRowId;

            using (EventLogReader reader = EventLogReader.CreateReader(eventLogPath))
            {
                reader.GoToEvent(10);
                EventLogPosition position = reader.GetCurrentPosition();
                reader.Read();
                correctRowId = reader.CurrentRow.RowId;

                if (position.StreamPosition != null)
                {
                    long wrongStreamPosition = (long)position.StreamPosition + changeStreamPosition;
                    reader.SetCurrentPosition(new EventLogPosition(
                        position.EventNumber,
                        position.CurrentFileReferences,
                        position.CurrentFileData,
                        wrongStreamPosition));
                }

                reader.Read();
                fixedRowId = reader.CurrentRow.RowId;
            }

            Assert.Equal(correctRowId, fixedRowId);
        }

        #endregion

        #region Events

        private void Reader_AfterReadEvent(EventLogReader sender, AfterReadEventArgs args)
        {
            _eventCountSuccess += 1;
        }
        private void Reader_OnErrorEvent(EventLogReader sender, OnErrorEventArgs args)
        {
            LastErrorData = args;
            _eventCountError += 1;
        }

        #endregion
    }
}
