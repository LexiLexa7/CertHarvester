using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CertHarvester
{
	/// <summary>
	/// Главная точка входа в приложение CertHarvester
	/// </summary>
	public static class Program
	{
		private const int STD_OUTPUT_HANDLE = -11;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct CONSOLE_FONT_INFO_EX
		{
			public uint cbSize;
			public uint nFont;
			public short dwFontSizeX;
			public short dwFontSizeY;
			public int FontFamily;
			public int FontWeight;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string FaceName;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetConsoleOutputCP(uint wCodePageID);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetConsoleCP(uint wCodePageID);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool bMaximumWindow, ref CONSOLE_FONT_INFO_EX lpConsoleCurrentFontEx);

		// Настройка кодировки консоли и Unicode-шрифта для корректного отображения кириллицы на всех версиях Windows
		private static void ConfigureConsoleEncoding()
		{
			try
			{
				SetConsoleOutputCP(65001);
				SetConsoleCP(65001);
				Console.OutputEncoding = System.Text.Encoding.UTF8;
				Console.InputEncoding = System.Text.Encoding.UTF8;

				IntPtr stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
				if (stdOutHandle != IntPtr.Zero && stdOutHandle != new IntPtr(-1))
				{
					CONSOLE_FONT_INFO_EX fontInfo = new CONSOLE_FONT_INFO_EX();
					fontInfo.cbSize = (uint)Marshal.SizeOf(typeof(CONSOLE_FONT_INFO_EX));
					fontInfo.FontFamily = 54;
					fontInfo.FontWeight = 400;
					fontInfo.FaceName = "Consolas";
					fontInfo.dwFontSizeY = 16;
					SetCurrentConsoleFontEx(stdOutHandle, false, ref fontInfo);
				}
			}
			catch
			{
				try
				{
					SetConsoleOutputCP(1251);
					SetConsoleCP(1251);
					Console.OutputEncoding = System.Text.Encoding.GetEncoding(1251);
					Console.InputEncoding = System.Text.Encoding.GetEncoding(1251);
				}
				catch { }
			}
		}

		// Точка входа в программу с маршрутизацией режимов работы по аргументам командной строки
		[STAThread]
		public static int Main(string[] args)
		{
			ConfigureConsoleEncoding();

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
				Console.WriteLine(" 2. Открыть графическую панель управления");
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

		// Инициализация и отображение графической панели управления на базе HandyControl (WPF)
		public static void OpenGraphicalInterface()
		{
			try
			{
				Logger.LogInfo("Инициализация графической подсистемы интерфейса (WPF)");

				if (System.Windows.Application.Current == null)
				{
					System.Windows.Application app = new System.Windows.Application();
					app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
				}

				ControlPanelForm form = new ControlPanelForm();
				form.ShowDialog();
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при запуске графического интерфейса: " + ex.Message, ex);
			}
		}
	}
}
