using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CertHarvester
{
	/// <summary>
	/// Главная точка входа в приложение CertHarvester
	/// </summary>
	public static class Program
	{
		// Точка входа в программу с маршрутизацией режимов работы по аргументам командной строки
		[STAThread]
		public static int Main(string[] args)
		{
			try
			{
				// Логирование старта работы приложения строго по образцу из задания
				string initialMessage = string.Format("Приложение запущено. User login: {0}, PC: {1}", Environment.UserName, Environment.MachineName);
				Logger.LogInfo(initialMessage);

				// 1. Анализ аргументов командной строки для серверного и пакетного запуска
				if (args != null && args.Length > 0)
				{
					for (int i = 0; i < args.Length; i++)
					{
						string argument = args[i].ToLowerInvariant();

						if (argument == "--scan" || argument == "-scan")
						{
							Logger.LogInfo("Обнаружен аргумент командной строки '--scan'. Запуск консольного сканирования");
							RunConsoleScan();
							Logger.LogInfo("Выход из приложения после выполнения сканирования по аргументу командной строки");
							return 0;
						}
						else if (argument == "--ui" || argument == "-ui")
						{
							Logger.LogInfo("Обнаружен аргумент командной строки '--ui'. Открытие графической панели управления");
							OpenGraphicalInterface();
							Logger.LogInfo("Выход из приложения после закрытия графического интерфейса");
							return 0;
						}
					}
				}

				// 2. Интерактивное консольное меню при запуске приложения без аргументов
				RunInteractiveConsoleMenu();

				Logger.LogInfo("Выход из приложения");
				return 0;
			}
			catch (Exception ex)
			{
				Logger.LogError("Критический сбой в основном потоке выполнения Program.Main: " + ex.Message, ex);
				return 1;
			}
		}

		// Запуск интерактивного консольного меню выбора режима работы
		private static void RunInteractiveConsoleMenu()
		{
			bool isApplicationRunning = true;

			while (isApplicationRunning == true)
			{
				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.Cyan;
				Console.WriteLine("======================================================================");
				Console.WriteLine("        CertHarvester - Модуль сбора данных о СКЗИ и сертификатах     ");
				Console.WriteLine("======================================================================");
				Console.ResetColor();
				Console.WriteLine(" Пользователь: " + Environment.UserName + " | Рабочая станция: " + Environment.MachineName);
				Console.WriteLine("----------------------------------------------------------------------");
				Console.WriteLine(" 1. Запустить консольное сканирование и парсинг сертификатов");
				Console.WriteLine(" 2. Открыть графическую панель управления (Windows Forms)");
				Console.WriteLine(" 3. Выход из программы");
				Console.WriteLine("----------------------------------------------------------------------");
				Console.Write(" Выберите пункт меню (1-3): ");

				string userInput = Console.ReadLine();
				Console.WriteLine();

				if (userInput == "1")
				{
					Logger.LogInfo("Пользователем выбран пункт меню 1: Консольное сканирование");
					RunConsoleScan();
				}
				else if (userInput == "2")
				{
					Logger.LogInfo("Пользователем выбран пункт меню 2: Графическая панель управления");
					OpenGraphicalInterface();
				}
				else if (userInput == "3")
				{
					Logger.LogInfo("Пользователем выбран пункт меню 3: Выход");
					isApplicationRunning = false;
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("Некорректный ввод. Пожалуйста, введите цифру 1, 2 или 3.");
					Console.ResetColor();
				}
			}
		}

		// Выполнение полного цикла консольного сканирования сертификатов и сохранения данных в СУБД MSSQL
		public static void RunConsoleScan()
		{
			try
			{
				Logger.LogInfo("Чтение конфигурационного файла для запуска процедуры сбора СКЗИ");
				AppConfig config = ConfigManager.LoadConfig();

				Logger.LogInfo("Запуск поиска и парсинга сертификатов открытого ключа");
				List<CertificateRecord> discoveredCertificates = CertificateScanner.ScanAll(config);

				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("-------------------- Результаты сканирования --------------------");
				for (int i = 0; i < discoveredCertificates.Count; i++)
				{
					CertificateRecord cert = discoveredCertificates[i];
					Console.WriteLine(string.Format("[{0}] Серийный №: {1} | Владелец: {2} | УЦ: {3} | КриптоПро: {4} | Лицензия: {5}", i + 1, cert.SerialNumber, cert.OwnerName, cert.Issuer, cert.CryptoProVersion, cert.HasEmbeddedLicense ? "Встроена" : "Отсутствует"));
				}
				Console.WriteLine("-----------------------------------------------------------------");
				Console.ResetColor();
				Console.WriteLine();

				// Сохранение обнаруженных сертификатов в конфигурационный файл
				config.Certificates = discoveredCertificates;
				ConfigManager.SaveConfig(config);

				Logger.LogInfo("Отправка полученных данных в таблицу СУБД Microsoft SQL Server 2019");
				int savedRecordsCount = DatabaseManager.SaveCertificates(discoveredCertificates, config.ConnectionString, config.TableName);

				Logger.LogInfo("Консольное сканирование и синхронизация успешно завершены. Записей в БД: " + savedRecordsCount);
			}
			catch (Exception ex)
			{
				Logger.LogError("Сбой при выполнении рабочего цикла консольного сканирования: " + ex.Message, ex);
			}
		}

		private static bool _visualStylesConfigured = false;

		// Инициализация и отображение графической панели управления на базе Windows Forms
		public static void OpenGraphicalInterface()
		{
			try
			{
				Logger.LogInfo("Инициализация графической подсистемы Windows Forms");

				if (_visualStylesConfigured == false)
				{
					Application.EnableVisualStyles();
					Application.SetCompatibleTextRenderingDefault(false);
					_visualStylesConfigured = true;
				}

				using (ControlPanelForm form = new ControlPanelForm())
				{
					form.ShowDialog();
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при запуске графического интерфейса Windows Forms: " + ex.Message, ex);
			}
		}
	}
}
