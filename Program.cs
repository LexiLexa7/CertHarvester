using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CertHarvester
{
	/// <summary>
	/// Главная точка входа в приложение CertHarvester с поддержкой консольных флагов и интерактивного меню
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

		// Настройка кодировки консоли и Unicode-шрифта для корректного отображения кириллицы
		private static void ConfigureConsoleEncoding()
		{
			try
			{
				SetConsoleOutputCP(65001);
				SetConsoleCP(65001);
				Console.OutputEncoding = Encoding.UTF8;
				Console.InputEncoding = Encoding.UTF8;

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
					Console.OutputEncoding = Encoding.GetEncoding(1251);
					Console.InputEncoding = Encoding.GetEncoding(1251);
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
				string initialMessage = string.Format("Приложение запущено. User login: {0}, PC: {1}", Environment.UserName, Environment.MachineName);
				Logger.LogInfo(initialMessage);

				// Если аргументы не переданы — запуск интерактивного консольного меню
				if (args == null || args.Length == 0)
				{
					RunInteractiveConsoleMenu();
					Logger.LogInfo("Выход из приложения");
					return 0;
				}

				// Обработка консольных флагов командной строки
				return ParseAndExecuteCommandLine(args);
			}
			catch (Exception ex)
			{
				Logger.LogError("Критический сбой в основном потоке выполнения Program.Main: " + ex.Message, ex);
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[!] Критическая ошибка: " + ex.Message);
				Console.ResetColor();
				return 1;
			}
		}

		// Разбор и диспетчеризация параметров командной строки
		private static int ParseAndExecuteCommandLine(string[] rawArgs)
		{
			List<string> args = NormalizeArguments(rawArgs);

			// Флаги и параметры сканирования
			bool isHelp = false;
			bool isUi = false;
			bool isScan = false;
			bool noDb = false;
			string targetHost = null;
			string targetDir = null;

			// Флаги базы данных
			bool isDbTest = false;
			bool isDbExport = false;
			bool isDbLoad = false;
			bool isDbClear = false;
			string overrideServer = null;
			string overrideDb = null;
			string overrideTable = null;
			string overrideConn = null;

			// Флаги сети и диагностики
			bool isDiscover = false;
			string discoverStartIp = null;
			string discoverEndIp = null;
			string pingHost = null;
			string netUseHost = null;
			string netUseUser = null;
			string netUsePass = null;

			// Флаги управления узлами
			bool isListNodes = false;
			string addNodeName = null;
			string addNodeIp = null;
			string addNodeUser = null;
			string addNodePass = null;
			string delNodeName = null;

			// Флаги просмотра и экспорта
			bool isListCerts = false;
			string exportCsvPath = null;
			string exportJsonPath = null;

			for (int i = 0; i < args.Count; i++)
			{
				string token = args[i];
				string lower = token.ToLowerInvariant();

				if (lower == "-help" || lower == "--help" || lower == "-h" || lower == "/?" || lower == "help")
				{
					isHelp = true;
				}
				else if (lower == "-ui" || lower == "--ui" || lower == "-gui" || lower == "ui")
				{
					isUi = true;
				}
				else if (lower == "-scan" || lower == "--scan")
				{
					isScan = true;
				}
				else if (lower == "-no-db" || lower == "--no-db")
				{
					noDb = true;
				}
				else if (lower == "-target" || lower == "--target")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						targetHost = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -target необходимо указать имя компьютера или IP-адрес.");
						return 1;
					}
				}
				else if (lower == "-dir" || lower == "--dir")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						targetDir = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -dir необходимо указать путь к директории.");
						return 1;
					}
				}
				else if (lower == "-db-test" || lower == "--db-test")
				{
					isDbTest = true;
				}
				else if (lower == "-db-export" || lower == "--db-export")
				{
					isDbExport = true;
				}
				else if (lower == "-db-load" || lower == "--db-load")
				{
					isDbLoad = true;
				}
				else if (lower == "-db-clear" || lower == "--db-clear")
				{
					isDbClear = true;
				}
				else if (lower == "-server" || lower == "--server")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						overrideServer = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -server необходимо указать адрес сервера MSSQL.");
						return 1;
					}
				}
				else if (lower == "-db" || lower == "--db")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						overrideDb = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -db необходимо указать имя базы данных.");
						return 1;
					}
				}
				else if (lower == "-table" || lower == "--table")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						overrideTable = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -table необходимо указать имя таблицы.");
						return 1;
					}
				}
				else if (lower == "-conn" || lower == "--conn")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						overrideConn = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -conn необходимо указать строку подключения.");
						return 1;
					}
				}
				else if (lower == "-discover" || lower == "--discover")
				{
					isDiscover = true;
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						discoverStartIp = args[++i];
						if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
						{
							discoverEndIp = args[++i];
						}
					}
				}
				else if (lower == "-ping" || lower == "--ping")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						pingHost = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -ping необходимо указать хост или IP-адрес.");
						return 1;
					}
				}
				else if (lower == "-netuse" || lower == "--netuse")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						netUseHost = args[++i];
						if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
						{
							netUseUser = args[++i];
							if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
							{
								netUsePass = args[++i];
							}
						}
					}
					else
					{
						PrintArgumentError("Для флага -netuse необходимо указать адрес компьютера.");
						return 1;
					}
				}
				else if (lower == "-list-nodes" || lower == "--list-nodes")
				{
					isListNodes = true;
				}
				else if (lower == "-add-node" || lower == "--add-node")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						addNodeName = args[++i];
						if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
						{
							addNodeIp = args[++i];
							if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
							{
								addNodeUser = args[++i];
								if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
								{
									addNodePass = args[++i];
								}
							}
						}
					}
					else
					{
						PrintArgumentError("Для флага -add-node необходимо указать имя компьютера.");
						return 1;
					}
				}
				else if (lower == "-del-node" || lower == "--del-node")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						delNodeName = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -del-node необходимо указать имя компьютера.");
						return 1;
					}
				}
				else if (lower == "-list-certs" || lower == "--list-certs")
				{
					isListCerts = true;
				}
				else if (lower == "-export-csv" || lower == "--export-csv")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						exportCsvPath = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -export-csv необходимо указать путь к файлу назначения.");
						return 1;
					}
				}
				else if (lower == "-export-json" || lower == "--export-json")
				{
					if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
					{
						exportJsonPath = args[++i];
					}
					else
					{
						PrintArgumentError("Для флага -export-json необходимо указать путь к файлу назначения.");
						return 1;
					}
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("[!] Неизвестный параметр командной строки: " + token);
					Console.WriteLine("[!] Введите CertHarvester.exe -help для просмотра полного списка команд.");
					Console.ResetColor();
					return 1;
				}
			}

			// Выполнение выбранных действий
			if (isHelp)
			{
				PrintHelp();
				return 0;
			}

			if (isUi)
			{
				Logger.LogInfo("Запуск графического интерфейса по аргументу командной строки");
				OpenGraphicalInterface();
				return 0;
			}

			// Загрузка конфигурации для последующих операций
			AppConfig config = ConfigManager.LoadConfig();

			// Применение переопределений базы данных
			ApplyDatabaseOverrides(config, overrideServer, overrideDb, overrideTable, overrideConn);

			// Выполнение диагностических команд сети
			if (isDiscover)
			{
				ExecuteNetworkDiscovery(discoverStartIp, discoverEndIp);
				return 0;
			}

			if (!string.IsNullOrEmpty(pingHost))
			{
				ExecutePing(pingHost);
				return 0;
			}

			if (!string.IsNullOrEmpty(netUseHost))
			{
				ExecuteNetUse(netUseHost, netUseUser, netUsePass);
				return 0;
			}

			// Управление узлами в конфигурации
			if (isListNodes || !string.IsNullOrEmpty(addNodeName) || !string.IsNullOrEmpty(delNodeName))
			{
				ExecuteNodeManagement(config, isListNodes, addNodeName, addNodeIp, addNodeUser, addNodePass, delNodeName);
				return 0;
			}

			// Операции с базой данных
			if (isDbTest || isDbExport || isDbLoad || isDbClear)
			{
				ExecuteDatabaseOperations(config, isDbTest, isDbExport, isDbLoad, isDbClear);
				return 0;
			}

			// Просмотр и экспорт сертификатов
			if (isListCerts)
			{
				PrintCertificateTable(config.Certificates);
				return 0;
			}

			if (!string.IsNullOrEmpty(exportCsvPath) && !isScan && string.IsNullOrEmpty(targetHost) && string.IsNullOrEmpty(targetDir))
			{
				ExportToCsv(config.Certificates, exportCsvPath);
				return 0;
			}

			if (!string.IsNullOrEmpty(exportJsonPath) && !isScan && string.IsNullOrEmpty(targetHost) && string.IsNullOrEmpty(targetDir))
			{
				ExportToJson(config.Certificates, exportJsonPath);
				return 0;
			}

			// Сканирование сертификатов
			if (isScan || !string.IsNullOrEmpty(targetHost) || !string.IsNullOrEmpty(targetDir))
			{
				ExecuteScan(config, noDb, targetHost, targetDir, exportCsvPath, exportJsonPath);
				return 0;
			}

			// Если флаги не совпали с действиями
			PrintHelp();
			return 0;
		}

		// Нормализация аргументов: разделение конструкций вида --flag=value на отдельные токены
		private static List<string> NormalizeArguments(string[] rawArgs)
		{
			List<string> normalized = new List<string>();
			if (rawArgs == null)
			{
				return normalized;
			}

			for (int i = 0; i < rawArgs.Length; i++)
			{
				string arg = rawArgs[i];
				if (string.IsNullOrWhiteSpace(arg))
				{
					continue;
				}

				arg = arg.Trim();
				int eqIndex = arg.IndexOf('=');
				if (arg.StartsWith("-") && eqIndex > 1)
				{
					string flagPart = arg.Substring(0, eqIndex).Trim();
					string valuePart = arg.Substring(eqIndex + 1).Trim('"', '\'');
					normalized.Add(flagPart);
					if (!string.IsNullOrEmpty(valuePart))
					{
						normalized.Add(valuePart);
					}
				}
				else
				{
					normalized.Add(arg);
				}
			}

			return normalized;
		}

		// Вывод сообщения об ошибке аргумента
		private static void PrintArgumentError(string message)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("[!] Ошибка аргументов: " + message);
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("[!] Введите CertHarvester.exe -help для справки по формату команд.");
			Console.ResetColor();
		}

		// Отображение полной интерактивной справки со списком команд и флагов приложения
		public static void PrintHelp()
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("========================================================================================================");
			Console.WriteLine("                       CertHarvester - Консольный интерфейс и параметры запуска                         ");
			Console.WriteLine("========================================================================================================");
			Console.ResetColor();
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" ИСПОЛЬЗОВАНИЕ:");
			Console.ResetColor();
			Console.WriteLine("   CertHarvester.exe [флаги / команды] [параметры]");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" ОСНОВНЫЕ РЕЖИМЫ:");
			Console.ResetColor();
			Console.WriteLine("   -help, --help, -h, /?              Отобразить данную справку со списком всех команд и флагов");
			Console.WriteLine("   -ui, --ui, -gui                    Запустить графический интерфейс пользователя (WPF)");
			Console.WriteLine("   (без параметров)                   Запустить интерактивное консольное меню с выбором действий");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" СКАНИРОВАНИЕ И СБОР СКЗИ:");
			Console.ResetColor();
			Console.WriteLine("   -scan, --scan                      Полное сканирование всех ПК и каталогов из config.json");
			Console.WriteLine("   -target <host/ip>                  Сканировать конкретный компьютер или IP-адрес");
			Console.WriteLine("   -dir <path>                        Рекурсивно сканировать конкретную папку на сертификаты (.cer, .crt)");
			Console.WriteLine("   -no-db                             Пропустить сохранение результатов сканирования в базу данных");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" РАБОТА С БАЗОЙ ДАННЫХ (MSSQL 2019):");
			Console.ResetColor();
			Console.WriteLine("   -db-test                           Проверить подключение к Microsoft SQL Server");
			Console.WriteLine("   -db-export                         Экспортировать локально найденные сертификаты в БД");
			Console.WriteLine("   -db-load                           Загрузить и отобразить список сертификатов из БД");
			Console.WriteLine("   -db-clear                          Очистить таблицу сертификатов в БД");
			Console.WriteLine("   -server <address>                  Переопределить адрес сервера БД (напр.: localhost или SRV\\SQL)");
			Console.WriteLine("   -db <database>                     Переопределить имя БД (по умолчанию: CertHarvesterDB)");
			Console.WriteLine("   -table <tableName>                 Переопределить имя таблицы (по умолчанию: Certificates)");
			Console.WriteLine("   -conn <connectionString>           Указать полную строку подключения к MSSQL");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" СЕТЕВАЯ ДИАГНОСТИКА И АВТОПОИСК:");
			Console.ResetColor();
			Console.WriteLine("   -discover [start_ip] [end_ip]      Автопоиск устройств в локальной подсети (Ping, SMB, NetBIOS)");
			Console.WriteLine("   -ping <host/ip>                    Быстрая диагностика доступности узла (ICMP Ping + SMB порт 445)");
			Console.WriteLine("   -netuse <host> <user> <pass>       Проверить авторизацию и подключить SMB-сессию net use");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" УПРАВЛЕНИЕ УЗЛАМИ СЕТИ (CONFIG.JSON):");
			Console.ResetColor();
			Console.WriteLine("   -list-nodes                        Показать список настроенных компьютеров и учетных данных");
			Console.WriteLine("   -add-node <name> [ip] [user] [pwd] Добавить или обновить компьютер в конфигурационном файле");
			Console.WriteLine("   -del-node <name>                   Удалить компьютер из конфигурационного файла");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" ПРОСМОТР И ЭКСПОРТ ДАННЫХ:");
			Console.ResetColor();
			Console.WriteLine("   -list-certs                        Показать список всех сохраненных сертификатов в config.json");
			Console.WriteLine("   -export-csv <file.csv>             Экспортировать сертификаты в CSV (UTF-8 с BOM для Excel)");
			Console.WriteLine("   -export-json <file.json>           Экспортировать сертификаты в структурированный JSON-файл");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(" ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ:");
			Console.ResetColor();
			Console.WriteLine("   CertHarvester.exe -scan");
			Console.WriteLine("   CertHarvester.exe -scan -no-db");
			Console.WriteLine("   CertHarvester.exe -target 192.168.1.100");
			Console.WriteLine("   CertHarvester.exe -dir \"C:\\Certificates\" -export-csv \"C:\\Export\\certs.csv\"");
			Console.WriteLine("   CertHarvester.exe -discover");
			Console.WriteLine("   CertHarvester.exe -discover 192.168.1.1 192.168.1.50");
			Console.WriteLine("   CertHarvester.exe -ping 192.168.1.50");
			Console.WriteLine("   CertHarvester.exe -netuse 192.168.1.50 Administrator SecretPass123");
			Console.WriteLine("   CertHarvester.exe -db-test -server \"SRV-DB\\SQLEXPRESS\" -db \"CertDB\"");
			Console.WriteLine("   CertHarvester.exe -db-load");
			Console.WriteLine("   CertHarvester.exe -add-node PC-BUHG 192.168.1.25 Admin Secret123");
			Console.WriteLine("   CertHarvester.exe -del-node PC-BUHG");
			Console.WriteLine("   CertHarvester.exe -export-csv certs.csv");

			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("========================================================================================================");
			Console.ResetColor();
			Console.WriteLine();
		}

		// Применение переопределений параметров подключения к базе данных
		private static void ApplyDatabaseOverrides(AppConfig config, string server, string db, string table, string conn)
		{
			if (!string.IsNullOrEmpty(conn))
			{
				config.ConnectionString = conn;
			}
			else if (!string.IsNullOrEmpty(server) || !string.IsNullOrEmpty(db))
			{
				try
				{
					System.Data.SqlClient.SqlConnectionStringBuilder builder =
						string.IsNullOrWhiteSpace(config.ConnectionString)
							? new System.Data.SqlClient.SqlConnectionStringBuilder()
							: new System.Data.SqlClient.SqlConnectionStringBuilder(config.ConnectionString);

					if (!string.IsNullOrEmpty(server))
					{
						builder.DataSource = server;
					}

					if (!string.IsNullOrEmpty(db))
					{
						builder.InitialCatalog = db;
					}

					if (string.IsNullOrWhiteSpace(builder.UserID) && !builder.IntegratedSecurity)
					{
						builder.IntegratedSecurity = true;
					}

					config.ConnectionString = builder.ConnectionString;
				}
				catch
				{
					// При сбое парсинга используем стандартное формирование
					if (!string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(db))
					{
						config.ConnectionString = string.Format("Server={0};Database={1};Integrated Security=True;", server, db);
					}
				}
			}

			if (!string.IsNullOrEmpty(table))
			{
				config.TableName = table;
			}
		}

		// Выполнение сканирования сертификатов с фильтрацией по узлу или директории
		private static void ExecuteScan(AppConfig config, bool noDb, string targetHost, string targetDir, string exportCsvPath, string exportJsonPath)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine("                              Запуск процедуры сканирования сертификатов                                ");
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.ResetColor();

			List<CertificateRecord> scanResults = new List<CertificateRecord>();

			if (!string.IsNullOrEmpty(targetHost))
			{
				Console.WriteLine("[*] Целевое сканирование удаленного компьютера: " + targetHost);
				Logger.LogInfo("Консольный запуск целевого сканирования компьютера: " + targetHost);
				CertificateScanner.ScanNetworkComputer(targetHost, config.ScanDirectories, scanResults, config.ComputerCredentials);
			}
			else if (!string.IsNullOrEmpty(targetDir))
			{
				Console.WriteLine("[*] Локальное сканирование директории: " + targetDir);
				if (!Directory.Exists(targetDir))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("[!] Указанная директория не существует: " + targetDir);
					Console.ResetColor();
					return;
				}
				Logger.LogInfo("Консольный запуск сканирования директории: " + targetDir);
				CertificateScanner.ScanDirectoryRecursive(targetDir, Environment.MachineName, scanResults);
			}
			else
			{
				Console.WriteLine("[*] Комплексное сканирование всех сконфигурированных узлов и папок...");
				Logger.LogInfo("Запуск поиска и парсинга сертификатов открытого ключа (полный цикл)");
				scanResults = CertificateScanner.ScanAll(config);
			}

			// Вывод таблицы найденных сертификатов
			PrintCertificateTable(scanResults);

			// Сохранение в config.json
			if (scanResults.Count > 0)
			{
				for (int i = 0; i < scanResults.Count; i++)
				{
					CertificateRecord newRec = scanResults[i];
					bool foundExisting = false;
					for (int j = 0; j < config.Certificates.Count; j++)
					{
						if (string.Equals(config.Certificates[j].SerialNumber, newRec.SerialNumber, StringComparison.OrdinalIgnoreCase))
						{
							config.Certificates[j] = newRec;
							foundExisting = true;
							break;
						}
					}
					if (!foundExisting)
					{
						config.Certificates.Add(newRec);
					}
				}
				ConfigManager.SaveConfig(config);
				Console.WriteLine("[+] Сертификаты синхронизированы с локальным файлом config.json");
			}

			// Экспорт в БД (если не отключен флагом -no-db)
			if (!noDb)
			{
				Console.WriteLine("[*] Отправка данных в СУБД Microsoft SQL Server...");
				Logger.LogInfo("Отправка полученных данных в таблицу СУБД Microsoft SQL Server 2019");
				int savedCount = DatabaseManager.SaveCertificates(scanResults, config.ConnectionString, config.TableName);
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(string.Format("[+] Синхронизация с базой данных успешно завершена. Записей в БД: {0}", savedCount));
				Console.ResetColor();
			}
			else
			{
				Console.WriteLine("[*] Сохранение в базу данных пропущено по флагу -no-db");
			}

			// Дополнительный экспорт в CSV/JSON при наличии флагов
			if (!string.IsNullOrEmpty(exportCsvPath))
			{
				ExportToCsv(scanResults, exportCsvPath);
			}

			if (!string.IsNullOrEmpty(exportJsonPath))
			{
				ExportToJson(scanResults, exportJsonPath);
			}
		}

		// Выполнение операций с базой данных MSSQL (проверка связи, экспорт, чтение, очистка)
		private static void ExecuteDatabaseOperations(AppConfig config, bool isTest, bool isExport, bool isLoad, bool isClear)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine("                             Операции с базой данных Microsoft SQL Server                               ");
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.ResetColor();
			Console.WriteLine(" Строка подключения: " + config.ConnectionString);
			Console.WriteLine(" Целевая таблица:     " + config.TableName);
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");

			if (isTest)
			{
				Console.WriteLine("[*] Проверка подключения к СУБД MSSQL...");
				bool connected = DatabaseManager.TestConnection(config.ConnectionString);
				if (connected)
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine("[+] Соединение с базой данных успешно установлено!");
					Console.ResetColor();
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("[!] Ошибка подключения к базе данных. Проверьте адрес сервера и права доступа.");
					Console.ResetColor();
				}
			}

			if (isClear)
			{
				Console.WriteLine("[*] Очистка таблиц сертификатов в базе данных...");
				bool cleared = DatabaseManager.ClearTables(config.ConnectionString, config.TableName);
				if (cleared)
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine("[+] Таблицы сертификатов в базе данных успешно очищены.");
					Console.ResetColor();
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("[!] Не удалось очистить таблицы в БД. Проверьте права учетной записи.");
					Console.ResetColor();
				}
			}

			if (isExport)
			{
				Console.WriteLine(string.Format("[*] Экспорт {0} сертификатов из config.json в базу данных...", config.Certificates.Count));
				if (config.Certificates.Count == 0)
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("[!] В config.json нет сертификатов для экспорта. Запустите -scan перед экспортом.");
					Console.ResetColor();
				}
				else
				{
					int savedCount = DatabaseManager.SaveCertificates(config.Certificates, config.ConnectionString, config.TableName);
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine(string.Format("[+] Экспорт завершен. В базе данных обновлено/внесено записей: {0}", savedCount));
					Console.ResetColor();
				}
			}

			if (isLoad)
			{
				Console.WriteLine("[*] Загрузка данных из СУБД MSSQL...");
				List<CertificateRecord> dbCerts = DatabaseManager.LoadCertificatesFromDatabase(config.ConnectionString, config.TableName);
				PrintCertificateTable(dbCerts);
			}
		}

		// Автопоиск устройств в локальной сети
		private static void ExecuteNetworkDiscovery(string startIp, string endIp)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine("                           Автопоиск устройств в локальной подсети                                      ");
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.ResetColor();

			string localIp;
			string defaultStart, defaultEnd;
			NetworkScanner.GetLocalSubnetInfo(out defaultStart, out defaultEnd, out localIp);

			string finalStart = string.IsNullOrEmpty(startIp) ? defaultStart : startIp;
			string finalEnd = string.IsNullOrEmpty(endIp) ? defaultEnd : endIp;

			Console.WriteLine(string.Format(" Локальный IPv4:      {0}", localIp));
			Console.WriteLine(string.Format(" Сканируемый диапазон: {0} - {1}", finalStart, finalEnd));
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine("[*] Сканирование сети и проверка SMB портов 445 (пожалуйста, подождите)...");

			List<DiscoveredHostItem> discoveredHosts = new List<DiscoveredHostItem>();
			object lockObj = new object();

			try
			{
				using (CancellationTokenSource cts = new CancellationTokenSource())
				{
					Task scanTask = NetworkScanner.AutoDiscoverHostsAsync(
						finalStart,
						finalEnd,
						localIp,
						(item) =>
						{
							lock (lockObj)
							{
								discoveredHosts.Add(item);
								string osLabel = item.IsWindows ? "[Windows]" : "[Узел]";
								string pingLabel = item.PingTimeMs <= 1 ? "< 1 мс" : item.PingTimeMs + " мс";
								Console.WriteLine(string.Format("  [+] Обнаружен: {0,-16} | {1,-24} | {2,-10} | Ping: {3}", item.IpAddress, item.Name, osLabel, pingLabel));
							}
						},
						(current, total) => { },
						cts.Token);

					scanTask.Wait();
				}

				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("--------------------------------------------------------------------------------------------------------");
				Console.WriteLine(string.Format(" Автопоиск завершен. Найдено активных узлов: {0}", discoveredHosts.Count));
				Console.WriteLine("--------------------------------------------------------------------------------------------------------");
				Console.ResetColor();

				if (discoveredHosts.Count > 0)
				{
					Console.WriteLine(string.Format(" {0,-17} {1,-26} {2,-12} {3,-10} {4}", "IP-адрес", "Устройство / Имя хоста", "Тип", "Ping", "Статус"));
					Console.WriteLine(new string('-', 95));
					for (int i = 0; i < discoveredHosts.Count; i++)
					{
						DiscoveredHostItem h = discoveredHosts[i];
						string typeLabel = h.IsWindows ? "Windows ПК" : "Сетевой узел";
						Console.WriteLine(string.Format(" {0,-17} {1,-26} {2,-12} {3,-10} {4}", h.IpAddress, h.Name, typeLabel, h.PingDisplay, h.StatusText));
					}
					Console.WriteLine(new string('-', 95));
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[!] Ошибка автопоиска сети: " + ex.Message);
				Console.ResetColor();
			}
		}

		// Быстрая комплексная диагностика связи с узлом (Ping + SMB)
		private static void ExecutePing(string host)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine("                                  Диагностика сетевого узла                                             ");
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.ResetColor();
			Console.WriteLine(" Целевой хост: " + host);
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");

			long pingMs = 0;
			bool isSmbOpen = false;
			bool available = NetworkScanner.CheckHostQuick(host, 2000, 1000, out pingMs, out isSmbOpen);

			string netbiosName = NetworkScanner.QueryNetBiosName(host, 500);

			if (available)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("[+] Статус связи:     Узел ДОСТУПЕН в сети");
				Console.ResetColor();
				Console.WriteLine(string.Format("    ICMP Ping:        Успешно ({0} мс)", pingMs <= 1 ? "< 1" : pingMs.ToString()));
				Console.WriteLine(string.Format("    SMB порт 445:     {0}", isSmbOpen ? "ОТКРЫТ (поддерживает сетевой доступ Windows)" : "ЗАКРЫТ"));
				if (!string.IsNullOrEmpty(netbiosName))
				{
					Console.WriteLine(string.Format("    Имя NetBIOS:      {0}", netbiosName));
				}
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[!] Статус связи:     Узел НЕДОСТУПЕН (нет ответа на Ping и порт 445 закрыт)");
				Console.ResetColor();
			}
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
		}

		// Проверка аутентификации и подключение SMB-сессии net use
		private static void ExecuteNetUse(string host, string username, string password)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine("                           Установление сетевой сессии (net use)                                        ");
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.ResetColor();
			Console.WriteLine(string.Format(" Компьютер:    {0}", host));
			Console.WriteLine(string.Format(" Пользователь: {0}", string.IsNullOrEmpty(username) ? "(текущий контекст Windows)" : username));
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");

			string outputMsg;
			bool success = CertificateScanner.EstablishNetworkSession(host, username, password, out outputMsg);

			if (success)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("[+] Сессия net use успешно установлена!");
				Console.WriteLine("    Ответ системы: " + outputMsg);
				Console.ResetColor();
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[!] Сбой подключения по net use:");
				Console.WriteLine("    " + outputMsg);
				Console.ResetColor();
			}
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
		}

		// Управление сетевыми узлами в конфигурационном файле
		private static void ExecuteNodeManagement(AppConfig config, bool isList, string addName, string addIp, string addUser, string addPass, string delName)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine("                         Управление узлами сети в файле конфигурации                                    ");
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.ResetColor();

			bool isModified = false;

			if (!string.IsNullOrEmpty(delName))
			{
				bool removedFromList = config.NetworkComputers.RemoveAll(c => string.Equals(c, delName, StringComparison.OrdinalIgnoreCase)) > 0;
				bool removedFromCreds = config.ComputerCredentials.RemoveAll(c => string.Equals(c.ComputerAddress, delName, StringComparison.OrdinalIgnoreCase)) > 0;

				if (removedFromList || removedFromCreds)
				{
					isModified = true;
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine(string.Format("[+] Узел '{0}' успешно удален из конфигурации.", delName));
					Console.ResetColor();
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine(string.Format("[!] Узел '{0}' не найден в конфигурационном файле.", delName));
					Console.ResetColor();
				}
			}

			if (!string.IsNullOrEmpty(addName))
			{
				if (!config.NetworkComputers.Contains(addName))
				{
					config.NetworkComputers.Add(addName);
					isModified = true;
				}

				if (!string.IsNullOrEmpty(addIp) || !string.IsNullOrEmpty(addUser))
				{
					NetworkComputerCredential cred = config.ComputerCredentials.Find(c => string.Equals(c.ComputerAddress, addName, StringComparison.OrdinalIgnoreCase));
					if (cred == null)
					{
						cred = new NetworkComputerCredential(addName, addUser ?? string.Empty, addPass ?? string.Empty);
						config.ComputerCredentials.Add(cred);
					}
					else
					{
						if (!string.IsNullOrEmpty(addUser)) cred.Username = addUser;
						if (!string.IsNullOrEmpty(addPass)) cred.Password = addPass;
					}
					isModified = true;
				}

				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(string.Format("[+] Узел '{0}' успешно добавлен/обновлен в конфигурации.", addName));
				Console.ResetColor();
			}

			if (isModified)
			{
				ConfigManager.SaveConfig(config);
				Console.WriteLine("[+] Конфигурационный файл config.json сохранен.");
			}

			// Вывод актуального списка компьютеров
			Console.WriteLine();
			Console.WriteLine(" Список сконфигурированных компьютеров сети:");
			Console.WriteLine(new string('-', 75));
			Console.WriteLine(string.Format(" {0,-4} {1,-26} {2,-20} {3}", "№", "Имя компьютера", "Логин", "Пароль"));
			Console.WriteLine(new string('-', 75));

			for (int i = 0; i < config.NetworkComputers.Count; i++)
			{
				string comp = config.NetworkComputers[i];
				NetworkComputerCredential cred = config.ComputerCredentials.Find(c => string.Equals(c.ComputerAddress, comp, StringComparison.OrdinalIgnoreCase));
				string login = cred != null && !string.IsNullOrEmpty(cred.Username) ? cred.Username : "(по умолчанию)";
				string pass = cred != null && !string.IsNullOrEmpty(cred.Password) ? "••••••••" : "(без пароля)";
				Console.WriteLine(string.Format(" {0,-4} {1,-26} {2,-20} {3}", i + 1, comp, login, pass));
			}
			Console.WriteLine(new string('-', 75));
		}

		// Вывод списка сертификатов в виде форматированной таблицы
		private static void PrintCertificateTable(List<CertificateRecord> certs)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.WriteLine(string.Format("                     Список сертификатов открытого ключа (Всего: {0})                     ", certs != null ? certs.Count : 0));
			Console.WriteLine("--------------------------------------------------------------------------------------------------------");
			Console.ResetColor();

			if (certs == null || certs.Count == 0)
			{
				Console.WriteLine("  Сертификаты не найдены.");
				Console.WriteLine("--------------------------------------------------------------------------------------------------------");
				return;
			}

			Console.WriteLine(string.Format(" {0,-4} {1,-22} {2,-26} {3,-12} {4,-10} {5}", "№", "Серийный номер", "Владелец", "КриптоПро", "Лицензия", "Компьютер"));
			Console.WriteLine(new string('-', 100));

			for (int i = 0; i < certs.Count; i++)
			{
				CertificateRecord c = certs[i];
				string serialShort = TruncateString(c.SerialNumber, 20);
				string ownerShort = TruncateString(c.OwnerName, 24);
				string lic = c.HasEmbeddedLicense ? "Да" : "Нет";
				string comp = TruncateString(c.MachineName, 18);

				Console.WriteLine(string.Format(" {0,-4} {1,-22} {2,-26} {3,-12} {4,-10} {5}", i + 1, serialShort, ownerShort, c.CryptoProVersion, lic, comp));
			}

			Console.WriteLine(new string('-', 100));
			Console.WriteLine();
		}

		// Экспорт списка сертификатов в файл CSV с кодировкой UTF-8 BOM для Microsoft Excel
		private static void ExportToCsv(List<CertificateRecord> certs, string filePath)
		{
			try
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("Серийный номер;Владелец;Удостоверяющий центр;Версия КриптоПро;Встроенная лицензия;Компьютер;IP-адрес;Источник/Файл;Дата внесения;Дата обновления");

				if (certs != null)
				{
					for (int i = 0; i < certs.Count; i++)
					{
						CertificateRecord cert = certs[i];
						string serial = EscapeCsvField(cert.SerialNumber);
						string owner = EscapeCsvField(cert.OwnerName);
						string issuer = EscapeCsvField(cert.IssuerName);
						string version = EscapeCsvField(cert.CryptoProVersion);
						string license = cert.HasEmbeddedLicense ? "Да" : "Нет";
						string machine = EscapeCsvField(cert.MachineName);
						string ip = EscapeCsvField(cert.MachineIp);
						string source = EscapeCsvField(cert.SourcePath);
						string inserted = cert.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");
						string updated = cert.UpdatedAt.ToString("dd.MM.yyyy HH:mm:ss");

						sb.AppendFormat("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9}",
							serial, owner, issuer, version, license, machine, ip, source, inserted, updated);
						sb.AppendLine();
					}
				}

				string dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}

				File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(string.Format("[+] Экспорт в CSV успешно выполнен: {0} (записей: {1})", Path.GetFullPath(filePath), certs != null ? certs.Count : 0));
				Console.ResetColor();
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[!] Ошибка экспорта в CSV: " + ex.Message);
				Console.ResetColor();
			}
		}

		// Экспорт списка сертификатов в файл JSON
		private static void ExportToJson(List<CertificateRecord> certs, string filePath)
		{
			try
			{
				JavaScriptSerializer serializer = new JavaScriptSerializer();
				string rawJson = serializer.Serialize(certs ?? new List<CertificateRecord>());
				string prettyJson = FormatJsonPretty(rawJson);

				string dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}

				File.WriteAllText(filePath, prettyJson, Encoding.UTF8);
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(string.Format("[+] Экспорт в JSON успешно выполнен: {0} (записей: {1})", Path.GetFullPath(filePath), certs != null ? certs.Count : 0));
				Console.ResetColor();
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("[!] Ошибка экспорта в JSON: " + ex.Message);
				Console.ResetColor();
			}
		}

		// Экранирование спецсимволов для формата CSV
		private static string EscapeCsvField(string field)
		{
			if (string.IsNullOrEmpty(field))
			{
				return string.Empty;
			}

			if (field.Contains(";") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
			{
				return "\"" + field.Replace("\"", "\"\"") + "\"";
			}

			return field;
		}

		// Форматирование JSON-строки с отступами без сторонних зависимостей
		private static string FormatJsonPretty(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return "[]";
			}

			StringBuilder sb = new StringBuilder();
			int indent = 0;
			bool inQuotes = false;

			for (int i = 0; i < json.Length; i++)
			{
				char ch = json[i];
				if (ch == '\"')
				{
					int backslashCount = 0;
					for (int j = i - 1; j >= 0 && json[j] == '\\'; j--)
					{
						backslashCount++;
					}
					if (backslashCount % 2 == 0)
					{
						inQuotes = !inQuotes;
					}
					sb.Append(ch);
				}
				else if (!inQuotes)
				{
					if (ch == '{' || ch == '[')
					{
						sb.Append(ch);
						sb.AppendLine();
						indent++;
						sb.Append(new string(' ', indent * 2));
					}
					else if (ch == '}' || ch == ']')
					{
						sb.AppendLine();
						indent--;
						if (indent < 0) indent = 0;
						sb.Append(new string(' ', indent * 2));
						sb.Append(ch);
					}
					else if (ch == ',')
					{
						sb.Append(ch);
						sb.AppendLine();
						sb.Append(new string(' ', indent * 2));
					}
					else if (ch == ':')
					{
						sb.Append(": ");
					}
					else if (!char.IsWhiteSpace(ch))
					{
						sb.Append(ch);
					}
				}
				else
				{
					sb.Append(ch);
				}
			}

			return sb.ToString();
		}

		// Обрезка длинных строк для компактного вывода в таблицу консоли
		private static string TruncateString(string text, int maxLength)
		{
			if (string.IsNullOrEmpty(text))
			{
				return "—";
			}

			if (text.Length <= maxLength)
			{
				return text;
			}

			return text.Substring(0, maxLength - 3) + "...";
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
				Console.WriteLine(" 1. Запустить полное сканирование и парсинг сертификатов");
				Console.WriteLine(" 2. Открыть графическую панель управления (WPF)");
				Console.WriteLine(" 3. Показать справку по флагам командной строки (-help)");
				Console.WriteLine(" 4. Выход из программы");
				Console.WriteLine("----------------------------------------------------------------------");
				Console.Write(" Выберите пункт меню (1-4): ");

				string userInput = Console.ReadLine();
				Console.WriteLine();

				if (userInput == "1")
				{
					Logger.LogInfo("Пользователем выбран пункт меню 1: Консольное сканирование");
					AppConfig config = ConfigManager.LoadConfig();
					ExecuteScan(config, false, null, null, null, null);
				}
				else if (userInput == "2")
				{
					Logger.LogInfo("Пользователем выбран пункт меню 2: Графическая панель управления");
					OpenGraphicalInterface();
				}
				else if (userInput == "3")
				{
					PrintHelp();
				}
				else if (userInput == "4")
				{
					Logger.LogInfo("Пользователем выбран пункт меню 4: Выход");
					isApplicationRunning = false;
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("Некорректный ввод. Пожалуйста, введите цифру от 1 до 4.");
					Console.ResetColor();
				}
			}
		}

		// Выполнение полного цикла консольного сканирования сертификатов и сохранения данных в СУБД MSSQL
		public static void RunConsoleScan()
		{
			AppConfig config = ConfigManager.LoadConfig();
			ExecuteScan(config, false, null, null, null, null);
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
