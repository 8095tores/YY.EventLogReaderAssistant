using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using YY.EventLogReaderAssistant.EventArguments;
using YY.EventLogReaderAssistant.Helpers;
using YY.EventLogReaderAssistant.Models;

[assembly: InternalsVisibleTo("YY.EventLogReaderAssistant.Tests")]
namespace YY.EventLogReaderAssistant
{
    internal sealed class EventLogLGFReader : EventLogReader
    {
        #region Private Member Variables

        private const long DefaultBeginLineForLgf = 3;
        private int _indexCurrentFile;
        private string[] _logFilesWithData;
        private long _eventCount = -1;
        private readonly int _maxReadAttempts = 3;
        private readonly int _delayReadAttemptsMs = 1000;
        private int _readAttempts;

        StreamReader _stream;
        readonly StringBuilder _eventSource;

        private LogParserLGF _logParser;
        private LogParserLGF LogParser => _logParser ??= new LogParserLGF(this);

        #endregion

        #region Public Properties

        public override string CurrentFile
        {
            get
            {
                if (!LogFileByIndexExist())
                    return null;
                else
                    return _logFilesWithData[_indexCurrentFile];
            }
        }

        #endregion

        #region Constructor

        internal EventLogLGFReader(string logFilePath) : base(logFilePath)
        {
            _indexCurrentFile = 0;
            UpdateEventLogFilesList();
            _eventSource = new StringBuilder();            
        }

        #endregion

        #region Public Methods

        public override bool Read()
        {
            bool output = false;

            try
            {
                if (!InitializeReadFileStream())
                    return false;

                RaiseBeforeReadFileEvent(out bool cancelBeforeReadFile);
                if (cancelBeforeReadFile)
                {
                    NextFile();
                    return Read();
                }
                bool newLine = true, textBlockOpen = false, readFinished = false;
                int countBracket = 0;
                EventLogPosition positionBeforeRead = GetCurrentPosition();
                _readAttempts = 1;

                while (true)
                {
                    while (_readAttempts > 0 && _readAttempts <= 3)
                    {
                        string sourceData = ReadSourceDataFromStream();
                        if (sourceData == null)
                        {
                            NextFile();
                            output = Read();
                            _readAttempts = 0;
                            readFinished = true;
                            break;
                        }

                        AddNewLineToSource(sourceData, newLine);

                        if (LogParserLGF.ItsEndOfEvent(sourceData, ref countBracket, ref textBlockOpen))
                        {
                            _currentFileEventNumber += 1;
                            string preparedSourceData = _eventSource.ToString().Trim();

                            RaiseBeforeRead(new BeforeReadEventArgs(preparedSourceData, _currentFileEventNumber));

                            try
                            {
                                RowData eventData = ReadRowData(preparedSourceData);
                                _currentRow = eventData;
                                _readAttempts = 0;
                                RaiseAfterRead(new AfterReadEventArgs(_currentRow, _currentFileEventNumber));
                                output = true;
                                readFinished = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                _readAttempts += 1;
                                _currentRow = null;
                                if (_readAttempts > _maxReadAttempts)
                                {
                                    readFinished = true;
                                    RaiseOnError(new OnErrorEventArgs(ex, preparedSourceData, false));
                                }
                                else
                                {
                                    readFinished = false;
                                    _eventSource.Clear();
                                    SetCurrentPosition(positionBeforeRead);
                                    Thread.Sleep(_delayReadAttemptsMs);
                                }

                                output = true;
                                break;
                            }
                        }
                        newLine = false;
                    }

                    if(readFinished) break;
                }
            }
            catch (Exception ex)
            {
                RaiseOnError(new OnErrorEventArgs(ex, null, true));
                _currentRow = null;
                output = false;
            }

            return output;
        }
        public override bool GoToEvent(long eventNumber)
        {
            Reset();

            int fileIndex = -1;
            long currentLineNumber = -1;
            long currentEventNumber = 0;
            bool moved = false;

            foreach (string logFile in _logFilesWithData)
            {
                fileIndex += 1;
                currentLineNumber = -1;

                IEnumerable<string> allLines = File.ReadLines(logFile);
                foreach (string line in allLines)
                {
                    currentLineNumber += 1;
                    if(LogParserLGF.ItsBeginOfEvent(line))                    
                    {
                        currentEventNumber += 1;
                    }

                    if (currentEventNumber == eventNumber)
                    {
                        moved = true;
                        break;
                    }
                }

                if (currentEventNumber == eventNumber)
                {
                    moved = true;
                    break;
                }
            }           

            if (moved && fileIndex >= 0 && currentLineNumber >= 0)
            {
                InitializeStream(currentLineNumber, fileIndex);
                _eventCount = eventNumber - 1;
                _currentFileEventNumber = eventNumber;

                return true;
            }
            else
            {
                return false;
            }
        }
        public override EventLogPosition GetCurrentPosition()
        {
            return new EventLogPosition(
                _currentFileEventNumber, 
                _logFilePath, 
                CurrentFile, 
                GetCurrentFileStreamPosition());
        }
        public override void SetCurrentPosition(EventLogPosition newPosition)
        {
            if(ApplyEventLogPosition(newPosition) == false)
                return;
            
            InitializeStream(DefaultBeginLineForLgf, _indexCurrentFile);
            long beginReadPosition =_stream.GetPosition();
            long newStreamPosition = Math.Max(beginReadPosition, newPosition.StreamPosition ?? 0);

            long sourceStreamPosition = newStreamPosition;
            string currentFilePath = _logFilesWithData[_indexCurrentFile];            
            
            FixEventPosition(currentFilePath, ref newStreamPosition, sourceStreamPosition);

            if (newPosition.StreamPosition != null)
                SetCurrentFileStreamPosition(newStreamPosition);
        }
        public override long Count()
        {
            if(_eventCount < 0)
                _eventCount = GetEventCount();

            return _eventCount;
        }
        public override void Reset()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            _indexCurrentFile = 0;
            UpdateEventLogFilesList();
            _currentFileEventNumber = 0;
            _currentRow = null;
        }
        public override long FilesCount()
        {
            return _logFilesWithData.LongLength;
        }
        public override bool PreviousFile()
        {
            return ChangeFileStep(-1);
        }
        public override bool NextFile()
        {
            return ChangeFileStep(1);
        }
        public override bool LastFile()
        {
            while (NextFile()) { }

            return PreviousFile();
        }
        public override void Dispose()
        {
            base.Dispose();

            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }
        protected override void ReadEventLogReferences()
        {
            DateTime beginReadReferences = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _logTimeZoneInfo);
            _referencesData = new ReferencesData();

            var referencesInfo = LogParser.GetEventLogReferences();
            referencesInfo.ReadReferencesByType(_referencesData._users);
            referencesInfo.ReadReferencesByType(_referencesData._computers);
            referencesInfo.ReadReferencesByType(_referencesData._applications);
            referencesInfo.ReadReferencesByType(_referencesData._events);
            referencesInfo.ReadReferencesByType(_referencesData._metadata);
            referencesInfo.ReadReferencesByType(_referencesData._workServers);
            referencesInfo.ReadReferencesByType(_referencesData._primaryPorts);
            referencesInfo.ReadReferencesByType(_referencesData._secondaryPorts);

            _referencesReadDate = beginReadReferences;

            base.ReadEventLogReferences();
        }
        public long GetCurrentFileStreamPosition()
        {
            return _stream?.GetPosition() ?? 0;
        }
        public void SetCurrentFileStreamPosition(long position)
        {
            _stream?.SetPosition(position);
        }

        #endregion

        #region Private Methods

        private void UpdateEventLogFilesList()
        {
            _logFilesWithData = Directory
                .GetFiles(_logFileDirectoryPath, "*.lgp")
                .OrderBy(i => i)
                .ToArray();
        }
        private void AddNewLineToSource(string sourceData, bool newLine)
        {
            if (newLine)
                _eventSource.Append(sourceData);
            else
            {
                _eventSource.AppendLine();
                _eventSource.Append(sourceData);
            }
        }
        private string ReadSourceDataFromStream()
        {
            string sourceData = _stream.ReadLineWithoutNull();

            if (sourceData == "," && NextLineIsBeginEvent())
                sourceData = _stream.ReadLineWithoutNull();

            return sourceData;
        }
        private void RaiseBeforeReadFileEvent(out bool cancel)
        {
            BeforeReadFileEventArgs beforeReadFileArgs = new BeforeReadFileEventArgs(CurrentFile);
            if (_currentFileEventNumber == 0)
                RaiseBeforeReadFile(beforeReadFileArgs);

            cancel = beforeReadFileArgs.Cancel;
        }
        private bool InitializeReadFileStream()
        {
            if (_stream == null)
            {
                if (!LogFileByIndexExist())
                {
                    _currentRow = null;
                    return false;
                }

                InitializeStream(DefaultBeginLineForLgf, _indexCurrentFile);
                _currentFileEventNumber = 0;
            }
            _eventSource.Clear();

            return true;
        }
        private RowData ReadRowData(string sourceData)
        {
            RowData eventData = LogParser.Parse(sourceData);

            if (eventData != null && eventData.Period >= ReferencesReadDate)
            {
                ReadEventLogReferences();
                eventData = LogParser.Parse(sourceData);
            }

            return eventData;
        }
        private bool ApplyEventLogPosition(EventLogPosition position)
        {
            Reset();

            if (position == null)
                return false;

            if (position.CurrentFileReferences != _logFilePath)
                throw new Exception("Invalid data file with references");

            int indexOfFileData = Array.IndexOf(_logFilesWithData, position.CurrentFileData);
            if (indexOfFileData < 0)
                throw new Exception("Invalid data file");

            _indexCurrentFile = indexOfFileData;
            _currentFileEventNumber = position.EventNumber;

            return true;
        }
        private void InitializeStream(long linesToSkip, int fileIndex = 0)
        {
            FileStream fs = new FileStream(_logFilesWithData[fileIndex], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _stream = new StreamReader(fs);
            _stream.SkipLine(linesToSkip);
        }
        private long GetEventCount()
        {
            long eventCount = 0;

            foreach (var logFile in _logFilesWithData)
            {
                using (StreamReader logFileStream = new StreamReader(File.Open(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    
                    do
                    {
                        string logFileCurrentString = logFileStream.ReadLineWithoutNull();
                        if (LogParserLGF.ItsBeginOfEvent(logFileCurrentString))
                            eventCount++;
                    } while (!logFileStream.EndOfStream);
                }
            }

            return eventCount;
        }
        private bool NextLineIsBeginEvent()
        {
            if (CurrentFile == null || _stream == null)
                return false;

            bool nextIsBeginEvent;
            long currentStreamPosition = _stream.GetPosition();

            using (FileStream fileStreamCheckReader = new FileStream(CurrentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader checkReader = new StreamReader(fileStreamCheckReader))
                {
                    checkReader.SetPosition(currentStreamPosition);
                    string lineContent = checkReader.ReadLineWithoutNull();
                    nextIsBeginEvent = LogParserLGF.ItsBeginOfEvent(lineContent);
                }
            }            

            return nextIsBeginEvent;
        }
        private void FixEventPosition(string currentFilePath, ref long newStreamPosition, long sourceStreamPosition)
        {
            bool isCorrectBeginEvent = false;

            FindNearestBeginEventPosition(
                ref isCorrectBeginEvent,
                currentFilePath,
                ref newStreamPosition);

            if (!isCorrectBeginEvent)
            {
                newStreamPosition = sourceStreamPosition;
                FindNearestBeginEventPosition(
                    ref isCorrectBeginEvent,
                    currentFilePath,
                    ref newStreamPosition,
                    -1);
            }
        }
        private void FindNearestBeginEventPosition(ref bool isCorrectBeginEvent, string currentFilePath, ref long newStreamPosition, int stepSize = 1)
        {
            int attemptToFoundBeginEventLine = 0;
            while (!isCorrectBeginEvent && attemptToFoundBeginEventLine < 10)
            {
                string beginEventLine;
                using (FileStream fileStreamCheckPosition =
                    new FileStream(currentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileStreamCheckPosition.Seek(newStreamPosition, SeekOrigin.Begin);
                    using (StreamReader fileStreamCheckReader = new StreamReader(fileStreamCheckPosition))
                        beginEventLine = fileStreamCheckReader.ReadLineWithoutNull();
                }

                if (beginEventLine == null)
                {
                    isCorrectBeginEvent = false;
                    break;
                }

                isCorrectBeginEvent = LogParserLGF.ItsBeginOfEvent(beginEventLine);
                if (!isCorrectBeginEvent)
                {
                    newStreamPosition -= stepSize;
                    attemptToFoundBeginEventLine += 1;
                }
            }
        }
        private bool LogFileByIndexExist()
        {
            return _indexCurrentFile < _logFilesWithData.Length
                && _indexCurrentFile >= 0;
        }
        private bool ChangeFileStep(int fileIndexStepToChange)
        {
            RaiseAfterReadFileIfIsNecessary();

            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            _indexCurrentFile += fileIndexStepToChange;
            _currentFileEventNumber = 0;
            _eventCount = -1;

            return LogFileByIndexExist();
        }
        private void RaiseAfterReadFileIfIsNecessary()
        {
            if (_stream != null)
                RaiseAfterReadFile(new AfterReadFileEventArgs(CurrentFile));
        }

        #endregion
    }
}
