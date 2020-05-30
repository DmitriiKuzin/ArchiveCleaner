using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConsoleApp1
{
    class Program
    {
        private static string logFilePath { get; set; }
        static void Main(string[] args)
        {
            try
            {
                CreateLogFile();
                var settingsPath = Directory.GetCurrentDirectory();
                var settings = File.ReadAllLines(Path.Combine(settingsPath, "ArchiveCleanerPaths.txt"));
            
                var entries = new List<Entry>();
                foreach (var entry in settings)
                {
                    var entryString = Regex.Split(entry, @"\s+");
                    entries.Add(new Entry(){Path = entryString[0], Days = Convert.ToInt32(entryString[1])});
                }
            
                Console.WriteLine("Archive cleanup started");
                
                foreach (var entry in entries)
                {
                    Console.WriteLine($"Working with {entry.Path} directory...");
                    WriteLog($"Работаю с папкой {entry.Path}");
                    var allFiles = Directory.GetFiles(entry.Path, "*.rar");
                    var irrelevantFiles =
                        (from file in allFiles
                            let fi = new FileInfo(file)
                            where DateTime.Now - fi.LastWriteTime > TimeSpan.FromDays(entry.Days)
                            select new IrrelevantFile() {File = file, Date = fi.LastWriteTime}).ToList();
            
                    var filesByYear = irrelevantFiles.GroupBy(f => f.Date.Year);
                    foreach (var group in filesByYear)
                    {
                        var filesByMonth = group.GroupBy(g => g.Date.Month);
                        foreach (var groupByMounth in filesByMonth)
                        {
                            if (groupByMounth.Count() <= 1) continue;
                            var orderedGroupByMounth = groupByMounth.OrderBy(g => g.Date);
                            var isFirst = true;
                            foreach (var file in orderedGroupByMounth)
                            {
                                if (isFirst)
                                {
                                    isFirst = false;
                                    continue;
                                }
                                new FileInfo(file.File).Attributes = FileAttributes.Normal;
                                File.Delete(file.File);
                                Console.WriteLine($"File {new FileInfo(file.File).Name} deleted");
                                WriteLog($"\t Файл {new FileInfo(file.File).Name} удален");
                            }
                        }
                    }
                }
                WriteLog("Работа успешно завершена");
                Console.WriteLine("Done!");
            }
            catch (IndexOutOfRangeException e)
            {
                Console.WriteLine("Wrong settings file");
                WriteLog("Ошибка! Неправильно заполнен файл настроек");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                WriteLog("Ошибка! " + e.Message);
            }
            
            //generateFiles();
        }

        private static void CreateLogFile()
        {
            var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logsDirectory))
                Directory.CreateDirectory(logsDirectory);
                
            logFilePath = Path.Combine(logsDirectory, DateTime.Now.ToString("dd-MM-yyyy-hh-mm-ss") + ".txt");
            using (StreamWriter sw = File.CreateText(logFilePath))
            {    
                sw.WriteLine(DateTime.Now + "\tНачало работы утилиты");
            }
        }
        
        private static void WriteLog(string message)
        {
            using (StreamWriter sw = File.AppendText(logFilePath)) 
            {
                sw.WriteLine( DateTime.Now + "\t" + message);
            }	
        }

        private static void GenerateFiles()
        {
            var dir = @"";

            for (int i = 0; i < 30; i++)
            {
                var fileName = "fileb " + i + ".rar";
                var file = Path.Combine(dir, fileName);
                using (StreamWriter sw = File.CreateText(file))
                {    
                    sw.WriteLine(DateTime.Now + " Начало работы утилиты");
                }
                var fi = new FileInfo(file);
                fi.LastWriteTime = DateTime.Parse("01.01.2010");
            }

        }
        
    }


    public class Entry
    {
        public string Path { get; set; }
        public int Days { get; set; }
    }

    public class IrrelevantFile
    {
        public string File { get; set; }
        public DateTime Date { get; set; }
    }
}