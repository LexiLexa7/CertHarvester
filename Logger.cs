using System;
using System.IO;
using System.Text;

namespace CertHarvester
{
	/// <summary>
	/// Класс системы логирования событий и ошибок приложения в файл и консоль (Зона ответственности: Участник 2)
	/// </summary>
	public static class Logger
	{
		private static readonly object SyncLock = new object();
		private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CertHarvester.log");

		// Запись информационного сообщения в журнал и вывод в консоль
		public static void LogInfo(string message)
		{
			// TODO (Участник 2): Реализовать настройку уровня логирования через конфигурационный файл
			// TODO (Участник 2): Добавить поддержку ротации лог-файлов по достижении заданного размера
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

		// Запись предупреждающего сообщения в журнал
		public static void LogWarning(string message)
		{
			// TODO (Участник 2): Добавить выделение предупреждений цветом в консоли и разделение потоков вывода
			string timestamp = DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff");
			string logRecord = timestamp + " - ВНИМАНИЕ: " + message;

			Console.WriteLine(logRecord);

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

		// Запись ошибки и сведений об исключении (Exception) в файл журнала
		public static void LogError(string message, Exception exception = null)
		{
			// TODO (Участник 2): Расширить детализацию исключений, включая StackTrace, TargetSite и InnerExceptions
			// TODO (Участник 2): Обеспечить отдельный файл error.log для критических сбоев при необходимости
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

		// Получение абсолютного пути к текущему файлу журнала
		public static string GetLogFilePath()
		{
			// Возвращает путь к активному файлу журнала
			return LogFilePath;
		}
	}
}
