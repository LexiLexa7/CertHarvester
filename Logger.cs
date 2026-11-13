using System;
using System.IO;
using System.Text;

namespace CertHarvester
{
	/// <summary>
	/// Класс подсистемы логирования событий, действий пользователя и возникающих исключений
	/// </summary>
	public static class Logger
	{
		private static readonly object SyncLock = new object();
		private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CertHarvester.log");

		// Запись информационного сообщения в журнал и вывод в стандартный поток консоли
		public static void LogInfo(string message)
		{
			string timestamp = DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff");
			string logRecord = timestamp + " - " + message;

			Console.WriteLine(logRecord);

			lock (SyncLock)
			{
				try
				{
					File.AppendAllText(LogFilePath, logRecord + Environment.NewLine, Encoding.UTF8);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Ошибка записи в файл лога: " + ex.Message);
				}
			}
		}

		// Запись предупреждающего сообщения о некритичных сбоях или недоступности ресурсов
		public static void LogWarning(string message)
		{
			string timestamp = DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff");
			string logRecord = timestamp + " - ВНИМАНИЕ: " + message;

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(logRecord);
			Console.ResetColor();

			lock (SyncLock)
			{
				try
				{
					File.AppendAllText(LogFilePath, logRecord + Environment.NewLine, Encoding.UTF8);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Ошибка записи предупреждения в лог: " + ex.Message);
				}
			}
		}

		// Запись сведений об ошибке и перехваченном исключении (Exception) в файл журнала
		public static void LogError(string message, Exception exception = null)
		{
			string timestamp = DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff");
			string details = exception != null ? " Ошибка: " + exception.Message + " | Стек: " + exception.StackTrace : string.Empty;
			string logRecord = timestamp + " - ОШИБКА: " + message + details;

			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(logRecord);
			Console.ResetColor();

			lock (SyncLock)
			{
				try
				{
					File.AppendAllText(LogFilePath, logRecord + Environment.NewLine, Encoding.UTF8);
				}
				catch (Exception fileEx)
				{
					Console.WriteLine("Критический сбой записи ошибки в лог: " + fileEx.Message);
				}
			}
		}

		// Получение абсолютного пути к активному файлу журнала событий
		public static string GetLogFilePath()
		{
			return LogFilePath;
		}
	}
}
