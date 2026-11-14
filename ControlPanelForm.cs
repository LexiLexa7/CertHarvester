using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using HandyControl.Data;
using HandyControl.Themes;
using HandyControl.Tools;
using HcWindow = HandyControl.Controls.Window;

namespace CertHarvester
{
	/// <summary>
	/// Элемент отображения сетевого рабочего узла в боковой панели
	/// </summary>
	public class NetworkNodeItem
	{
		public string Name { get; set; }
		public string IpAddress { get; set; }
		public string Status { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public bool IsConnected { get; set; }
		public List<string> CustomDirectories { get; set; }

		// Конструктор модели сетевого узла с поддержкой учетных данных и индивидуальных директорий
		public NetworkNodeItem(string name, string ipAddress, string status, string username = "", string password = "", List<string> customDirectories = null)
		{
			Name = name;
			IpAddress = ipAddress;
			Status = status;
			Username = username;
			Password = password;
			IsConnected = false;
			CustomDirectories = customDirectories ?? new List<string>();
		}
	}

	/// <summary>
	/// Элемент представления сертификата для привязки данных в таблице DataGrid
	/// </summary>
	public class CertificateDisplayItem
	{
		public string SerialNumber { get; set; }
		public string DateInsertedDisplay { get; set; }
		public string DateUpdatedDisplay { get; set; }
		public string OwnerName { get; set; }
		public string Issuer { get; set; }
		public string CryptoProVersion { get; set; }
		public bool HasEmbeddedLicense { get; set; }
		public string LicenseDisplay { get { return HasEmbeddedLicense ? "●  Да" : "○  Нет"; } }
		public CertificateRecord Record { get; set; }

		// Конструктор адаптера записи сертификата
		public CertificateDisplayItem(CertificateRecord record)
		{
			Record = record;
			SerialNumber = record.SerialNumber ?? string.Empty;
			DateInsertedDisplay = record.DateInserted.ToString("dd.MM.yy HH:mm:ss");
			DateUpdatedDisplay = record.DateUpdated.ToString("dd.MM.yy HH:mm:ss");
			OwnerName = record.OwnerName ?? string.Empty;
			Issuer = record.Issuer ?? string.Empty;
			CryptoProVersion = string.IsNullOrEmpty(record.CryptoProVersion) ? "—" : record.CryptoProVersion;
			HasEmbeddedLicense = record.HasEmbeddedLicense;
		}
	}

	/// <summary>
	/// <summary>
	/// Графическая панель управления СКЗИ на базе современной библиотеки HandyControl (WPF для .NET Framework 4.5)
	/// </summary>
	public class ControlPanelForm : HcWindow
	{
		private AppConfig _currentConfig;
		private List<CertificateRecord> _allCertificates;
		private List<CertificateDisplayItem> _displayCertificates;
		private List<NetworkNodeItem> _networkNodes;
		private bool _filterOnlyWithLicense;
		private string _searchFilterQuery;
		private CancellationTokenSource _scanCts;
		private bool _isScanningActive;
		private bool _isDarkTheme;
		private HandyControl.Themes.Theme _handyTheme;

		// Элементы верхней панели (NonClientArea)
		private System.Windows.Controls.Button _buttonThemeToggle;

		// Элементы левого сайдбара
		private Border _sidebarCard;
		private TextBlock _labelSidebarCompHeader;
		private TextBlock _labelSidebarDirsHeader;
		private TextBlock _labelSidebarDbHeader;
		private System.Windows.Controls.Button _buttonAddComputer;
		private System.Windows.Controls.Button _buttonManageCredentials;
		private System.Windows.Controls.Button _buttonAddDirectory;
		private StackPanel _panelComputersContainer;
		private StackPanel _panelDirectoriesContainer;
		private System.Windows.Controls.TextBox _textSqlServer;
		private System.Windows.Controls.TextBox _textSqlDatabase;
		private System.Windows.Controls.TextBox _textSqlTable;
		private System.Windows.Controls.CheckBox _checkAutoCreateTable;
		private System.Windows.Controls.Button _buttonSaveDbSettings;
		private System.Windows.Controls.Button _buttonClearDbTable;

		// Элементы главной области
		private TextBlock _labelMainHeader;
		private System.Windows.Controls.Button _buttonStopScan;
		private System.Windows.Controls.Button _buttonExportDb;
		private System.Windows.Controls.Button _buttonStartScan;

		// Карточки метрик (KPI)
		private Border _cardMetricTotal;
		private TextBlock _labelMetricTotalNumber;
		private TextBlock _labelMetricTotalTitle;
		private Border _cardMetricInserted;
		private TextBlock _labelMetricInsertedNumber;
		private TextBlock _labelMetricInsertedTitle;
		private Border _cardMetricUpdated;
		private TextBlock _labelMetricUpdatedNumber;
		private TextBlock _labelMetricUpdatedTitle;
		private Border _cardMetricProgress;
		private TextBlock _labelProgressSummary;
		private TextBlock _labelProgressPercent;
		private TextBlock _labelProgressCurrentNode;
		private System.Windows.Controls.ProgressBar _progressBarCustom;

		// Поиск и фильтрация
		private Border _panelSearchContainer;
		private System.Windows.Controls.TextBox _textSearchBox;
		private TextBlock _labelSearchPlaceholder;
		private System.Windows.Controls.Button _buttonClearSearch;
		private System.Windows.Controls.Button _buttonFilterLicense;

		// Сортировка таблицы сертификатов
		private string _sortColumnKey;
		private bool _isSortAscending;

		// Таблица DataGrid
		private System.Windows.Controls.DataGrid _gridCertificates;

		// Терминал журнала работы
		private Border _panelTerminalCard;
		private TextBlock _labelTerminalTitle;
		private System.Windows.Controls.RichTextBox _textTerminalLog;

		// Конструктор инициализации окна управления
		public ControlPanelForm()
		{
			_allCertificates = new List<CertificateRecord>();
			_displayCertificates = new List<CertificateDisplayItem>();
			_networkNodes = new List<NetworkNodeItem>();
			_filterOnlyWithLicense = false;
			_searchFilterQuery = string.Empty;
			_isScanningActive = false;
			_sortColumnKey = null;
			_isSortAscending = true;

			InitializeHandyControlTheme();
			InitializeComponentCustom();
			LoadConfigurationFromJsonFile();
			ApplyTheme(_isDarkTheme);
			RefreshCertificatesGrid();
		}

		// Инициализация ресурсного словаря HandyControl для динамического переключения тем оформления
		private void InitializeHandyControlTheme()
		{
			if (System.Windows.Application.Current == null)
			{
				System.Windows.Application app = new System.Windows.Application();
				app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			}

			foreach (System.Windows.ResourceDictionary dict in System.Windows.Application.Current.Resources.MergedDictionaries)
			{
				if (dict is HandyControl.Themes.Theme existingTheme)
				{
					_handyTheme = existingTheme;
					break;
				}
			}

			if (_handyTheme == null)
			{
				_handyTheme = new HandyControl.Themes.Theme();
				_handyTheme.Skin = SkinType.Default;
				System.Windows.Application.Current.Resources.MergedDictionaries.Add(_handyTheme);
			}

			try
			{
				ConfigHelper.Instance.SetLang("en");
			}
			catch
			{
			}
		}

		// Инициализация структуры окна, компоновки и элементов управления
		private void InitializeComponentCustom()
		{
			this.Title = "Сбор данных о СКЗИ";
			this.Width = 1260;
			this.Height = 820;
			this.MinWidth = 1000;
			this.MinHeight = 650;
			this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			this.ShowTitle = true;
			this.FontFamily = new FontFamily("Segoe UI");
			this.FontSize = 13;

			// Кнопка переключения темы в заголовке окна (NonClientArea)
			_buttonThemeToggle = new System.Windows.Controls.Button();
			_buttonThemeToggle.Content = "🌙  Тёмная тема";
			_buttonThemeToggle.Height = 30;
			_buttonThemeToggle.Padding = new Thickness(12, 4, 12, 4);
			_buttonThemeToggle.Margin = new Thickness(0, 0, 16, 0);
			_buttonThemeToggle.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonThemeToggle.Click += ButtonThemeToggle_Click;

			StackPanel headerStack = new StackPanel();
			headerStack.Orientation = System.Windows.Controls.Orientation.Horizontal;
			headerStack.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			headerStack.VerticalAlignment = VerticalAlignment.Center;
			headerStack.Children.Add(_buttonThemeToggle);

			this.NonClientAreaContent = headerStack;

			// Главная сетка: левая колонка (280px) и правая рабочая область (*)
			Grid rootGrid = new Grid();
			rootGrid.Margin = new Thickness(14, 8, 14, 14);
			rootGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(280) });
			rootGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(14) });
			rootGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			// 1. Левый сайдбар
			Border sidebar = CreateLeftSidebar();
			Grid.SetColumn(sidebar, 0);
			rootGrid.Children.Add(sidebar);

			// 2. Правая основная область
			Grid mainArea = CreateMainContentArea();
			Grid.SetColumn(mainArea, 2);
			rootGrid.Children.Add(mainArea);

			this.Content = rootGrid;
		}

		// Создание левого сайдбара с настройками ресурсов и подключения к СУБД
		private Border CreateLeftSidebar()
		{
			_sidebarCard = new Border();
			_sidebarCard.CornerRadius = new CornerRadius(10);
			_sidebarCard.BorderThickness = new Thickness(1);
			_sidebarCard.Padding = new Thickness(12);

			Grid sidebarGrid = new Grid();
			sidebarGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
			sidebarGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(14) });
			sidebarGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			// Блок 1: Компьютеры в сети
			Grid compBlock = new Grid();
			compBlock.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			compBlock.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			compBlock.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

			_labelSidebarCompHeader = new TextBlock();
			_labelSidebarCompHeader.Text = "Компьютеры в сети";
			_labelSidebarCompHeader.FontWeight = FontWeights.Bold;
			_labelSidebarCompHeader.FontSize = 12;
			_labelSidebarCompHeader.Margin = new Thickness(2, 0, 0, 8);
			Grid.SetRow(_labelSidebarCompHeader, 0);
			compBlock.Children.Add(_labelSidebarCompHeader);

			StackPanel compButtonsStack = new StackPanel();
			compButtonsStack.Margin = new Thickness(0, 0, 0, 8);

			_buttonAddComputer = new System.Windows.Controls.Button();
			_buttonAddComputer.Content = "+   Добавить компьютер...";
			_buttonAddComputer.Height = 28;
			_buttonAddComputer.Margin = new Thickness(0, 0, 0, 4);
			_buttonAddComputer.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonAddComputer.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
			_buttonAddComputer.Foreground = Brushes.White;
			_buttonAddComputer.FontWeight = FontWeights.SemiBold;
			_buttonAddComputer.Click += ButtonAddComputer_Click;
			compButtonsStack.Children.Add(_buttonAddComputer);

			_buttonManageCredentials = new System.Windows.Controls.Button();
			_buttonManageCredentials.Content = "🔑   Учетные записи ПК (net use)...";
			_buttonManageCredentials.Height = 28;
			_buttonManageCredentials.Margin = new Thickness(0, 0, 0, 0);
			_buttonManageCredentials.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonManageCredentials.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			_buttonManageCredentials.Foreground = Brushes.White;
			_buttonManageCredentials.FontWeight = FontWeights.SemiBold;
			_buttonManageCredentials.Click += ButtonManageCredentials_Click;
			compButtonsStack.Children.Add(_buttonManageCredentials);

			Grid.SetRow(compButtonsStack, 1);
			compBlock.Children.Add(compButtonsStack);

			_panelComputersContainer = new StackPanel();
			System.Windows.Controls.ScrollViewer scrollComp = new System.Windows.Controls.ScrollViewer();
			scrollComp.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			scrollComp.Content = _panelComputersContainer;
			Grid.SetRow(scrollComp, 2);
			compBlock.Children.Add(scrollComp);

			Grid.SetRow(compBlock, 0);
			sidebarGrid.Children.Add(compBlock);

			// Инициализация полей директорий для совместимости
			_labelSidebarDirsHeader = new TextBlock();
			_buttonAddDirectory = new System.Windows.Controls.Button();
			_panelDirectoriesContainer = new StackPanel();

			// Блок 2: База данных MSSQL
			StackPanel dbBlock = new StackPanel();
			_labelSidebarDbHeader = new TextBlock();
			_labelSidebarDbHeader.Text = "База данных MSSQL";
			_labelSidebarDbHeader.FontWeight = FontWeights.Bold;
			_labelSidebarDbHeader.FontSize = 12;
			_labelSidebarDbHeader.Margin = new Thickness(2, 0, 0, 8);
			dbBlock.Children.Add(_labelSidebarDbHeader);

			// Сервер
			DockPanel serverDock = new DockPanel();
			serverDock.Margin = new Thickness(0, 0, 0, 6);
			TextBlock serverLbl = new TextBlock() { Text = "Сервер", Width = 65, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
			_textSqlServer = new System.Windows.Controls.TextBox() { Text = "localhost", Height = 28 };
			serverDock.Children.Add(serverLbl);
			serverDock.Children.Add(_textSqlServer);
			dbBlock.Children.Add(serverDock);

			// База
			DockPanel dbDock = new DockPanel();
			dbDock.Margin = new Thickness(0, 0, 0, 6);
			TextBlock dbLbl = new TextBlock() { Text = "База", Width = 65, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
			_textSqlDatabase = new System.Windows.Controls.TextBox() { Text = "CertHarvesterDB", Height = 28 };
			dbDock.Children.Add(dbLbl);
			dbDock.Children.Add(_textSqlDatabase);
			dbBlock.Children.Add(dbDock);

			// Таблица
			DockPanel tableDock = new DockPanel();
			tableDock.Margin = new Thickness(0, 0, 0, 6);
			TextBlock tableLbl = new TextBlock() { Text = "Таблица", Width = 65, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
			_textSqlTable = new System.Windows.Controls.TextBox() { Text = "Certificates", Height = 28 };
			tableDock.Children.Add(tableLbl);
			tableDock.Children.Add(_textSqlTable);
			dbBlock.Children.Add(tableDock);

			// Авто-создание
			_checkAutoCreateTable = new System.Windows.Controls.CheckBox();
			_checkAutoCreateTable.Content = "Создавать таблицу при отсутствии";
			_checkAutoCreateTable.IsChecked = true;
			_checkAutoCreateTable.Margin = new Thickness(0, 4, 0, 8);
			_checkAutoCreateTable.Cursor = System.Windows.Input.Cursors.Hand;
			dbBlock.Children.Add(_checkAutoCreateTable);

			// Кнопка сохранения параметров БД
			_buttonSaveDbSettings = new System.Windows.Controls.Button();
			_buttonSaveDbSettings.Content = "Сохранить параметры БД";
			_buttonSaveDbSettings.Height = 30;
			_buttonSaveDbSettings.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonSaveDbSettings.FontWeight = FontWeights.SemiBold;
			_buttonSaveDbSettings.Click += ButtonSaveDbSettings_Click;
			dbBlock.Children.Add(_buttonSaveDbSettings);

			// Кнопка полной очистки таблицы в БД
			_buttonClearDbTable = new System.Windows.Controls.Button();
			_buttonClearDbTable.Content = "🗑   Очистить таблицу в БД";
			_buttonClearDbTable.Height = 30;
			_buttonClearDbTable.Margin = new Thickness(0, 6, 0, 0);
			_buttonClearDbTable.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonClearDbTable.Background = new SolidColorBrush(Color.FromRgb(185, 28, 28));
			_buttonClearDbTable.Foreground = Brushes.White;
			_buttonClearDbTable.FontWeight = FontWeights.SemiBold;
			_buttonClearDbTable.Click += ButtonClearDbTable_Click;
			dbBlock.Children.Add(_buttonClearDbTable);

			Grid.SetRow(dbBlock, 2);
			sidebarGrid.Children.Add(dbBlock);

			_sidebarCard.Child = sidebarGrid;
			return _sidebarCard;
		}

		// Создание правой основной области с заголовком, карточками показателей, таблицей и терминалом
		private Grid CreateMainContentArea()
		{
			Grid mainGrid = new Grid();
			mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 0: Заголовок и кнопки
			mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: Карточки метрик (KPI)
			mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 2: Поиск и фильтр
			mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 3: DataGrid
			mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(170) }); // 4: Терминал журнала работы

			// Строка 0: Заголовок "Сертификаты" и кнопки управления действиями
			DockPanel headerDock = new DockPanel();
			headerDock.Margin = new Thickness(0, 0, 0, 12);

			_labelMainHeader = new TextBlock();
			_labelMainHeader.Text = "Сертификаты";
			_labelMainHeader.FontSize = 24;
			_labelMainHeader.FontWeight = FontWeights.Bold;
			_labelMainHeader.VerticalAlignment = VerticalAlignment.Center;
			DockPanel.SetDock(_labelMainHeader, System.Windows.Controls.Dock.Left);
			headerDock.Children.Add(_labelMainHeader);

			StackPanel actionButtons = new StackPanel();
			actionButtons.Orientation = System.Windows.Controls.Orientation.Horizontal;
			actionButtons.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;

			_buttonStopScan = new System.Windows.Controls.Button();
			_buttonStopScan.Content = "Остановить";
			_buttonStopScan.Height = 36;
			_buttonStopScan.Padding = new Thickness(16, 0, 16, 0);
			_buttonStopScan.Margin = new Thickness(0, 0, 8, 0);
			_buttonStopScan.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonStopScan.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
			_buttonStopScan.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
			_buttonStopScan.FontWeight = FontWeights.SemiBold;
			_buttonStopScan.Click += ButtonStopScan_Click;
			actionButtons.Children.Add(_buttonStopScan);

			_buttonExportDb = new System.Windows.Controls.Button();
			_buttonExportDb.Content = "Отправить в БД";
			_buttonExportDb.Height = 36;
			_buttonExportDb.Padding = new Thickness(16, 0, 16, 0);
			_buttonExportDb.Margin = new Thickness(0, 0, 8, 0);
			_buttonExportDb.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonExportDb.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			_buttonExportDb.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
			_buttonExportDb.FontWeight = FontWeights.SemiBold;
			_buttonExportDb.Click += ButtonExportDb_Click;
			actionButtons.Children.Add(_buttonExportDb);

			_buttonStartScan = new System.Windows.Controls.Button();
			_buttonStartScan.Content = "▶   Сканировать";
			_buttonStartScan.Height = 36;
			_buttonStartScan.Padding = new Thickness(18, 0, 18, 0);
			_buttonStartScan.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonStartScan.Foreground = Brushes.White;
			_buttonStartScan.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
			_buttonStartScan.FontWeight = FontWeights.Bold;
			_buttonStartScan.Click += ButtonStartScan_Click;
			actionButtons.Children.Add(_buttonStartScan);

			DockPanel.SetDock(actionButtons, System.Windows.Controls.Dock.Right);
			headerDock.Children.Add(actionButtons);

			Grid.SetRow(headerDock, 0);
			mainGrid.Children.Add(headerDock);

			// Строка 1: Ряд 4 адаптивных карточек метрик (KPI)
			Grid metricsGrid = CreateMetricCards();
			metricsGrid.Margin = new Thickness(0, 0, 0, 12);
			Grid.SetRow(metricsGrid, 1);
			mainGrid.Children.Add(metricsGrid);

			// Строка 2: Панель поиска и переключатель лицензий
			DockPanel searchBar = CreateSearchAndFilterBar();
			searchBar.Margin = new Thickness(0, 0, 0, 12);
			Grid.SetRow(searchBar, 2);
			mainGrid.Children.Add(searchBar);

			// Строка 3: Таблица сертификатов DataGrid
			_gridCertificates = CreateCertificatesTable();
			_gridCertificates.Margin = new Thickness(0, 0, 0, 12);
			Grid.SetRow(_gridCertificates, 3);
			mainGrid.Children.Add(_gridCertificates);

			// Строка 4: Терминал журнала работы
			_panelTerminalCard = CreateTerminalCard();
			Grid.SetRow(_panelTerminalCard, 4);
			mainGrid.Children.Add(_panelTerminalCard);

			return mainGrid;
		}

		// Создание 4 карточек ключевых показателей процесса сканирования
		private Grid CreateMetricCards()
		{
			Grid cardsGrid = new Grid();
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(12) });
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(12) });
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(12) });
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			// Карточка 1: Найдено
			_cardMetricTotal = CreateBaseMetricCard(out _labelMetricTotalNumber, out _labelMetricTotalTitle, "0", "сертификатов найдено", null);
			Grid.SetColumn(_cardMetricTotal, 0);
			cardsGrid.Children.Add(_cardMetricTotal);

			// Карточка 2: Добавлено
			_cardMetricInserted = CreateBaseMetricCard(out _labelMetricInsertedNumber, out _labelMetricInsertedTitle, "0", "добавлено в таблицу", new SolidColorBrush(Color.FromRgb(16, 185, 129)));
			Grid.SetColumn(_cardMetricInserted, 2);
			cardsGrid.Children.Add(_cardMetricInserted);

			// Карточка 3: Обновлено
			_cardMetricUpdated = CreateBaseMetricCard(out _labelMetricUpdatedNumber, out _labelMetricUpdatedTitle, "0", "обновлено (без дублей)", new SolidColorBrush(Color.FromRgb(59, 130, 246)));
			Grid.SetColumn(_cardMetricUpdated, 4);
			cardsGrid.Children.Add(_cardMetricUpdated);

			// Карточка 4: Прогресс
			_cardMetricProgress = new Border();
			_cardMetricProgress.CornerRadius = new CornerRadius(10);
			_cardMetricProgress.BorderThickness = new Thickness(1);
			_cardMetricProgress.Padding = new Thickness(14, 10, 14, 10);
			_cardMetricProgress.Height = 84;

			Grid progressGrid = new Grid();
			progressGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			progressGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			progressGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			DockPanel progTop = new DockPanel();
			_labelProgressSummary = new TextBlock() { Text = "0 из 0 ПК", FontWeight = FontWeights.Bold, FontSize = 14 };
			_labelProgressPercent = new TextBlock() { Text = "0%", FontSize = 12, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
			DockPanel.SetDock(_labelProgressSummary, System.Windows.Controls.Dock.Left);
			DockPanel.SetDock(_labelProgressPercent, System.Windows.Controls.Dock.Right);
			progTop.Children.Add(_labelProgressSummary);
			progTop.Children.Add(_labelProgressPercent);
			Grid.SetRow(progTop, 0);
			progressGrid.Children.Add(progTop);

			_progressBarCustom = new System.Windows.Controls.ProgressBar();
			_progressBarCustom.Height = 8;
			_progressBarCustom.Margin = new Thickness(0, 8, 0, 6);
			_progressBarCustom.Value = 0;
			_progressBarCustom.Template = CreateModernProgressBarTemplate();
			_progressBarCustom.Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199));
			Grid.SetRow(_progressBarCustom, 1);
			progressGrid.Children.Add(_progressBarCustom);

			_labelProgressCurrentNode = new TextBlock() { Text = "Готов к запуску", FontSize = 11 };
			Grid.SetRow(_labelProgressCurrentNode, 2);
			progressGrid.Children.Add(_labelProgressCurrentNode);

			_cardMetricProgress.Child = progressGrid;
			Grid.SetColumn(_cardMetricProgress, 6);
			cardsGrid.Children.Add(_cardMetricProgress);

			return cardsGrid;
		}

		// Создание чистого шаблона полосы прогресса без отображения процентов внутри индикатора
		private ControlTemplate CreateModernProgressBarTemplate()
		{
			string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
							xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
							TargetType='ProgressBar'>
				<Grid Name='TemplateRoot' ClipToBounds='True' SnapsToDevicePixels='True'>
					<Border x:Name='PART_Track' Background='{TemplateBinding Background}' CornerRadius='4' />
					<Border x:Name='PART_Indicator' HorizontalAlignment='Left' Background='{TemplateBinding Foreground}' CornerRadius='4' />
				</Grid>
			</ControlTemplate>";

			return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
		}

		// Вспомогательный метод создания базовой карточки метрики
		private Border CreateBaseMetricCard(out TextBlock numberBlock, out TextBlock titleBlock, string number, string title, Brush numberColor)
		{
			Border card = new Border();
			card.CornerRadius = new CornerRadius(10);
			card.BorderThickness = new Thickness(1);
			card.Padding = new Thickness(14, 8, 14, 8);
			card.Height = 84;

			StackPanel stack = new StackPanel();
			numberBlock = new TextBlock();
			numberBlock.Text = number;
			numberBlock.FontSize = 24;
			numberBlock.FontWeight = FontWeights.Bold;
			if (numberColor != null)
			{
				numberBlock.Foreground = numberColor;
			}
			stack.Children.Add(numberBlock);

			titleBlock = new TextBlock();
			titleBlock.Text = title;
			titleBlock.FontSize = 11;
			stack.Children.Add(titleBlock);

			card.Child = stack;
			return card;
		}

		// Создание панели с поисковой строкой и кнопкой фильтрации
		private DockPanel CreateSearchAndFilterBar()
		{
			DockPanel panel = new DockPanel();

			_buttonFilterLicense = new System.Windows.Controls.Button();
			_buttonFilterLicense.Content = "Только со встроенной лицензией";
			_buttonFilterLicense.Height = 34;
			_buttonFilterLicense.Padding = new Thickness(16, 0, 16, 0);
			_buttonFilterLicense.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonFilterLicense.FontWeight = FontWeights.Regular;
			_buttonFilterLicense.Click += ButtonFilterLicense_Click;
			DockPanel.SetDock(_buttonFilterLicense, System.Windows.Controls.Dock.Right);
			panel.Children.Add(_buttonFilterLicense);

			_panelSearchContainer = new Border();
			_panelSearchContainer.CornerRadius = new CornerRadius(8);
			_panelSearchContainer.BorderThickness = new Thickness(1);
			_panelSearchContainer.Height = 34;
			_panelSearchContainer.Margin = new Thickness(0, 0, 12, 0);

			DockPanel searchInner = new DockPanel();
			TextBlock icon = new TextBlock() { Text = "🔍", Margin = new Thickness(10, 8, 8, 8), FontSize = 12 };
			DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left);
			searchInner.Children.Add(icon);

			_buttonClearSearch = new System.Windows.Controls.Button();
			_buttonClearSearch.Content = "✕";
			_buttonClearSearch.FontSize = 11;
			_buttonClearSearch.Width = 26;
			_buttonClearSearch.Height = 26;
			_buttonClearSearch.Margin = new Thickness(0, 0, 6, 0);
			_buttonClearSearch.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonClearSearch.Background = Brushes.Transparent;
			_buttonClearSearch.BorderThickness = new Thickness(0);
			_buttonClearSearch.Visibility = Visibility.Collapsed;
			_buttonClearSearch.Click += (s, e) =>
			{
				_textSearchBox.Text = string.Empty;
				_textSearchBox.Focus();
			};
			DockPanel.SetDock(_buttonClearSearch, System.Windows.Controls.Dock.Right);
			searchInner.Children.Add(_buttonClearSearch);

			Grid inputGrid = new Grid();
			inputGrid.VerticalAlignment = VerticalAlignment.Center;

			_labelSearchPlaceholder = new TextBlock();
			_labelSearchPlaceholder.Text = "Поиск по номеру, владельцу или УЦ...";
			_labelSearchPlaceholder.FontSize = 13;
			_labelSearchPlaceholder.VerticalAlignment = VerticalAlignment.Center;
			_labelSearchPlaceholder.Margin = new Thickness(14, 0, 0, 0);
			_labelSearchPlaceholder.IsHitTestVisible = false;
			inputGrid.Children.Add(_labelSearchPlaceholder);

			_textSearchBox = new System.Windows.Controls.TextBox();
			_textSearchBox.BorderThickness = new Thickness(0);
			_textSearchBox.Background = Brushes.Transparent;
			_textSearchBox.VerticalContentAlignment = VerticalAlignment.Center;
			_textSearchBox.Padding = new Thickness(4, 0, 0, 0);
			_textSearchBox.FontSize = 13;
			_textSearchBox.TextChanged += (s, e) =>
			{
				_searchFilterQuery = _textSearchBox.Text.Trim();
				bool hasText = !string.IsNullOrEmpty(_textSearchBox.Text);
				_labelSearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
				_buttonClearSearch.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
				RefreshCertificatesGrid();
			};
			inputGrid.Children.Add(_textSearchBox);

			searchInner.Children.Add(inputGrid);

			_panelSearchContainer.Child = searchInner;
			panel.Children.Add(_panelSearchContainer);

			return panel;
		}

		// Создание таблицы сертификатов на базе WPF DataGrid
		private System.Windows.Controls.DataGrid CreateCertificatesTable()
		{
			System.Windows.Controls.DataGrid grid = new System.Windows.Controls.DataGrid();
			grid.AutoGenerateColumns = false;
			grid.IsReadOnly = true;
			grid.SelectionMode = DataGridSelectionMode.Single;
			grid.HeadersVisibility = DataGridHeadersVisibility.Column;
			grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
			grid.CanUserResizeRows = false;
			grid.RowHeight = 34;
			grid.ColumnHeaderHeight = 38;
			grid.BorderThickness = new Thickness(1);
			grid.EnableRowVirtualization = true;
			grid.CanUserSortColumns = true;
			grid.Sorting += GridCertificates_Sorting;

			// 1. Серийный номер
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Серийный номер",
				Binding = new System.Windows.Data.Binding("SerialNumber"),
				SortMemberPath = "SerialNumber",
				Width = new DataGridLength(170)
			});

			// 2. Дата внесения
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Дата внесения",
				Binding = new System.Windows.Data.Binding("DateInsertedDisplay"),
				SortMemberPath = "DateInserted",
				Width = new DataGridLength(135)
			});

			// 3. Дата обновления
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Дата обновления",
				Binding = new System.Windows.Data.Binding("DateUpdatedDisplay"),
				SortMemberPath = "DateUpdated",
				Width = new DataGridLength(135)
			});

			// 4. Имя владельца
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Имя владельца",
				Binding = new System.Windows.Data.Binding("OwnerName"),
				SortMemberPath = "OwnerName",
				Width = new DataGridLength(230),
				MinWidth = 150
			});

			// 5. Удостоверяющий центр
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Удостоверяющий центр",
				Binding = new System.Windows.Data.Binding("Issuer"),
				SortMemberPath = "Issuer",
				Width = new DataGridLength(210),
				MinWidth = 150
			});

			// 6. Версия КриптоПро
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Версия КриптоПро",
				Binding = new System.Windows.Data.Binding("CryptoProVersion"),
				SortMemberPath = "CryptoProVersion",
				Width = new DataGridLength(125)
			});

			// 7. Лицензия встроена
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Лицензия встроена",
				Binding = new System.Windows.Data.Binding("LicenseDisplay"),
				SortMemberPath = "HasEmbeddedLicense",
				Width = new DataGridLength(125)
			});

			return grid;
		}

		// Создание карточки темного терминала для журнала работы
		private Border CreateTerminalCard()
		{
			Border terminalBorder = new Border();
			terminalBorder.CornerRadius = new CornerRadius(10);
			terminalBorder.BorderThickness = new Thickness(1);
			terminalBorder.Background = new SolidColorBrush(Color.FromRgb(22, 24, 29));
			terminalBorder.Padding = new Thickness(12, 8, 12, 10);

			Grid termGrid = new Grid();
			termGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			termGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

			_labelTerminalTitle = new TextBlock();
			_labelTerminalTitle.Text = "Журнал работы";
			_labelTerminalTitle.FontWeight = FontWeights.Bold;
			_labelTerminalTitle.FontSize = 11;
			_labelTerminalTitle.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
			_labelTerminalTitle.Margin = new Thickness(2, 0, 0, 6);
			Grid.SetRow(_labelTerminalTitle, 0);
			termGrid.Children.Add(_labelTerminalTitle);

			_textTerminalLog = new System.Windows.Controls.RichTextBox();
			_textTerminalLog.Background = new SolidColorBrush(Color.FromRgb(22, 24, 29));
			_textTerminalLog.Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235));
			_textTerminalLog.BorderThickness = new Thickness(0);
			_textTerminalLog.FontFamily = new FontFamily("Consolas");
			_textTerminalLog.FontSize = 12;
			_textTerminalLog.IsReadOnly = true;
			_textTerminalLog.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			_textTerminalLog.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

			Grid.SetRow(_textTerminalLog, 1);
			termGrid.Children.Add(_textTerminalLog);

			terminalBorder.Child = termGrid;
			return terminalBorder;
		}

		// Обработчик переключения темы оформления с тихим сохранением настроек
		private void ButtonThemeToggle_Click(object sender, RoutedEventArgs e)
		{
			_isDarkTheme = !_isDarkTheme;
			_currentConfig.IsDarkTheme = _isDarkTheme;
			ConfigManager.SaveConfig(_currentConfig, true);

			ApplyTheme(_isDarkTheme);
			RefreshCertificatesGrid();
		}

		// Применение визуального стиля и палитры ко всем компонентам окна
		private void ApplyTheme(bool isDark)
		{
			if (_handyTheme != null)
			{
				_handyTheme.Skin = isDark ? SkinType.Dark : SkinType.Default;
			}

			SolidColorBrush windowBg = isDark ? new SolidColorBrush(Color.FromRgb(17, 24, 39)) : new SolidColorBrush(Color.FromRgb(244, 246, 249));
			SolidColorBrush cardBg = isDark ? new SolidColorBrush(Color.FromRgb(31, 41, 55)) : Brushes.White;
			SolidColorBrush cardBorder = isDark ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(229, 231, 235));
			SolidColorBrush textPrimary = isDark ? new SolidColorBrush(Color.FromRgb(243, 244, 246)) : new SolidColorBrush(Color.FromRgb(17, 24, 39));
			SolidColorBrush textSecondary = isDark ? new SolidColorBrush(Color.FromRgb(156, 163, 175)) : new SolidColorBrush(Color.FromRgb(107, 114, 128));
			SolidColorBrush inputBg = isDark ? new SolidColorBrush(Color.FromRgb(17, 24, 39)) : new SolidColorBrush(Color.FromRgb(249, 250, 251));

			// Окно и шапка
			this.Background = windowBg;
			this.NonClientAreaBackground = windowBg;
			this.NonClientAreaForeground = textPrimary;

			_buttonThemeToggle.Content = isDark ? "☀️  Светлая тема" : "🌙  Тёмная тема";
			_buttonThemeToggle.Background = isDark ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
			_buttonThemeToggle.Foreground = textPrimary;

			// Сайдбар
			_sidebarCard.Background = cardBg;
			_sidebarCard.BorderBrush = cardBorder;
			_labelSidebarCompHeader.Foreground = textSecondary;
			_labelSidebarDirsHeader.Foreground = textSecondary;
			_labelSidebarDbHeader.Foreground = textSecondary;

			_textSqlServer.Background = inputBg;
			_textSqlServer.Foreground = textPrimary;
			_textSqlDatabase.Background = inputBg;
			_textSqlDatabase.Foreground = textPrimary;
			_textSqlTable.Background = inputBg;
			_textSqlTable.Foreground = textPrimary;

			_buttonSaveDbSettings.Background = isDark ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(243, 244, 246));
			_buttonSaveDbSettings.Foreground = textPrimary;

			// Главная панель
			_labelMainHeader.Foreground = textPrimary;

			_buttonStopScan.Background = isDark ? new SolidColorBrush(Color.FromRgb(69, 26, 26)) : new SolidColorBrush(Color.FromRgb(254, 242, 242));
			_buttonStopScan.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(248, 113, 113)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));

			_buttonExportDb.Background = isDark ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(239, 246, 255));
			_buttonExportDb.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));

			// Карточки метрик
			ApplyCardTheme(_cardMetricTotal, cardBg, cardBorder, _labelMetricTotalNumber, textPrimary, _labelMetricTotalTitle, textSecondary);
			ApplyCardTheme(_cardMetricInserted, cardBg, cardBorder, _labelMetricInsertedNumber, null, _labelMetricInsertedTitle, textSecondary);
			ApplyCardTheme(_cardMetricUpdated, cardBg, cardBorder, _labelMetricUpdatedNumber, null, _labelMetricUpdatedTitle, textSecondary);

			_cardMetricProgress.Background = cardBg;
			_cardMetricProgress.BorderBrush = cardBorder;
			_labelProgressSummary.Foreground = textPrimary;
			_labelProgressPercent.Foreground = textSecondary;
			_labelProgressCurrentNode.Foreground = textSecondary;
			_progressBarCustom.Background = isDark ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
			_progressBarCustom.Foreground = new SolidColorBrush(Color.FromRgb(14, 165, 233));

			// Поиск и фильтрация
			_panelSearchContainer.Background = cardBg;
			_panelSearchContainer.BorderBrush = cardBorder;
			_textSearchBox.Foreground = textPrimary;
			_textSearchBox.CaretBrush = textPrimary;

			if (_labelSearchPlaceholder != null)
			{
				_labelSearchPlaceholder.Foreground = isDark 
					? new SolidColorBrush(Color.FromArgb(125, 243, 244, 246)) 
					: new SolidColorBrush(Color.FromArgb(125, 17, 24, 39));
			}

			if (_buttonClearSearch != null)
			{
				_buttonClearSearch.Foreground = textSecondary;
			}

			if (_filterOnlyWithLicense == false)
			{
				_buttonFilterLicense.Background = isDark ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(239, 246, 255));
				_buttonFilterLicense.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
			}

			// DataGrid (динамическая настройка строк, заголовков и ячеек)
			ApplyDataGridTheme(isDark);

			// Терминал журнала (постоянно монолитно темный)
			_panelTerminalCard.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(31, 41, 55));
			_labelTerminalTitle.Foreground = textSecondary;

			RenderComputersList();
			RenderDirectoriesList();
		}

		// Настройка динамических стилей, цветов строк и колонок таблицы сертификатов под выбранную тему
		private void ApplyDataGridTheme(bool isDark)
		{
			Color gridBg = isDark ? Color.FromRgb(31, 41, 55) : Colors.White;
			Color gridBorder = isDark ? Color.FromRgb(55, 65, 81) : Color.FromRgb(229, 231, 235);
			Color rowBg = isDark ? Color.FromRgb(31, 41, 55) : Colors.White;
			Color altRowBg = isDark ? Color.FromRgb(24, 33, 47) : Color.FromRgb(249, 250, 251);
			Color gridLines = isDark ? Color.FromRgb(55, 65, 81) : Color.FromRgb(229, 231, 235);
			Color textPrimary = isDark ? Color.FromRgb(243, 244, 246) : Color.FromRgb(17, 24, 39);
			Color textSecondary = isDark ? Color.FromRgb(156, 163, 175) : Color.FromRgb(107, 114, 128);
			Color headerBg = isDark ? Color.FromRgb(17, 24, 39) : Color.FromRgb(248, 250, 252);
			Color headerFg = isDark ? Color.FromRgb(209, 213, 219) : Color.FromRgb(75, 85, 99);
			Color rowHover = isDark ? Color.FromRgb(55, 65, 81) : Color.FromRgb(243, 244, 246);
			Color rowSelectedBg = isDark ? Color.FromRgb(30, 58, 138) : Color.FromRgb(219, 234, 254);
			Color rowSelectedFg = isDark ? Colors.White : Color.FromRgb(30, 58, 138);

			if (System.Windows.Application.Current != null)
			{
				System.Windows.Application.Current.Resources["PrimaryTextBrush"] = new SolidColorBrush(textPrimary);
				System.Windows.Application.Current.Resources["RegionBrush"] = new SolidColorBrush(gridBg);
				System.Windows.Application.Current.Resources["BorderBrush"] = new SolidColorBrush(gridBorder);
				System.Windows.Application.Current.Resources["SecondaryTextBrush"] = new SolidColorBrush(textSecondary);
			}

			_gridCertificates.Background = new SolidColorBrush(gridBg);
			_gridCertificates.BorderBrush = new SolidColorBrush(gridBorder);
			_gridCertificates.RowBackground = new SolidColorBrush(rowBg);
			_gridCertificates.AlternatingRowBackground = new SolidColorBrush(altRowBg);
			_gridCertificates.HorizontalGridLinesBrush = new SolidColorBrush(gridLines);

			// Стиль заголовков колонок таблицы
			Style headerStyle = new Style(typeof(DataGridColumnHeader));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HeightProperty, 38.0));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(headerBg)));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, new SolidColorBrush(headerFg)));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 12.0));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, new SolidColorBrush(gridBorder)));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(10, 0, 10, 0)));
			headerStyle.Setters.Add(new Setter(DataGridColumnHeader.VerticalContentAlignmentProperty, VerticalAlignment.Center));
			_gridCertificates.ColumnHeaderStyle = headerStyle;

			// Стиль строк таблицы с эффектами наведения курсора и выделения
			Style rowStyle = new Style(typeof(DataGridRow));
			rowStyle.Setters.Add(new Setter(DataGridRow.HeightProperty, 34.0));
			rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
			rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, new SolidColorBrush(textPrimary)));

			Trigger hoverTrigger = new Trigger() { Property = DataGridRow.IsMouseOverProperty, Value = true };
			hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(rowHover)));
			rowStyle.Triggers.Add(hoverTrigger);

			Trigger selectedTrigger = new Trigger() { Property = DataGridRow.IsSelectedProperty, Value = true };
			selectedTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(rowSelectedBg)));
			selectedTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, new SolidColorBrush(rowSelectedFg)));
			rowStyle.Triggers.Add(selectedTrigger);
			_gridCertificates.RowStyle = rowStyle;

			// Стиль ячеек таблицы
			Style cellStyle = new Style(typeof(DataGridCell));
			cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
			cellStyle.Setters.Add(new Setter(DataGridCell.VerticalContentAlignmentProperty, VerticalAlignment.Center));
			cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(textPrimary)));
			cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));

			Trigger cellSelectedTrigger = new Trigger() { Property = DataGridCell.IsSelectedProperty, Value = true };
			cellSelectedTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(rowSelectedBg)));
			cellSelectedTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(rowSelectedFg)));
			cellStyle.Triggers.Add(cellSelectedTrigger);
			_gridCertificates.CellStyle = cellStyle;

			// Стиль текстовых блоков внутри стандартных колонок (с привязкой цвета выделения к строке)
			Style textColStyle = new Style(typeof(TextBlock));
			textColStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
			textColStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(10, 0, 10, 0)));
			textColStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(textPrimary)));
			textColStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));

			DataTrigger selTextTrigger = new DataTrigger()
			{
				Binding = new System.Windows.Data.Binding("IsSelected") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1) },
				Value = true
			};
			selTextTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(rowSelectedFg)));
			textColStyle.Triggers.Add(selTextTrigger);

			for (int c = 0; c < 6; c++)
			{
				if (c < _gridCertificates.Columns.Count && _gridCertificates.Columns[c] is DataGridTextColumn txtCol)
				{
					txtCol.ElementStyle = textColStyle;
				}
			}

			// Стиль колонки наличия встроенной лицензии с цветной индикацией (зеленый / серый)
			Style licenseColStyle = new Style(typeof(TextBlock));
			licenseColStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
			licenseColStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(10, 0, 10, 0)));

			DataTrigger licTrueTrigger = new DataTrigger()
			{
				Binding = new System.Windows.Data.Binding("HasEmbeddedLicense"),
				Value = true
			};
			licTrueTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(16, 185, 129))));
			licTrueTrigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
			licenseColStyle.Triggers.Add(licTrueTrigger);

			DataTrigger licFalseTrigger = new DataTrigger()
			{
				Binding = new System.Windows.Data.Binding("HasEmbeddedLicense"),
				Value = false
			};
			licFalseTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(textSecondary)));
			licenseColStyle.Triggers.Add(licFalseTrigger);

			if (_gridCertificates.Columns.Count > 6 && _gridCertificates.Columns[6] is DataGridTextColumn licCol)
			{
				licCol.ElementStyle = licenseColStyle;
			}
		}

		// Применение темы к отдельной карточке метрики
		private void ApplyCardTheme(Border card, Brush bg, Brush border, TextBlock num, Brush numColor, TextBlock title, Brush titleColor)
		{
			card.Background = bg;
			card.BorderBrush = border;
			if (numColor != null)
			{
				num.Foreground = numColor;
			}
			title.Foreground = titleColor;
		}

		// Загрузка реальных конфигурационных параметров и сохраненных сертификатов из config.json
		private void LoadConfigurationFromJsonFile()
		{
			_currentConfig = ConfigManager.LoadConfig();
			_isDarkTheme = _currentConfig.IsDarkTheme;

			_networkNodes.Clear();
			for (int i = 0; i < _currentConfig.NetworkComputers.Count; i++)
			{
				string compEntry = _currentConfig.NetworkComputers[i];
				NetworkNodeItem node = ParseNetworkNode(compEntry);
				if (_currentConfig.ComputerCredentials != null)
				{
					for (int c = 0; c < _currentConfig.ComputerCredentials.Count; c++)
					{
						NetworkComputerCredential cred = _currentConfig.ComputerCredentials[c];
						if (cred != null && (string.Equals(cred.ComputerAddress, node.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(cred.ComputerAddress, node.IpAddress, StringComparison.OrdinalIgnoreCase)))
						{
							node.Username = cred.Username;
							node.Password = cred.Password;
							node.IsConnected = cred.IsConnected;
							if (cred.CustomDirectories != null)
							{
								node.CustomDirectories = new List<string>(cred.CustomDirectories);
							}
							break;
						}
					}
				}

				if (node.CustomDirectories.Count == 0 && _currentConfig.ScanDirectories != null && _currentConfig.ScanDirectories.Count > 0)
				{
					node.CustomDirectories = new List<string>(_currentConfig.ScanDirectories);
				}
				_networkNodes.Add(node);
			}
			RenderComputersList();
			RenderDirectoriesList();

			ParseConnectionStringToFields(_currentConfig.ConnectionString);
			_textSqlTable.Text = string.IsNullOrEmpty(_currentConfig.TableName) ? "Certificates" : _currentConfig.TableName;
			_checkAutoCreateTable.IsChecked = _currentConfig.AutoCreateTable;

			_allCertificates.Clear();
			if (_currentConfig.Certificates != null && _currentConfig.Certificates.Count > 0)
			{
				for (int c = 0; c < _currentConfig.Certificates.Count; c++)
				{
					_allCertificates.Add(_currentConfig.Certificates[c]);
				}
				AppendTerminalLog(string.Format("{0} - Из конфигурации config.json загружено ранее сохраненных сертификатов: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), _allCertificates.Count), Color.FromRgb(96, 165, 250));
			}
			else
			{
				AppendTerminalLog(string.Format("{0} - Конфигурация успешно загружена. Сертификаты готовы к первому сканированию", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(156, 163, 175));
			}

			UpdateProgressCardSummary();
		}

		// Парсинг строки сетевого компьютера из файла конфигурации
		private NetworkNodeItem ParseNetworkNode(string entry)
		{
			if (string.IsNullOrWhiteSpace(entry))
			{
				return new NetworkNodeItem("Неизвестный ПК", "127.0.0.1", "В очереди");
			}

			string trimmed = entry.Trim();
			string name = trimmed;
			string ip = "127.0.0.1";

			if (trimmed.Contains("(") && trimmed.Contains(")"))
			{
				int startParen = trimmed.IndexOf('(');
				int endParen = trimmed.IndexOf(')');
				if (endParen > startParen)
				{
					name = trimmed.Substring(0, startParen).Trim();
					ip = trimmed.Substring(startParen + 1, endParen - startParen - 1).Trim();
				}
			}
			else if (trimmed.Contains(";"))
			{
				string[] parts = trimmed.Split(';');
				name = parts[0].Trim();
				if (parts.Length > 1)
				{
					ip = parts[1].Trim();
				}
			}

			return new NetworkNodeItem(name, ip, "В очереди");
		}

		// Разбор строки подключения к БД на отдельные визуальные поля формы
		private void ParseConnectionStringToFields(string connString)
		{
			if (string.IsNullOrWhiteSpace(connString))
			{
				_textSqlServer.Text = "localhost";
				_textSqlDatabase.Text = "CertHarvesterDB";
				return;
			}

			string[] parts = connString.Split(';');
			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				if (part.StartsWith("Server=", StringComparison.OrdinalIgnoreCase) || part.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
				{
					int equalPos = part.IndexOf('=');
					_textSqlServer.Text = part.Substring(equalPos + 1).Trim();
				}
				else if (part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) || part.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
				{
					int equalPos = part.IndexOf('=');
					_textSqlDatabase.Text = part.Substring(equalPos + 1).Trim();
				}
			}
		}

		// Обновление карточки прогресса сканирования компьютеров
		private void UpdateProgressCardSummary()
		{
			int totalNodes = _networkNodes.Count;
			_labelProgressSummary.Text = string.Format("0 из {0} ПК", totalNodes);
			_progressBarCustom.Value = 0;
			_labelProgressPercent.Text = "0%";
			_labelProgressCurrentNode.Text = totalNodes > 0 ? "Готов к запуску" : "Добавьте ПК";
		}

		// Отрисовка списка карточек компьютеров сети в левом сайдбаре
		private void RenderComputersList()
		{
			_panelComputersContainer.Children.Clear();

			Brush cardBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(17, 24, 39)) : new SolidColorBrush(Color.FromRgb(249, 250, 251));
			Brush cardBorder = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(229, 231, 235));
			Brush nameText = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(243, 244, 246)) : new SolidColorBrush(Color.FromRgb(17, 24, 39));
			Brush ipText = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(156, 163, 175)) : new SolidColorBrush(Color.FromRgb(107, 114, 128));

			for (int i = 0; i < _networkNodes.Count; i++)
			{
				NetworkNodeItem node = _networkNodes[i];
				Border itemBorder = new Border();
				itemBorder.Background = cardBg;
				itemBorder.BorderBrush = cardBorder;
				itemBorder.BorderThickness = new Thickness(1);
				itemBorder.CornerRadius = new CornerRadius(6);
				itemBorder.Margin = new Thickness(0, 0, 0, 6);
				itemBorder.Padding = new Thickness(6);

				System.Windows.Controls.ContextMenu cm = new System.Windows.Controls.ContextMenu();
				System.Windows.Controls.MenuItem credItem = new System.Windows.Controls.MenuItem() { Header = "🔑 Учетные записи (net use)..." };
				credItem.Click += (s, e) => ButtonManageCredentials_Click(s, e);
				cm.Items.Add(credItem);

				System.Windows.Controls.MenuItem connectItem = new System.Windows.Controls.MenuItem() { Header = "🔌 Подключить сейчас (net use)" };
				connectItem.Click += async (s, e) =>
				{
					string outMsg = string.Empty;
					bool ok = await Task.Run(() => CertificateScanner.EstablishNetworkSession(node.IpAddress, node.Username, node.Password, out outMsg));
					node.IsConnected = ok;
					SyncAndSaveComputersToConfig();
					RenderComputersList();
					if (ok == true)
					{
						AppendTerminalLog(string.Format("{0} - Успешное net use подключение к {1} ({2}) под пользователем {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress, node.Username), Color.FromRgb(52, 211, 153));
					}
					else
					{
						AppendTerminalLog(string.Format("{0} - Ошибка net use к {1} ({2}): {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress, outMsg), Color.FromRgb(248, 113, 113));
					}
				};
				cm.Items.Add(connectItem);

				System.Windows.Controls.MenuItem delItem = new System.Windows.Controls.MenuItem() { Header = "Удалить компьютер" };
				delItem.Click += (s, e) =>
				{
					_networkNodes.Remove(node);
					SyncAndSaveComputersToConfig();
					RenderComputersList();
					UpdateProgressCardSummary();
				};
				cm.Items.Add(delItem);
				itemBorder.ContextMenu = cm;

				DockPanel dock = new DockPanel();
				TextBlock icon = new TextBlock() { Text = "💻", FontSize = 14, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
				DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left);
				dock.Children.Add(icon);

				Border badge = CreateStatusBadge(node.Status);
				DockPanel.SetDock(badge, System.Windows.Controls.Dock.Right);
				dock.Children.Add(badge);

				StackPanel textStack = new StackPanel();
				TextBlock nameBlock = new TextBlock() { Text = node.Name, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = nameText };
				TextBlock ipBlock = new TextBlock() { Text = node.IpAddress, FontSize = 10, Foreground = ipText };
				textStack.Children.Add(nameBlock);
				textStack.Children.Add(ipBlock);

				if (!string.IsNullOrEmpty(node.Username))
				{
					TextBlock userBlock = new TextBlock()
					{
						Text = string.Format("👤 {0}{1}", node.Username, node.IsConnected ? " (net use: ✓)" : ""),
						FontSize = 9,
						Foreground = node.IsConnected ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(147, 197, 253))
					};
					textStack.Children.Add(userBlock);
				}

				if (node.CustomDirectories != null && node.CustomDirectories.Count > 0)
				{
					TextBlock dirsBlock = new TextBlock()
					{
						Text = string.Format("📁 Папок: {0}", node.CustomDirectories.Count),
						FontSize = 9,
						Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
						Margin = new Thickness(0, 1, 0, 0)
					};
					textStack.Children.Add(dirsBlock);
				}

				dock.Children.Add(textStack);

				itemBorder.Child = dock;
				_panelComputersContainer.Children.Add(itemBorder);
			}
		}

		// Создание плашки статуса компьютера с учетом цветовой темы
		private Border CreateStatusBadge(string status)
		{
			Border badge = new Border();
			badge.CornerRadius = new CornerRadius(4);
			badge.Padding = new Thickness(6, 2, 6, 2);
			badge.VerticalAlignment = VerticalAlignment.Center;

			TextBlock text = new TextBlock();
			text.Text = status;
			text.FontSize = 10;
			text.FontWeight = FontWeights.Bold;

			if (status == "Готово")
			{
				badge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(6, 78, 59)) : new SolidColorBrush(Color.FromRgb(220, 252, 231));
				text.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(22, 163, 74));
			}
			else if (status == "Сканируется")
			{
				badge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(219, 234, 254));
				text.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(96, 165, 250)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
			}
			else if (status == "Недоступен")
			{
				badge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(127, 29, 29)) : new SolidColorBrush(Color.FromRgb(254, 226, 226));
				text.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(248, 113, 113)) : new SolidColorBrush(Color.FromRgb(220, 38, 38));
			}
			else
			{
				badge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(243, 244, 246));
				text.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(156, 163, 175)) : new SolidColorBrush(Color.FromRgb(107, 114, 128));
			}

			badge.Child = text;
			return badge;
		}

		// Синхронизация списка компьютеров с объектом конфигурации и сохранение в файл
		private void SyncAndSaveComputersToConfig()
		{
			_currentConfig.NetworkComputers.Clear();
			if (_currentConfig.ComputerCredentials == null)
			{
				_currentConfig.ComputerCredentials = new List<NetworkComputerCredential>();
			}
			_currentConfig.ComputerCredentials.Clear();

			for (int i = 0; i < _networkNodes.Count; i++)
			{
				NetworkNodeItem node = _networkNodes[i];
				_currentConfig.NetworkComputers.Add(string.Format("{0} ({1})", node.Name, node.IpAddress));
				_currentConfig.ComputerCredentials.Add(new NetworkComputerCredential(node.IpAddress, node.Username, node.Password)
				{
					IsConnected = node.IsConnected,
					CustomDirectories = new List<string>(node.CustomDirectories)
				});
			}
			ConfigManager.SaveConfig(_currentConfig);
		}

		// Отрисовка списка директорий из конфигурации с возможностью контекстного удаления
		private void RenderDirectoriesList()
		{
			_panelDirectoriesContainer.Children.Clear();
			Brush pathTextColor = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(243, 244, 246)) : new SolidColorBrush(Color.FromRgb(31, 41, 55));

			for (int i = 0; i < _currentConfig.ScanDirectories.Count; i++)
			{
				string path = _currentConfig.ScanDirectories[i];
				DockPanel dirPanel = new DockPanel();
				dirPanel.Margin = new Thickness(0, 0, 0, 6);

				System.Windows.Controls.ContextMenu cmDir = new System.Windows.Controls.ContextMenu();
				System.Windows.Controls.MenuItem delDir = new System.Windows.Controls.MenuItem() { Header = "Удалить директорию" };
				delDir.Click += (s, e) =>
				{
					_currentConfig.ScanDirectories.Remove(path);
					ConfigManager.SaveConfig(_currentConfig);
					RenderDirectoriesList();
				};
				cmDir.Items.Add(delDir);
				dirPanel.ContextMenu = cmDir;

				TextBlock folderIcon = new TextBlock() { Text = "📁", FontSize = 13, Margin = new Thickness(0, 0, 6, 0), Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)) };
				DockPanel.SetDock(folderIcon, System.Windows.Controls.Dock.Left);
				dirPanel.Children.Add(folderIcon);

				TextBlock pathLabel = new TextBlock() { Text = path, FontSize = 11, Foreground = pathTextColor, TextTrimming = TextTrimming.CharacterEllipsis };
				dirPanel.Children.Add(pathLabel);

				_panelDirectoriesContainer.Children.Add(dirPanel);
			}
		}

		// Обновление отображаемых записей таблицы сертификатов с учетом поиска, фильтрации и активной сортировки
		private void RefreshCertificatesGrid()
		{
			_displayCertificates.Clear();

			for (int i = 0; i < _allCertificates.Count; i++)
			{
				CertificateRecord cert = _allCertificates[i];

				if (_filterOnlyWithLicense == true && cert.HasEmbeddedLicense == false)
				{
					continue;
				}

				if (!string.IsNullOrEmpty(_searchFilterQuery))
				{
					string query = _searchFilterQuery.ToLowerInvariant();
					bool matchSerial = (cert.SerialNumber ?? string.Empty).ToLowerInvariant().Contains(query);
					bool matchOwner = (cert.OwnerName ?? string.Empty).ToLowerInvariant().Contains(query);
					bool matchIssuer = (cert.Issuer ?? string.Empty).ToLowerInvariant().Contains(query);

					if (matchSerial == false && matchOwner == false && matchIssuer == false)
					{
						continue;
					}
				}

				_displayCertificates.Add(new CertificateDisplayItem(cert));
			}

			ApplySortingToDisplayCertificates();
			UpdateColumnHeaderSortIndicators();

			if (_gridCertificates.ItemsSource == null)
			{
				_gridCertificates.ItemsSource = _displayCertificates;
			}
			else
			{
				_gridCertificates.Items.Refresh();
			}

			_labelMetricTotalNumber.Text = _allCertificates.Count.ToString();
		}

		// Обработчик события клика по заголовку колонки для переключения сортировки (по возрастанию / по убыванию)
		private void GridCertificates_Sorting(object sender, DataGridSortingEventArgs e)
		{
			e.Handled = true;

			string clickedKey = e.Column.SortMemberPath;
			if (string.IsNullOrEmpty(clickedKey))
			{
				return;
			}

			if (_sortColumnKey == clickedKey)
			{
				_isSortAscending = !_isSortAscending;
			}
			else
			{
				_sortColumnKey = clickedKey;
				_isSortAscending = true;
			}

			ApplySortingToDisplayCertificates();
			UpdateColumnHeaderSortIndicators();

			_gridCertificates.Items.Refresh();
		}

		// Сортировка отфильтрованного списка сертификатов в соответствии с выбранной колонкой и направлением
		private void ApplySortingToDisplayCertificates()
		{
			if (string.IsNullOrEmpty(_sortColumnKey) || _displayCertificates == null || _displayCertificates.Count <= 1)
			{
				return;
			}

			if (_sortColumnKey == "SerialNumber")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? string.Compare(a.SerialNumber, b.SerialNumber, StringComparison.OrdinalIgnoreCase) 
					: string.Compare(b.SerialNumber, a.SerialNumber, StringComparison.OrdinalIgnoreCase));
			}
			else if (_sortColumnKey == "DateInserted")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? a.Record.DateInserted.CompareTo(b.Record.DateInserted) 
					: b.Record.DateInserted.CompareTo(a.Record.DateInserted));
			}
			else if (_sortColumnKey == "DateUpdated")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? a.Record.DateUpdated.CompareTo(b.Record.DateUpdated) 
					: b.Record.DateUpdated.CompareTo(a.Record.DateUpdated));
			}
			else if (_sortColumnKey == "OwnerName")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? string.Compare(a.OwnerName, b.OwnerName, StringComparison.CurrentCultureIgnoreCase) 
					: string.Compare(b.OwnerName, a.OwnerName, StringComparison.CurrentCultureIgnoreCase));
			}
			else if (_sortColumnKey == "Issuer")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? string.Compare(a.Issuer, b.Issuer, StringComparison.CurrentCultureIgnoreCase) 
					: string.Compare(b.Issuer, a.Issuer, StringComparison.CurrentCultureIgnoreCase));
			}
			else if (_sortColumnKey == "CryptoProVersion")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? string.Compare(a.CryptoProVersion, b.CryptoProVersion, StringComparison.OrdinalIgnoreCase) 
					: string.Compare(b.CryptoProVersion, a.CryptoProVersion, StringComparison.OrdinalIgnoreCase));
			}
			else if (_sortColumnKey == "HasEmbeddedLicense")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? a.HasEmbeddedLicense.CompareTo(b.HasEmbeddedLicense) 
					: b.HasEmbeddedLicense.CompareTo(a.HasEmbeddedLicense));
			}
		}

		// Обновление заголовков колонок таблицы с добавлением визуальной стрелочки направления сортировки (▲ / ▼)
		private void UpdateColumnHeaderSortIndicators()
		{
			if (_gridCertificates == null)
			{
				return;
			}

			string arrow = _isSortAscending ? "  ▲" : "  ▼";

			for (int i = 0; i < _gridCertificates.Columns.Count; i++)
			{
				DataGridColumn column = _gridCertificates.Columns[i];
				string baseTitle = GetBaseColumnTitle(column.SortMemberPath);

				if (!string.IsNullOrEmpty(_sortColumnKey) && column.SortMemberPath == _sortColumnKey)
				{
					column.Header = baseTitle + arrow;
					column.SortDirection = _isSortAscending 
						? System.ComponentModel.ListSortDirection.Ascending 
						: System.ComponentModel.ListSortDirection.Descending;
				}
				else
				{
					column.Header = baseTitle;
					column.SortDirection = null;
				}
			}
		}

		// Получение исходного базового наименования колонки без стрелок сортировки
		private string GetBaseColumnTitle(string sortMemberPath)
		{
			if (sortMemberPath == "SerialNumber") return "Серийный номер";
			if (sortMemberPath == "DateInserted") return "Дата внесения";
			if (sortMemberPath == "DateUpdated") return "Дата обновления";
			if (sortMemberPath == "OwnerName") return "Имя владельца";
			if (sortMemberPath == "Issuer") return "Удостоверяющий центр";
			if (sortMemberPath == "CryptoProVersion") return "Версия КриптоПро";
			if (sortMemberPath == "HasEmbeddedLicense") return "Лицензия встроена";
			return string.Empty;
		}

		// Переключение фильтра сертификатов со встроенной лицензией
		private void ButtonFilterLicense_Click(object sender, RoutedEventArgs e)
		{
			_filterOnlyWithLicense = !_filterOnlyWithLicense;

			if (_filterOnlyWithLicense == true)
			{
				_buttonFilterLicense.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
				_buttonFilterLicense.Foreground = Brushes.White;
				_buttonFilterLicense.FontWeight = FontWeights.Bold;
			}
			else
			{
				SolidColorBrush defaultBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(239, 246, 255));
				SolidColorBrush defaultText = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
				_buttonFilterLicense.Background = defaultBg;
				_buttonFilterLicense.Foreground = defaultText;
				_buttonFilterLicense.FontWeight = FontWeights.Regular;
			}

			RefreshCertificatesGrid();
		}

		// Диалоговое добавление нового компьютера в список сетевых узлов с опциональными учетными данными net use
		private void ButtonAddComputer_Click(object sender, RoutedEventArgs e)
		{
			HcWindow prompt = new HcWindow();
			prompt.Width = 380;
			prompt.Height = 340;
			prompt.Title = "Добавить компьютер в сеть";
			prompt.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			prompt.Owner = this;
			prompt.ResizeMode = ResizeMode.NoResize;

			StackPanel promptStack = new StackPanel() { Margin = new Thickness(16) };
			promptStack.Children.Add(new TextBlock() { Text = "Имя ПК:", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
			System.Windows.Controls.TextBox nameBox = new System.Windows.Controls.TextBox() { Text = "PC-NODE-01", Height = 28, Margin = new Thickness(0, 0, 0, 8), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
			promptStack.Children.Add(nameBox);

			promptStack.Children.Add(new TextBlock() { Text = "IP адрес:", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
			System.Windows.Controls.TextBox ipBox = new System.Windows.Controls.TextBox() { Text = "192.168.1.100", Height = 28, Margin = new Thickness(0, 0, 0, 8), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
			promptStack.Children.Add(ipBox);

			promptStack.Children.Add(new TextBlock() { Text = "Логин (пользователь для net use):", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
			System.Windows.Controls.TextBox userBox = new System.Windows.Controls.TextBox() { Text = "Admin", Height = 28, Margin = new Thickness(0, 0, 0, 8), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
			promptStack.Children.Add(userBox);

			promptStack.Children.Add(new TextBlock() { Text = "Пароль (для net use):", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
			System.Windows.Controls.TextBox passBox = new System.Windows.Controls.TextBox() { Text = "", Height = 28, Margin = new Thickness(0, 0, 0, 14), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
			promptStack.Children.Add(passBox);

			System.Windows.Controls.Button btnOk = new System.Windows.Controls.Button() { Content = "Добавить компьютер", Height = 32, FontWeight = FontWeights.Bold, Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Foreground = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
			btnOk.Click += (s, ev) =>
			{
				string name = nameBox.Text.Trim();
				string ip = ipBox.Text.Trim();
				string user = userBox.Text.Trim();
				string pass = passBox.Text;

				if (!string.IsNullOrEmpty(name))
				{
					_networkNodes.Add(new NetworkNodeItem(name, ip, "В очереди", user, pass));
					SyncAndSaveComputersToConfig();
					RenderComputersList();
					UpdateProgressCardSummary();
					AppendTerminalLog(string.Format("{0} - Добавлен новый сетевой узел: {1} ({2})", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), name, ip), Color.FromRgb(96, 165, 250));
				}
				prompt.Close();
			};
			promptStack.Children.Add(btnOk);

			prompt.Content = promptStack;
			prompt.ShowDialog();
		}

		// Диалоговое окно управления учетными записями компьютеров сети и подключения через net use
		private void ButtonManageCredentials_Click(object sender, RoutedEventArgs e)
		{
			HcWindow window = new HcWindow();
			window.Title = "Управление доступом к компьютерам сети (net use)";
			window.Width = 920;
			window.Height = 650;
			window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			window.Owner = this;
			window.ResizeMode = ResizeMode.NoResize;

			Brush winBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(21, 28, 44)) : new SolidColorBrush(Color.FromRgb(248, 250, 252));
			Brush cardBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) : Brushes.White;
			Brush cardBorder = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
			Brush textPrimary = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(15, 23, 42));
			Brush textSecondary = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(148, 163, 184)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
			Brush inputBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White;
			Brush inputBorder = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(71, 85, 105)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));

			window.Background = winBg;

			Grid mainLayout = new Grid() { Margin = new Thickness(16) };
			mainLayout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			mainLayout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			mainLayout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
			mainLayout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			mainLayout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			// 1. Заголовок диалога
			StackPanel headerStack = new StackPanel() { Margin = new Thickness(0, 0, 0, 10) };
			TextBlock title = new TextBlock()
			{
				Text = "🔑  Сетевые учетные записи и авторизация ПК (net use)",
				FontSize = 15,
				FontWeight = FontWeights.Bold,
				Foreground = textPrimary
			};
			TextBlock subtitle = new TextBlock()
			{
				Text = "Для доступа к реестру и скрытым дискам выполняется: net use \\\\IP\\IPC$ \"Пароль\" /user:\"Пользователь\"",
				FontSize = 11,
				Foreground = textSecondary,
				Margin = new Thickness(0, 4, 0, 0)
			};
			headerStack.Children.Add(title);
			headerStack.Children.Add(subtitle);
			Grid.SetRow(headerStack, 0);
			mainLayout.Children.Add(headerStack);

			// 2. Верхняя панель групповых действий
			DockPanel toolbar = new DockPanel() { Margin = new Thickness(0, 0, 0, 10) };
			System.Windows.Controls.Button btnConnectAll = new System.Windows.Controls.Button()
			{
				Content = "🔌  Подключить все ПК (net use)",
				Height = 28,
				Padding = new Thickness(12, 0, 12, 0),
				Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				Cursor = System.Windows.Input.Cursors.Hand
			};
			DockPanel.SetDock(btnConnectAll, System.Windows.Controls.Dock.Left);
			toolbar.Children.Add(btnConnectAll);

			System.Windows.Controls.Button btnDisconnectAll = new System.Windows.Controls.Button()
			{
				Content = "❌  Сбросить все подключения",
				Height = 28,
				Margin = new Thickness(8, 0, 0, 0),
				Padding = new Thickness(12, 0, 12, 0),
				Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				Cursor = System.Windows.Input.Cursors.Hand
			};
			DockPanel.SetDock(btnDisconnectAll, System.Windows.Controls.Dock.Left);
			toolbar.Children.Add(btnDisconnectAll);

			Grid.SetRow(toolbar, 1);
			mainLayout.Children.Add(toolbar);

			// 3. Прокручиваемый список компьютеров
			StackPanel itemsContainer = new StackPanel();
			System.Windows.Controls.ScrollViewer scrollViewer = new System.Windows.Controls.ScrollViewer()
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Margin = new Thickness(0, 0, 0, 10)
			};
			scrollViewer.Content = itemsContainer;
			Grid.SetRow(scrollViewer, 2);
			mainLayout.Children.Add(scrollViewer);

			Action renderCredentialRows = null;
			renderCredentialRows = () =>
			{
				itemsContainer.Children.Clear();
				if (_networkNodes.Count == 0)
				{
					TextBlock emptyNotice = new TextBlock()
					{
						Text = "Список компьютеров пуст. Добавьте сетевой компьютер через форму ниже.",
						FontSize = 12,
						Foreground = textSecondary,
						HorizontalAlignment = HorizontalAlignment.Center,
						Margin = new Thickness(0, 30, 0, 0)
					};
					itemsContainer.Children.Add(emptyNotice);
					return;
				}

				for (int i = 0; i < _networkNodes.Count; i++)
				{
					NetworkNodeItem node = _networkNodes[i];
					Border rowBorder = new Border()
					{
						Background = cardBg,
						BorderBrush = cardBorder,
						BorderThickness = new Thickness(1),
						CornerRadius = new CornerRadius(6),
						Margin = new Thickness(0, 0, 0, 8),
						Padding = new Thickness(10)
					};

					Grid rowGrid = new Grid();
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(170) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(160) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(160) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(120) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

					// Данные узла
					StackPanel pcInfo = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
					TextBlock pcName = new TextBlock() { Text = "💻 " + node.Name, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = textPrimary };
					TextBlock pcIp = new TextBlock() { Text = node.IpAddress, FontSize = 10, Foreground = textSecondary };
					pcInfo.Children.Add(pcName);
					pcInfo.Children.Add(pcIp);
					Grid.SetColumn(pcInfo, 0);
					rowGrid.Children.Add(pcInfo);

					// Поле ввода пользователя
					StackPanel userStack = new StackPanel() { Margin = new Thickness(4, 0, 8, 0) };
					userStack.Children.Add(new TextBlock() { Text = "Пользователь:", FontSize = 9, Foreground = textSecondary, Margin = new Thickness(0, 0, 0, 2) });
					System.Windows.Controls.TextBox userBox = new System.Windows.Controls.TextBox()
					{
						Text = node.Username,
						Height = 26,
						Background = inputBg,
						Foreground = textPrimary,
						BorderBrush = inputBorder,
						VerticalContentAlignment = VerticalAlignment.Center,
						Padding = new Thickness(4, 0, 4, 0)
					};
					userBox.TextChanged += (s, ev) => node.Username = userBox.Text.Trim();
					userStack.Children.Add(userBox);
					Grid.SetColumn(userStack, 1);
					rowGrid.Children.Add(userStack);

					// Поле ввода пароля
					StackPanel passStack = new StackPanel() { Margin = new Thickness(4, 0, 8, 0) };
					passStack.Children.Add(new TextBlock() { Text = "Пароль:", FontSize = 9, Foreground = textSecondary, Margin = new Thickness(0, 0, 0, 2) });
					System.Windows.Controls.TextBox passBox = new System.Windows.Controls.TextBox()
					{
						Text = node.Password,
						Height = 26,
						Background = inputBg,
						Foreground = textPrimary,
						BorderBrush = inputBorder,
						VerticalContentAlignment = VerticalAlignment.Center,
						Padding = new Thickness(4, 0, 4, 0)
					};
					passBox.TextChanged += (s, ev) => node.Password = passBox.Text;
					passStack.Children.Add(passBox);
					Grid.SetColumn(passStack, 2);
					rowGrid.Children.Add(passStack);

					// Индикатор подключения
					Border statusBadge = new Border()
					{
						CornerRadius = new CornerRadius(4),
						Padding = new Thickness(8, 4, 8, 4),
						HorizontalAlignment = HorizontalAlignment.Left,
						VerticalAlignment = VerticalAlignment.Center
					};
					TextBlock statusText = new TextBlock() { FontSize = 10, FontWeight = FontWeights.Bold };
					if (node.IsConnected == true)
					{
						statusBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(6, 78, 59)) : new SolidColorBrush(Color.FromRgb(220, 252, 231));
						statusText.Text = "✓ Подключен";
						statusText.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(22, 163, 74));
					}
					else
					{
						statusBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(243, 244, 246));
						statusText.Text = "— Не подключен";
						statusText.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(156, 163, 175)) : new SolidColorBrush(Color.FromRgb(107, 114, 128));
					}
					statusBadge.Child = statusText;
					Grid.SetColumn(statusBadge, 3);
					rowGrid.Children.Add(statusBadge);

					// Кнопки строки
					StackPanel actionStack = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
					System.Windows.Controls.Button btnConnectSingle = new System.Windows.Controls.Button()
					{
						Content = "🔌  Подключить",
						Height = 26,
						Margin = new Thickness(0, 0, 6, 0),
						Padding = new Thickness(8, 0, 8, 0),
						Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
						Foreground = Brushes.White,
						FontWeight = FontWeights.SemiBold,
						Cursor = System.Windows.Input.Cursors.Hand
					};
					btnConnectSingle.Click += async (s, ev) =>
					{
						btnConnectSingle.IsEnabled = false;
						btnConnectSingle.Content = "⏳...";
						string targetHost = node.IpAddress;
						string user = userBox.Text.Trim();
						string pass = passBox.Text;
						node.Username = user;
						node.Password = pass;

						string outMsg = string.Empty;
						bool ok = await Task.Run(() => CertificateScanner.EstablishNetworkSession(targetHost, user, pass, out outMsg));
						node.IsConnected = ok;
						btnConnectSingle.IsEnabled = true;
						btnConnectSingle.Content = "🔌  Подключить";

						if (ok == true)
						{
							statusBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(6, 78, 59)) : new SolidColorBrush(Color.FromRgb(220, 252, 231));
							statusText.Text = "✓ Подключен";
							statusText.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(22, 163, 74));
							AppendTerminalLog(string.Format("{0} - Успешное net use подключение к {1} ({2}) под пользователем {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress, user), Color.FromRgb(52, 211, 153));
						}
						else
						{
							statusBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(127, 29, 29)) : new SolidColorBrush(Color.FromRgb(254, 226, 226));
							statusText.Text = "❌ Ошибка";
							statusText.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(248, 113, 113)) : new SolidColorBrush(Color.FromRgb(220, 38, 38));
							AppendTerminalLog(string.Format("{0} - Ошибка net use к {1} ({2}): {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress, outMsg), Color.FromRgb(248, 113, 113));
							ShowAppMessage("Не удалось выполнить net use: " + outMsg, "Ошибка подключения", MessageBoxImage.Warning);
						}
						SyncAndSaveComputersToConfig();
						RenderComputersList();
					};
					actionStack.Children.Add(btnConnectSingle);

					System.Windows.Controls.Button btnDeleteRow = new System.Windows.Controls.Button()
					{
						Content = "✕",
						Height = 26,
						Width = 26,
						Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
						Foreground = Brushes.White,
						FontWeight = FontWeights.Bold,
						Cursor = System.Windows.Input.Cursors.Hand,
						ToolTip = "Удалить компьютер из списка"
					};
					btnDeleteRow.Click += (s, ev) =>
					{
						_networkNodes.Remove(node);
						SyncAndSaveComputersToConfig();
						renderCredentialRows();
						RenderComputersList();
						UpdateProgressCardSummary();
					};
					actionStack.Children.Add(btnDeleteRow);

					Grid.SetColumn(actionStack, 4);
					rowGrid.Children.Add(actionStack);

					StackPanel cardContent = new StackPanel();
					cardContent.Children.Add(rowGrid);

					// Секция индивидуальных директорий для поиска сертификатов на данном ПК
					Border dirsSectionBorder = new Border()
					{
						Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(24, 33, 47)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
						CornerRadius = new CornerRadius(4),
						Margin = new Thickness(0, 8, 0, 0),
						Padding = new Thickness(8, 6, 8, 6)
					};

					StackPanel dirsStack = new StackPanel();
					DockPanel dirsHeader = new DockPanel();
					TextBlock dirsTitle = new TextBlock()
					{
						Text = "📁  Директории для дополнительного поиска на этом ПК:",
						FontSize = 10,
						FontWeight = FontWeights.SemiBold,
						Foreground = textSecondary,
						VerticalAlignment = VerticalAlignment.Center
					};
					DockPanel.SetDock(dirsTitle, System.Windows.Controls.Dock.Left);
					dirsHeader.Children.Add(dirsTitle);

					System.Windows.Controls.Button btnAddDirToNode = new System.Windows.Controls.Button()
					{
						Content = "+  Добавить директорию...",
						Height = 22,
						Padding = new Thickness(8, 0, 8, 0),
						Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
						Foreground = Brushes.White,
						FontSize = 10,
						FontWeight = FontWeights.SemiBold,
						Cursor = System.Windows.Input.Cursors.Hand,
						HorizontalAlignment = HorizontalAlignment.Right
					};
					DockPanel.SetDock(btnAddDirToNode, System.Windows.Controls.Dock.Right);
					dirsHeader.Children.Add(btnAddDirToNode);
					dirsStack.Children.Add(dirsHeader);

					WrapPanel dirsListWrap = new WrapPanel() { Margin = new Thickness(0, 4, 0, 0) };
					Action renderNodeDirs = null;
					renderNodeDirs = () =>
					{
						dirsListWrap.Children.Clear();
						if (node.CustomDirectories.Count == 0)
						{
							dirsListWrap.Children.Add(new TextBlock()
							{
								Text = "Индивидуальные папки не заданы (поиск выполняется в реестре и хранилищах)",
								FontSize = 9,
								Foreground = textSecondary,
								FontStyle = FontStyles.Italic,
								Margin = new Thickness(0, 2, 0, 2)
							});
							return;
						}

						for (int dIdx = 0; dIdx < node.CustomDirectories.Count; dIdx++)
						{
							string dirPath = node.CustomDirectories[dIdx];
							Border dirBadge = new Border()
							{
								Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) : Brushes.White,
								BorderBrush = cardBorder,
								BorderThickness = new Thickness(1),
								CornerRadius = new CornerRadius(3),
								Padding = new Thickness(6, 2, 6, 2),
								Margin = new Thickness(0, 0, 6, 4)
							};
							DockPanel dirBadgeDock = new DockPanel();
							TextBlock dirText = new TextBlock()
							{
								Text = dirPath,
								FontSize = 10,
								Foreground = textPrimary,
								Margin = new Thickness(0, 0, 6, 0),
								VerticalAlignment = VerticalAlignment.Center
							};
							DockPanel.SetDock(dirText, System.Windows.Controls.Dock.Left);
							dirBadgeDock.Children.Add(dirText);

							System.Windows.Controls.Button btnRemoveDir = new System.Windows.Controls.Button()
							{
								Content = "✕",
								Width = 16,
								Height = 16,
								Padding = new Thickness(0),
								Background = Brushes.Transparent,
								Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
								BorderThickness = new Thickness(0),
								FontWeight = FontWeights.Bold,
								FontSize = 9,
								Cursor = System.Windows.Input.Cursors.Hand,
								ToolTip = "Удалить папку"
							};
							string capturedDir = dirPath;
							btnRemoveDir.Click += (s2, e2) =>
							{
								node.CustomDirectories.Remove(capturedDir);
								renderNodeDirs();
								SyncAndSaveComputersToConfig();
							};
							DockPanel.SetDock(btnRemoveDir, System.Windows.Controls.Dock.Right);
							dirBadgeDock.Children.Add(btnRemoveDir);

							dirBadge.Child = dirBadgeDock;
							dirsListWrap.Children.Add(dirBadge);
						}
					};

					btnAddDirToNode.Click += (s2, e2) =>
					{
						using (System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog())
						{
							fbd.Description = "Выберите директорию для поиска сертификатов на компьютере " + node.Name;
							fbd.ShowNewFolderButton = true;
							if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
							{
								if (!node.CustomDirectories.Contains(fbd.SelectedPath))
								{
									node.CustomDirectories.Add(fbd.SelectedPath);
									renderNodeDirs();
									SyncAndSaveComputersToConfig();
								}
							}
						}
					};

					renderNodeDirs();
					dirsStack.Children.Add(dirsListWrap);
					dirsSectionBorder.Child = dirsStack;
					cardContent.Children.Add(dirsSectionBorder);

					rowBorder.Child = cardContent;
					itemsContainer.Children.Add(rowBorder);
				}
			};

			renderCredentialRows();

			// Обработчик "Подключить все"
			btnConnectAll.Click += async (s, ev) =>
			{
				btnConnectAll.IsEnabled = false;
				btnConnectAll.Content = "⏳  Подключение...";
				AppendTerminalLog(string.Format("{0} - Запуск массового подключения net use ко всем сетевым узлам...", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(96, 165, 250));

				int successCount = 0;
				for (int i = 0; i < _networkNodes.Count; i++)
				{
					NetworkNodeItem node = _networkNodes[i];
					if (!string.IsNullOrWhiteSpace(node.Username))
					{
						string outMsg = string.Empty;
						bool ok = await Task.Run(() => CertificateScanner.EstablishNetworkSession(node.IpAddress, node.Username, node.Password, out outMsg));
						node.IsConnected = ok;
						if (ok == true)
						{
							successCount = successCount + 1;
						}
					}
				}

				btnConnectAll.IsEnabled = true;
				btnConnectAll.Content = "🔌  Подключить все ПК (net use)";
				SyncAndSaveComputersToConfig();
				renderCredentialRows();
				RenderComputersList();
				AppendTerminalLog(string.Format("{0} - Массовое подключение завершено. Успешно подключено: {1} из {2} ПК", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), successCount, _networkNodes.Count), Color.FromRgb(52, 211, 153));
				ShowAppMessage(string.Format("Сетевые сессии установлены для {0} из {1} ПК!", successCount, _networkNodes.Count), "Авторизация net use", MessageBoxImage.Information);
			};

			// Обработчик "Сбросить все"
			btnDisconnectAll.Click += async (s, ev) =>
			{
				btnDisconnectAll.IsEnabled = false;
				for (int i = 0; i < _networkNodes.Count; i++)
				{
					NetworkNodeItem node = _networkNodes[i];
					await Task.Run(() => CertificateScanner.DisconnectNetworkSession(node.IpAddress));
					node.IsConnected = false;
				}
				btnDisconnectAll.IsEnabled = true;
				SyncAndSaveComputersToConfig();
				renderCredentialRows();
				RenderComputersList();
				AppendTerminalLog(string.Format("{0} - Все сетевые сессии net use сброшены", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(156, 163, 175));
			};

			// 4. Форма быстрого добавления нового компьютера
			Border addCard = new Border()
			{
				Background = cardBg,
				BorderBrush = cardBorder,
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(10),
				Margin = new Thickness(0, 0, 0, 10)
			};

			Grid addGrid = new Grid();
			addGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			addGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			TextBlock addTitle = new TextBlock()
			{
				Text = "+ Добавить новый компьютер с логином и паролем:",
				FontWeight = FontWeights.Bold,
				FontSize = 11,
				Foreground = textPrimary,
				Margin = new Thickness(0, 0, 0, 6)
			};
			Grid.SetRow(addTitle, 0);
			addGrid.Children.Add(addTitle);

			Grid inputFieldsGrid = new Grid();
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(140) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(140) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(140) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(140) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			System.Windows.Controls.TextBox newNameBox = new System.Windows.Controls.TextBox() { Text = "12DEV", Height = 28, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
			System.Windows.Controls.TextBox newIpBox = new System.Windows.Controls.TextBox() { Text = "192.168.1.50", Height = 28, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
			System.Windows.Controls.TextBox newUserBox = new System.Windows.Controls.TextBox() { Text = "Admin", Height = 28, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
			System.Windows.Controls.TextBox newPassBox = new System.Windows.Controls.TextBox() { Text = "", Height = 28, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };

			Grid.SetColumn(newNameBox, 0);
			Grid.SetColumn(newIpBox, 1);
			Grid.SetColumn(newUserBox, 2);
			Grid.SetColumn(newPassBox, 3);
			inputFieldsGrid.Children.Add(newNameBox);
			inputFieldsGrid.Children.Add(newIpBox);
			inputFieldsGrid.Children.Add(newUserBox);
			inputFieldsGrid.Children.Add(newPassBox);

			System.Windows.Controls.Button btnAddNew = new System.Windows.Controls.Button()
			{
				Content = "+ Добавить",
				Height = 28,
				Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				Cursor = System.Windows.Input.Cursors.Hand
			};
			btnAddNew.Click += (s, ev) =>
			{
				string name = newNameBox.Text.Trim();
				string ip = newIpBox.Text.Trim();
				string user = newUserBox.Text.Trim();
				string pass = newPassBox.Text;

				if (!string.IsNullOrEmpty(name))
				{
					_networkNodes.Add(new NetworkNodeItem(name, ip, "В очереди", user, pass));
					SyncAndSaveComputersToConfig();
					renderCredentialRows();
					RenderComputersList();
					UpdateProgressCardSummary();
					AppendTerminalLog(string.Format("{0} - Добавлен новый сетевой ПК с учетными данными: {1} ({2}), пользователь: {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), name, ip, user), Color.FromRgb(96, 165, 250));
					newNameBox.Text = "PC-NODE-" + (_networkNodes.Count + 1);
					newPassBox.Text = string.Empty;
				}
			};
			Grid.SetColumn(btnAddNew, 4);
			inputFieldsGrid.Children.Add(btnAddNew);

			Grid.SetRow(inputFieldsGrid, 1);
			addGrid.Children.Add(inputFieldsGrid);

			addCard.Child = addGrid;
			Grid.SetRow(addCard, 3);
			mainLayout.Children.Add(addCard);

			// 5. Футер с кнопкой "Сохранить и закрыть"
			DockPanel footer = new DockPanel();
			System.Windows.Controls.Button btnClose = new System.Windows.Controls.Button()
			{
				Content = "Сохранить и закрыть",
				Height = 32,
				Width = 170,
				Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				Cursor = System.Windows.Input.Cursors.Hand,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			btnClose.Click += (s, ev) =>
			{
				SyncAndSaveComputersToConfig();
				RenderComputersList();
				AppendTerminalLog(string.Format("{0} - Учетные данные сетевых компьютеров сохранены в config.json", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(52, 211, 153));
				window.Close();
			};
			DockPanel.SetDock(btnClose, System.Windows.Controls.Dock.Right);
			footer.Children.Add(btnClose);

			Grid.SetRow(footer, 4);
			mainLayout.Children.Add(footer);

			window.Content = mainLayout;
			window.ShowDialog();
		}

		// Выбор и добавление директории сканирования через проводник Windows
		private void ButtonAddDirectory_Click(object sender, RoutedEventArgs e)
		{
			using (System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog())
			{
				folderDialog.Description = "Выберите каталог для поиска сертификатов открытого ключа";
				folderDialog.ShowNewFolderButton = true;

				if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					string selectedPath = folderDialog.SelectedPath;
					if (!_currentConfig.ScanDirectories.Contains(selectedPath))
					{
						_currentConfig.ScanDirectories.Add(selectedPath);
						ConfigManager.SaveConfig(_currentConfig);
						RenderDirectoriesList();
						AppendTerminalLog(string.Format("{0} - Добавлена новая директория сканирования: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), selectedPath), Color.FromRgb(96, 165, 250));
					}
				}
			}
		}

		// Сохранение настроек подключения к базе данных MSSQL
		private void ButtonSaveDbSettings_Click(object sender, RoutedEventArgs e)
		{
			string server = _textSqlServer.Text.Trim();
			string database = _textSqlDatabase.Text.Trim();
			string table = _textSqlTable.Text.Trim();

			_currentConfig.ConnectionString = string.Format("Server={0};Database={1};Integrated Security=True;", server, database);
			_currentConfig.TableName = string.IsNullOrWhiteSpace(table) ? "Certificates" : table;
			_currentConfig.AutoCreateTable = _checkAutoCreateTable.IsChecked == true;

			ConfigManager.SaveConfig(_currentConfig);
			AppendTerminalLog(string.Format("{0} - Параметры подключения к MSSQL сохранены в config.json", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(52, 211, 153));
			ShowAppMessage("Параметры базы данных успешно сохранены в config.json!", "Сохранение", MessageBoxImage.Information);
		}

		// Полная очистка таблицы в базе данных MSSQL и сброс текущего списка сертификатов
		private void ButtonClearDbTable_Click(object sender, RoutedEventArgs e)
		{
			string server = _textSqlServer.Text.Trim();
			string database = _textSqlDatabase.Text.Trim();
			string table = string.IsNullOrWhiteSpace(_textSqlTable.Text) ? "Certificates" : _textSqlTable.Text.Trim();
			string connString = string.Format("Server={0};Database={1};Integrated Security=True;", server, database);

			bool confirm = ShowAppConfirm(
				string.Format("Вы действительно хотите полностью очистить таблицу '{0}' в базе данных MSSQL?\n\nВсе существующие записи будут удалены для проведения чистого сканирования.", table),
				"Подтверждение очистки таблицы БД",
				"Да",
				"Нет",
				MessageBoxImage.Warning);

			if (confirm == false)
			{
				return;
			}

			bool isSuccess = DatabaseManager.ClearTable(connString, table);
			if (isSuccess == true)
			{
				_allCertificates.Clear();
				if (_currentConfig.Certificates != null)
				{
					_currentConfig.Certificates.Clear();
					ConfigManager.SaveConfig(_currentConfig);
				}

				_labelMetricInsertedNumber.Text = "0";
				_labelMetricUpdatedNumber.Text = "0";
				RefreshCertificatesGrid();

				AppendTerminalLog(string.Format("{0} - Таблица '{1}' в базе данных MSSQL успешно полностью очищена. Готово к чистому сканированию", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), table), Color.FromRgb(52, 211, 153));
				ShowAppMessage(string.Format("Таблица '{0}' успешно очищена!", table), "Очистка БД", MessageBoxImage.Information);
			}
			else
			{
				AppendTerminalLog(string.Format("{0} - Ошибка при очистке таблицы '{1}' в БД MSSQL", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), table), Color.FromRgb(248, 113, 113));
				ShowAppMessage("Не удалось очистить таблицу в базе данных. Проверьте подключение к серверу MSSQL.", "Ошибка очистки", MessageBoxImage.Error);
			}
		}

		// Запуск процедуры комплексного сканирования СКЗИ с обновлением интерфейса в реальном времени
		private async void ButtonStartScan_Click(object sender, RoutedEventArgs e)
		{
			if (_isScanningActive == true)
			{
				return;
			}

			_isScanningActive = true;
			_buttonStartScan.IsEnabled = false;
			_buttonStopScan.IsEnabled = true;
			_scanCts = new CancellationTokenSource();

			AppendTerminalLog(string.Format("{0} - Запущено комплексное сканирование СКЗИ", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Colors.White);

			try
			{
				int totalNodes = _networkNodes.Count;

				for (int i = 0; i < totalNodes; i++)
				{
					if (_scanCts.IsCancellationRequested)
					{
						break;
					}

					NetworkNodeItem node = _networkNodes[i];
					node.Status = "Сканируется";
					RenderComputersList();

					_labelProgressSummary.Text = string.Format("{0} из {1} ПК", i + 1, totalNodes);
					double prog = ((i + 1) * 100.0) / totalNodes;
					_progressBarCustom.Value = prog;
					_labelProgressPercent.Text = ((int)prog) + "%";
					_labelProgressCurrentNode.Text = "Сейчас: " + node.Name;

					AppendTerminalLog(string.Format("{0} - Начато сканирование ПК: {1} ({2})", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress), Color.FromRgb(156, 163, 175));

					await Task.Delay(400, _scanCts.Token);

					node.Status = "Готово";
					AppendTerminalLog(string.Format("{0} - ПК {1} успешно проверен. Сертификаты и СКЗИ обработаны", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name), Color.FromRgb(52, 211, 153));
					RenderComputersList();
				}

				List<CertificateRecord> scanned = await Task.Run(() => CertificateScanner.ScanAll(_currentConfig));

				int newlyAdded = 0;
				int updatedExisting = 0;

				for (int s = 0; s < scanned.Count; s++)
				{
					CertificateRecord foundCert = scanned[s];
					bool alreadyExists = false;

					for (int ex = 0; ex < _allCertificates.Count; ex++)
					{
						if (_allCertificates[ex].SerialNumber == foundCert.SerialNumber)
						{
							_allCertificates[ex].DateUpdated = DateTime.Now;
							_allCertificates[ex].OwnerName = foundCert.OwnerName;
							_allCertificates[ex].Issuer = foundCert.Issuer;
							_allCertificates[ex].CryptoProVersion = foundCert.CryptoProVersion;
							_allCertificates[ex].HasEmbeddedLicense = foundCert.HasEmbeddedLicense;
							alreadyExists = true;
							updatedExisting = updatedExisting + 1;
							break;
						}
					}

					if (!alreadyExists)
					{
						_allCertificates.Add(foundCert);
						newlyAdded = newlyAdded + 1;
					}
				}

				_currentConfig.Certificates = _allCertificates;
				ConfigManager.SaveConfig(_currentConfig);

				_labelMetricInsertedNumber.Text = newlyAdded.ToString();
				_labelMetricUpdatedNumber.Text = updatedExisting.ToString();

				RefreshCertificatesGrid();
				AppendTerminalLog(string.Format("{0} - Сканирование завершено. Всего сертификатов: {1}. Данные сохранены в config.json", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), _allCertificates.Count), Color.FromRgb(52, 211, 153));
			}
			catch (OperationCanceledException)
			{
				AppendTerminalLog(string.Format("{0} - Сканирование прервано пользователем", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(239, 68, 68));
			}
			catch (Exception ex)
			{
				AppendTerminalLog(string.Format("{0} - Ошибка сканирования: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), ex.Message), Color.FromRgb(239, 68, 68));
			}
			finally
			{
				_isScanningActive = false;
				_buttonStartScan.IsEnabled = true;
				_buttonStopScan.IsEnabled = false;
			}
		}

		// Прерывание текущего процесса сканирования сертификатов
		private void ButtonStopScan_Click(object sender, RoutedEventArgs e)
		{
			if (_scanCts != null)
			{
				_scanCts.Cancel();
			}
		}

		// Синхронизация и отправка записей сертификатов в СУБД Microsoft SQL Server
		private async void ButtonExportDb_Click(object sender, RoutedEventArgs e)
		{
			if (_allCertificates == null || _allCertificates.Count == 0)
			{
				ShowAppMessage("Список сертификатов пуст. Сначала выполните сканирование.", "Информация", MessageBoxImage.Information);
				return;
			}

			AppendTerminalLog(string.Format("{0} - Запущена отправка данных в СУБД Microsoft SQL Server...", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Colors.White);

			string connectionString = string.Format("Server={0};Database={1};Integrated Security=True;", _textSqlServer.Text.Trim(), _textSqlDatabase.Text.Trim());
			string tableName = _textSqlTable.Text.Trim();

			int savedCount = await Task.Run(() => DatabaseManager.SaveCertificates(_allCertificates, connectionString, tableName));

			_labelMetricInsertedNumber.Text = savedCount.ToString();
			AppendTerminalLog(string.Format("{0} - Синхронизация с БД завершена. Таблица '{1}': успешно обработано записей: {2}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), tableName, savedCount), Color.FromRgb(52, 211, 153));
		}


		// Добавление строки в цветной терминал журнала работы с автоматической прокруткой к концу
		public void AppendTerminalLog(string message, Color color)
		{
			if (!this.Dispatcher.CheckAccess())
			{
				this.Dispatcher.Invoke(new Action<string, Color>(AppendTerminalLog), message, color);
				return;
			}

			Paragraph p = new Paragraph();
			p.Margin = new Thickness(0, 0, 0, 2);
			Run run = new Run(message);
			run.Foreground = new SolidColorBrush(color);
			p.Inlines.Add(run);

			_textTerminalLog.Document.Blocks.Add(p);
			_textTerminalLog.ScrollToEnd();
		}

		// Отображение диалогового окна с подтверждением действия (Да/Нет) в едином стиле приложения
		private bool ShowAppConfirm(string message, string title, string okText = "Да", string cancelText = "Нет", MessageBoxImage image = MessageBoxImage.Warning)
		{
			if (!this.Dispatcher.CheckAccess())
			{
				return (bool)this.Dispatcher.Invoke(new Func<string, string, string, string, MessageBoxImage, bool>(ShowAppConfirm), message, title, okText, cancelText, image);
			}

			bool result = false;

			HcWindow dialog = new HcWindow();
			dialog.Title = title;
			dialog.Width = 490;
			dialog.SizeToContent = SizeToContent.Height;
			dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			dialog.Owner = this;
			dialog.ResizeMode = ResizeMode.NoResize;
			dialog.ShowTitle = true;

			bool isDark = _isDarkTheme;
			SolidColorBrush windowBg = isDark ? new SolidColorBrush(Color.FromRgb(17, 24, 39)) : new SolidColorBrush(Color.FromRgb(244, 246, 249));
			SolidColorBrush textPrimary = isDark ? new SolidColorBrush(Color.FromRgb(243, 244, 246)) : new SolidColorBrush(Color.FromRgb(17, 24, 39));

			dialog.Background = windowBg;
			dialog.NonClientAreaBackground = windowBg;
			dialog.NonClientAreaForeground = textPrimary;

			Grid rootGrid = new Grid() { Margin = new Thickness(22, 18, 22, 20) };
			rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			// Контент сообщения с иконкой
			Grid contentGrid = new Grid();
			contentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			contentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			TextBlock iconBlock = new TextBlock();
			iconBlock.FontSize = 32;
			iconBlock.VerticalAlignment = VerticalAlignment.Top;
			iconBlock.Margin = new Thickness(0, 0, 16, 0);

			if (image == MessageBoxImage.Warning)
			{
				iconBlock.Text = "⚠️";
			}
			else if (image == MessageBoxImage.Error)
			{
				iconBlock.Text = "❌";
			}
			else if (image == MessageBoxImage.Information)
			{
				iconBlock.Text = "ℹ️";
			}
			else
			{
				iconBlock.Text = "❓";
			}

			Grid.SetColumn(iconBlock, 0);
			contentGrid.Children.Add(iconBlock);

			TextBlock msgBlock = new TextBlock();
			msgBlock.Text = message;
			msgBlock.TextWrapping = TextWrapping.Wrap;
			msgBlock.FontSize = 13;
			msgBlock.LineHeight = 20;
			msgBlock.Foreground = textPrimary;
			msgBlock.VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(msgBlock, 1);
			contentGrid.Children.Add(msgBlock);

			Grid.SetRow(contentGrid, 0);
			rootGrid.Children.Add(contentGrid);

			// Панель кнопок Да / Нет
			StackPanel btnPanel = new StackPanel();
			btnPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
			btnPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			btnPanel.Margin = new Thickness(0, 22, 0, 0);

			Button btnConfirm = new Button();
			btnConfirm.Content = okText;
			btnConfirm.Height = 34;
			btnConfirm.MinWidth = 90;
			btnConfirm.Margin = new Thickness(0, 0, 10, 0);
			btnConfirm.FontWeight = FontWeights.SemiBold;
			btnConfirm.FontSize = 12;
			btnConfirm.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			btnConfirm.Foreground = Brushes.White;
			btnConfirm.BorderThickness = new Thickness(0);
			btnConfirm.Cursor = System.Windows.Input.Cursors.Hand;
			HandyControl.Controls.BorderElement.SetCornerRadius(btnConfirm, new CornerRadius(6));
			btnConfirm.Click += (s, e) =>
			{
				result = true;
				dialog.Close();
			};

			Button btnCancel = new Button();
			btnCancel.Content = cancelText;
			btnCancel.Height = 34;
			btnCancel.MinWidth = 90;
			btnCancel.FontSize = 12;
			btnCancel.Background = isDark ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
			btnCancel.Foreground = textPrimary;
			btnCancel.BorderThickness = new Thickness(0);
			btnCancel.Cursor = System.Windows.Input.Cursors.Hand;
			HandyControl.Controls.BorderElement.SetCornerRadius(btnCancel, new CornerRadius(6));
			btnCancel.Click += (s, e) =>
			{
				result = false;
				dialog.Close();
			};

			btnPanel.Children.Add(btnConfirm);
			btnPanel.Children.Add(btnCancel);

			Grid.SetRow(btnPanel, 1);
			rootGrid.Children.Add(btnPanel);

			dialog.Content = rootGrid;
			dialog.ShowDialog();

			return result;
		}

		// Отображение информационного диалогового окна с одной кнопкой 'ОК' в едином стиле приложения
		private void ShowAppMessage(string message, string title, MessageBoxImage image = MessageBoxImage.Information)
		{
			if (!this.Dispatcher.CheckAccess())
			{
				this.Dispatcher.Invoke(new Action<string, string, MessageBoxImage>(ShowAppMessage), message, title, image);
				return;
			}

			HcWindow dialog = new HcWindow();
			dialog.Title = title;
			dialog.Width = 460;
			dialog.SizeToContent = SizeToContent.Height;
			dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			dialog.Owner = this;
			dialog.ResizeMode = ResizeMode.NoResize;
			dialog.ShowTitle = true;

			bool isDark = _isDarkTheme;
			SolidColorBrush windowBg = isDark ? new SolidColorBrush(Color.FromRgb(17, 24, 39)) : new SolidColorBrush(Color.FromRgb(244, 246, 249));
			SolidColorBrush textPrimary = isDark ? new SolidColorBrush(Color.FromRgb(243, 244, 246)) : new SolidColorBrush(Color.FromRgb(17, 24, 39));

			dialog.Background = windowBg;
			dialog.NonClientAreaBackground = windowBg;
			dialog.NonClientAreaForeground = textPrimary;

			Grid rootGrid = new Grid() { Margin = new Thickness(22, 18, 22, 20) };
			rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			// Контент сообщения с иконкой
			Grid contentGrid = new Grid();
			contentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			contentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			TextBlock iconBlock = new TextBlock();
			iconBlock.FontSize = 32;
			iconBlock.VerticalAlignment = VerticalAlignment.Top;
			iconBlock.Margin = new Thickness(0, 0, 16, 0);

			if (image == MessageBoxImage.Warning)
			{
				iconBlock.Text = "⚠️";
			}
			else if (image == MessageBoxImage.Error)
			{
				iconBlock.Text = "❌";
			}
			else if (image == MessageBoxImage.Question)
			{
				iconBlock.Text = "❓";
			}
			else
			{
				iconBlock.Text = "ℹ️";
			}

			Grid.SetColumn(iconBlock, 0);
			contentGrid.Children.Add(iconBlock);

			TextBlock msgBlock = new TextBlock();
			msgBlock.Text = message;
			msgBlock.TextWrapping = TextWrapping.Wrap;
			msgBlock.FontSize = 13;
			msgBlock.LineHeight = 20;
			msgBlock.Foreground = textPrimary;
			msgBlock.VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(msgBlock, 1);
			contentGrid.Children.Add(msgBlock);

			Grid.SetRow(contentGrid, 0);
			rootGrid.Children.Add(contentGrid);

			// Панель с кнопкой ОК
			StackPanel btnPanel = new StackPanel();
			btnPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
			btnPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			btnPanel.Margin = new Thickness(0, 22, 0, 0);

			Button btnOk = new Button();
			btnOk.Content = "ОК";
			btnOk.Height = 34;
			btnOk.MinWidth = 90;
			btnOk.FontWeight = FontWeights.SemiBold;
			btnOk.FontSize = 12;
			btnOk.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			btnOk.Foreground = Brushes.White;
			btnOk.BorderThickness = new Thickness(0);
			btnOk.Cursor = System.Windows.Input.Cursors.Hand;
			HandyControl.Controls.BorderElement.SetCornerRadius(btnOk, new CornerRadius(6));
			btnOk.Click += (s, e) => dialog.Close();

			btnPanel.Children.Add(btnOk);

			Grid.SetRow(btnPanel, 1);
			rootGrid.Children.Add(btnPanel);

			dialog.Content = rootGrid;
			dialog.ShowDialog();
		}
	}
}
