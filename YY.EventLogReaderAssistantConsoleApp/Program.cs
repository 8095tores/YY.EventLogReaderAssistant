using System;
using System.Linq;
using System.Threading;
using YY.EventLogReaderAssistant;
using YY.EventLogReaderAssistant.EventArguments;

namespace YY.EventLogReaderAssistantConsoleApp
{
    static class Program
    {
        private static int _eventNumber;
        private static DateTime _lastPeriodEvent = DateTime.MinValue;
        private static EventLogPosition _currentPosition;

        static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            string dataDirectoryPath = args[0];
            Console.WriteLine($"{DateTime.Now}: Инициализация чтения логов \"{dataDirectoryPath}\"...");

            using (EventLogReader reader = EventLogReader.CreateReader(dataDirectoryPath))
            {
                InitializingEventHandlers(reader);
                if (args.Contains("LastFile"))
                    reader.LastFile();
                
                while (true)
                {
                    if (_currentPosition != null)
                        reader.SetCurrentPosition(_currentPosition);

                    while (reader.Read())
                    {
                        // reader.CurrentRow - данные текущего события
                    }

                    Thread.Sleep(1000);
                }
            }
        }

        private static void Reader_BeforeReadFile(EventLogReader sender, BeforeReadFileEventArgs args)
        {
            Console.WriteLine($"{DateTime.Now}: Начало чтения файла \"{args.FileName}\"");
            Console.WriteLine($"{DateTime.Now}: {_eventNumber}");
            Console.WriteLine($"{DateTime.Now}: {_lastPeriodEvent}");
        }
        private static void Reader_AfterReadFile(EventLogReader sender, AfterReadFileEventArgs args)
        {
            Console.WriteLine($"{DateTime.Now}: Окончание чтения файла \"{args.FileName}\"");
        }
        private static void Reader_BeforeReadEvent(EventLogReader sender, BeforeReadEventArgs args)
        {
            Console.SetCursorPosition(0, Console.CursorTop - 2);
            Console.WriteLine($"{DateTime.Now}: (+){_eventNumber}");
            Console.WriteLine($"{DateTime.Now}: {_lastPeriodEvent}");
        }
        private static void Reader_AfterReadEvent(EventLogReader sender, AfterReadEventArgs args)
        {
            if (args.RowData != null)
            {
                _lastPeriodEvent = args.RowData.Period;
                _eventNumber += 1;
                _currentPosition = sender.GetCurrentPosition();
            }

            Console.SetCursorPosition(0, Console.CursorTop - 2);
            Console.WriteLine($"{DateTime.Now}: [+]{_eventNumber}");
            Console.WriteLine($"{DateTime.Now}: {_lastPeriodEvent}");
        }
        private static void Reader_OnErrorEvent(EventLogReader sender, OnErrorEventArgs args)
        {
            Console.WriteLine($"{DateTime.Now}: Ошибка чтения логов \"{args.Exception}\"");
        }
        private static void InitializingEventHandlers(EventLogReader reader)
        {
            reader.AfterReadEvent += Reader_AfterReadEvent;
            reader.AfterReadFile += Reader_AfterReadFile;
            reader.BeforeReadEvent += Reader_BeforeReadEvent;
            reader.BeforeReadFile += Reader_BeforeReadFile;
            reader.OnErrorEvent += Reader_OnErrorEvent;
        }
    }
}
