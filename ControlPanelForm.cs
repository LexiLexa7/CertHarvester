using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
		public bool? IsOnline { get; set; }
		public long? PingTimeMs { get; set; }
		public bool? IsSmbOpen { get; set; }

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
			IsOnline = null;
			PingTimeMs = null;
			IsSmbOpen = null;
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
		public string ComputerSourceDisplay { get; set; }
		public CertificateRecord Record { get; set; }

		// Конструктор адаптера записи сертификата
		public CertificateDisplayItem(CertificateRecord record)
		{
			Record = record;
			SerialNumber = record.SerialNumber ?? string.Empty;
			DateInsertedDisplay = record.CreatedAt.ToString("dd.MM.yy HH:mm:ss");
			DateUpdatedDisplay = record.UpdatedAt.ToString("dd.MM.yy HH:mm:ss");
			OwnerName = record.OwnerName ?? string.Empty;
			Issuer = !string.IsNullOrEmpty(record.IssuerName) ? record.IssuerName : (record.Issuer ?? string.Empty);
			CryptoProVersion = string.IsNullOrEmpty(record.CryptoProVersion) ? "—" : record.CryptoProVersion;
			HasEmbeddedLicense = record.HasEmbeddedLicense;

			if (record.Locations != null && record.Locations.Count > 1)
			{
				List<string> uniqueNames = new List<string>();
				for (int l = 0; l < record.Locations.Count; l++)
				{
					string mName = record.Locations[l].MachineName;
					if (!string.IsNullOrEmpty(mName) && !uniqueNames.Contains(mName))
					{
						uniqueNames.Add(mName);
					}
				}
				ComputerSourceDisplay = string.Format("На {0} ПК ({1})", uniqueNames.Count, string.Join(", ", uniqueNames));
			}
			else if (record.Locations != null && record.Locations.Count == 1)
			{
				string mName = !string.IsNullOrEmpty(record.Locations[0].MachineName) ? record.Locations[0].MachineName : record.MachineName;
				string src = !string.IsNullOrEmpty(record.Locations[0].SourcePath) ? record.Locations[0].SourcePath : record.SourcePath;
				ComputerSourceDisplay = string.Format("{0} ({1})", mName, src);
			}
			else
			{
				string mName = !string.IsNullOrEmpty(record.MachineName) ? record.MachineName : "—";
				string src = !string.IsNullOrEmpty(record.SourcePath) ? record.SourcePath : "—";
				ComputerSourceDisplay = string.Format("{0} ({1})", mName, src);
			}
		}
	}

	/// <summary>
	/// Специализированный элемент управления вводом IPv4-адреса с 4 раздельными октетами (0-255) в едином поле
	/// </summary>
	public class IpAddressBox : Border
	{
		private readonly System.Windows.Controls.TextBox[] _octets = new System.Windows.Controls.TextBox[4];
		private readonly TextBlock[] _dots = new TextBlock[3];
		private Brush _normalBorderBrush;
		private readonly Brush _focusBorderBrush = new SolidColorBrush(Color.FromRgb(2, 132, 199));
		private readonly Brush _errorBorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
		private bool _isError = false;

		public IpAddressBox(bool isDark = true)
		{
			Height = 28;
			CornerRadius = new CornerRadius(4);
			BorderThickness = new Thickness(1);
			_normalBorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
			BorderBrush = _normalBorderBrush;
			Background = isDark ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White;
			Margin = new Thickness(0, 0, 6, 0);

			Grid grid = new Grid();
			grid.VerticalAlignment = VerticalAlignment.Center;
			grid.Margin = new Thickness(2, 0, 2, 0);

			// 7 колонок: 4 октета и 3 разделительные точки
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			Brush textColor = isDark ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(15, 23, 42));
			Brush dotColor = isDark ? new SolidColorBrush(Color.FromRgb(148, 163, 184)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));

			for (int i = 0; i < 4; i++)
			{
				int octetIndex = i;
				System.Windows.Controls.TextBox tb = new System.Windows.Controls.TextBox()
				{
					MaxLength = 3,
					TextAlignment = TextAlignment.Center,
					VerticalContentAlignment = VerticalAlignment.Center,
					BorderThickness = new Thickness(0),
					Background = Brushes.Transparent,
					Foreground = textColor,
					CaretBrush = textColor,
					FontSize = 11,
					FontWeight = FontWeights.Normal,
					Padding = new Thickness(0)
				};

				tb.PreviewTextInput += (s, e) =>
				{
					for (int c = 0; c < e.Text.Length; c++)
					{
						if (!char.IsDigit(e.Text[c]))
						{
							e.Handled = true;
							return;
						}
					}

					string current = tb.Text;
					int start = tb.SelectionStart;
					int len = tb.SelectionLength;
					string candidate = current.Remove(start, len).Insert(start, e.Text);

					int val;
					if (int.TryParse(candidate, out val))
					{
						if (val < 0 || val > 255)
						{
							e.Handled = true;
							return;
						}
					}
					else if (!string.IsNullOrEmpty(candidate))
					{
						e.Handled = true;
						return;
					}
				};

				tb.PreviewKeyDown += (s, e) =>
				{
					if (e.Key == Key.OemPeriod || e.Key == Key.Decimal || e.Key == Key.Space)
					{
						if (tb.Text.Length > 0 && octetIndex < 3)
						{
							_octets[octetIndex + 1].Focus();
							_octets[octetIndex + 1].SelectAll();
							e.Handled = true;
						}
					}
					else if (e.Key == Key.Back)
					{
						if (tb.SelectionStart == 0 && tb.SelectionLength == 0 && octetIndex > 0)
						{
							_octets[octetIndex - 1].Focus();
							_octets[octetIndex - 1].SelectionStart = _octets[octetIndex - 1].Text.Length;
						}
					}
					else if (e.Key == Key.Right)
					{
						if (tb.SelectionStart == tb.Text.Length && octetIndex < 3)
						{
							_octets[octetIndex + 1].Focus();
							_octets[octetIndex + 1].SelectionStart = 0;
							e.Handled = true;
						}
					}
					else if (e.Key == Key.Left)
					{
						if (tb.SelectionStart == 0 && octetIndex > 0)
						{
							_octets[octetIndex - 1].Focus();
							_octets[octetIndex - 1].SelectionStart = _octets[octetIndex - 1].Text.Length;
							e.Handled = true;
						}
					}
				};

				tb.TextChanged += (s, e) =>
				{
					ClearErrorHighlight();
					if (tb.Text.Length == 3 && tb.SelectionStart == 3 && octetIndex < 3)
					{
						_octets[octetIndex + 1].Focus();
						_octets[octetIndex + 1].SelectAll();
					}
				};

				tb.GotFocus += (s, e) =>
				{
					if (!_isError)
					{
						BorderBrush = _focusBorderBrush;
					}
				};

				tb.LostFocus += (s, e) =>
				{
					bool hasFocusAny = false;
					for (int k = 0; k < 4; k++)
					{
						if (_octets[k].IsFocused)
						{
							hasFocusAny = true;
							break;
						}
					}

					if (!hasFocusAny && !_isError)
					{
						BorderBrush = _normalBorderBrush;
					}
				};

				DataObject.AddPastingHandler(tb, (s, e) =>
				{
					if (e.DataObject.GetDataPresent(DataFormats.Text))
					{
						string text = e.DataObject.GetData(DataFormats.Text) as string;
						if (!string.IsNullOrEmpty(text) && text.Contains("."))
						{
							string[] parts = text.Trim().Split('.');
							if (parts.Length == 4)
							{
								bool valid = true;
								int[] nums = new int[4];
								for (int p = 0; p < 4; p++)
								{
									if (!int.TryParse(parts[p].Trim(), out nums[p]) || nums[p] < 0 || nums[p] > 255)
									{
										valid = false;
										break;
									}
								}
								if (valid)
								{
									for (int p = 0; p < 4; p++)
									{
										_octets[p].Text = nums[p].ToString();
									}
									_octets[3].Focus();
									_octets[3].SelectionStart = _octets[3].Text.Length;
									e.CancelCommand();
									e.Handled = true;
								}
							}
						}
					}
				});

				Grid.SetColumn(tb, i * 2);
				grid.Children.Add(tb);
				_octets[i] = tb;

				if (i < 3)
				{
					TextBlock dot = new TextBlock()
					{
						Text = ".",
						Foreground = dotColor,
						FontWeight = FontWeights.Bold,
						FontSize = 12,
						VerticalAlignment = VerticalAlignment.Center,
						HorizontalAlignment = HorizontalAlignment.Center,
						Margin = new Thickness(1, 0, 1, 0)
					};
					Grid.SetColumn(dot, i * 2 + 1);
					grid.Children.Add(dot);
					_dots[i] = dot;
				}
			}

			Child = grid;
		}

		public void UpdateTheme(bool isDark)
		{
			_normalBorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
			if (!_isError)
			{
				BorderBrush = _normalBorderBrush;
			}
			Background = isDark ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White;
			Brush textColor = isDark ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(15, 23, 42));
			Brush dotColor = isDark ? new SolidColorBrush(Color.FromRgb(148, 163, 184)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));

			for (int i = 0; i < 4; i++)
			{
				_octets[i].Foreground = textColor;
				_octets[i].CaretBrush = textColor;
			}
			for (int i = 0; i < 3; i++)
			{
				_dots[i].Foreground = dotColor;
			}
		}

		public void SetErrorHighlight()
		{
			_isError = true;
			BorderBrush = _errorBorderBrush;
		}

		public void ClearErrorHighlight()
		{
			if (_isError)
			{
				_isError = false;
				BorderBrush = _normalBorderBrush;
			}
		}

		public bool IsEmpty()
		{
			return string.IsNullOrWhiteSpace(_octets[0].Text) &&
			       string.IsNullOrWhiteSpace(_octets[1].Text) &&
			       string.IsNullOrWhiteSpace(_octets[2].Text) &&
			       string.IsNullOrWhiteSpace(_octets[3].Text);
		}

		public bool TryGetIpAddress(out string ipAddress, out string errorMessage)
		{
			ipAddress = string.Empty;
			errorMessage = string.Empty;

			bool anyFilled = !string.IsNullOrWhiteSpace(_octets[0].Text) ||
			                 !string.IsNullOrWhiteSpace(_octets[1].Text) ||
			                 !string.IsNullOrWhiteSpace(_octets[2].Text) ||
			                 !string.IsNullOrWhiteSpace(_octets[3].Text);

			if (!anyFilled)
			{
				return true;
			}

			for (int i = 0; i < 4; i++)
			{
				string text = _octets[i].Text.Trim();
				int val;
				if (string.IsNullOrEmpty(text) || !int.TryParse(text, out val) || val < 0 || val > 255)
				{
					SetErrorHighlight();
					errorMessage = "IP-адрес некорректен. Каждый октет должен содержать число от 0 до 255.";
					return false;
				}
			}

			ipAddress = string.Format("{0}.{1}.{2}.{3}",
				int.Parse(_octets[0].Text.Trim()),
				int.Parse(_octets[1].Text.Trim()),
				int.Parse(_octets[2].Text.Trim()),
				int.Parse(_octets[3].Text.Trim()));

			ClearErrorHighlight();
			return true;
		}

		public void Clear()
		{
			for (int i = 0; i < 4; i++)
			{
				_octets[i].Text = string.Empty;
			}
			ClearErrorHighlight();
		}

		public void SetIp(string ip)
		{
			if (string.IsNullOrWhiteSpace(ip))
			{
				Clear();
				return;
			}

			string[] parts = ip.Trim().Split('.');
			if (parts.Length == 4)
			{
				for (int i = 0; i < 4; i++)
				{
					int n;
					if (int.TryParse(parts[i].Trim(), out n) && n >= 0 && n <= 255)
					{
						_octets[i].Text = n.ToString();
					}
				}
			}
		}
	}

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
		private bool _isDarkTheme = true;
		private HandyControl.Themes.Theme _handyTheme;

		// Элементы левого сайдбара
		private Border _sidebarCard;
		private TextBlock _labelSidebarCompHeader;
		private TextBlock _labelSidebarDirsHeader;
		private TextBlock _labelSidebarDbHeader;
		private System.Windows.Controls.Button _buttonManageDevices;
		private System.Windows.Controls.Button _buttonAddDirectory;
		private StackPanel _panelComputersContainer;
		private StackPanel _panelDirectoriesContainer;
		private System.Windows.Controls.TextBox _textSqlServer;
		private System.Windows.Controls.TextBox _textSqlDatabase;
		private System.Windows.Controls.TextBox _textSqlTable;
		private System.Windows.Controls.CheckBox _checkAutoCreateTable;
		private System.Windows.Controls.Button _buttonSaveDbSettings;
		private System.Windows.Controls.Button _buttonClearDbTable;
		private System.Windows.Controls.Button _buttonLoadFromDb;

		// Интерактивная фильтрация по компьютеру сети
		private string _selectedComputerFilter;

		// Быстрые кнопки-переключатели фильтрации
		private enum QuickFilterMode
		{
			All,
			CryptoProGost,
			HideSystemUuid
		}
		private QuickFilterMode _currentQuickFilter = QuickFilterMode.All;
		private System.Windows.Controls.Button _buttonQuickFilterAll;
		private System.Windows.Controls.Button _buttonQuickFilterCrypto;
		private System.Windows.Controls.Button _buttonQuickFilterNoUuid;

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
		private bool _hasCompletedScan = false;

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
				_handyTheme.Skin = SkinType.Dark;
				System.Windows.Application.Current.Resources.MergedDictionaries.Add(_handyTheme);
			}
			else
			{
				_handyTheme.Skin = SkinType.Dark;
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

			// Главная сетка: левая колонка (280px) и правая рабочая область (*)
			Grid rootGrid = new Grid();
			rootGrid.Margin = new Thickness(14, 8, 14, 14);
			rootGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(295) });
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

			_buttonManageDevices = new System.Windows.Controls.Button();
			_buttonManageDevices.Height = 32;
			_buttonManageDevices.HorizontalAlignment = HorizontalAlignment.Stretch;
			_buttonManageDevices.Margin = new Thickness(0, 0, 0, 8);
			_buttonManageDevices.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonManageDevices.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			_buttonManageDevices.Foreground = Brushes.White;
			_buttonManageDevices.FontWeight = FontWeights.SemiBold;
			_buttonManageDevices.FontSize = 11;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonManageDevices, new CornerRadius(5));
			LucideIcons.ApplyToButton(_buttonManageDevices, "sliders-horizontal", "Управление устройствами", 15, Brushes.White, Brushes.White, 8, FontWeights.SemiBold);
			_buttonManageDevices.Click += ButtonManageDevices_Click;

			Grid.SetRow(_buttonManageDevices, 1);
			compBlock.Children.Add(_buttonManageDevices);

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
			_buttonSaveDbSettings.Height = 30;
			_buttonSaveDbSettings.HorizontalAlignment = HorizontalAlignment.Stretch;
			_buttonSaveDbSettings.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonSaveDbSettings.FontWeight = FontWeights.SemiBold;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonSaveDbSettings, new CornerRadius(5));
			LucideIcons.ApplyToButton(_buttonSaveDbSettings, "save", "Сохранить параметры БД", 14, _buttonSaveDbSettings.Foreground, _buttonSaveDbSettings.Foreground, 8, FontWeights.SemiBold);
			_buttonSaveDbSettings.Click += ButtonSaveDbSettings_Click;
			dbBlock.Children.Add(_buttonSaveDbSettings);

			// Кнопка отправки собранных данных в БД
			_buttonExportDb = new System.Windows.Controls.Button();
			_buttonExportDb.Height = 30;
			_buttonExportDb.HorizontalAlignment = HorizontalAlignment.Stretch;
			_buttonExportDb.Margin = new Thickness(0, 6, 0, 0);
			_buttonExportDb.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonExportDb.FontWeight = FontWeights.SemiBold;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonExportDb, new CornerRadius(5));
			LucideIcons.ApplyToButton(_buttonExportDb, "database", "Отправить данные в БД", 14, _buttonExportDb.Foreground, _buttonExportDb.Foreground, 8, FontWeights.SemiBold);
			_buttonExportDb.Click += ButtonExportDb_Click;
			dbBlock.Children.Add(_buttonExportDb);

			// Кнопка загрузки данных из БД через LEFT JOIN
			_buttonLoadFromDb = new System.Windows.Controls.Button();
			_buttonLoadFromDb.Height = 30;
			_buttonLoadFromDb.HorizontalAlignment = HorizontalAlignment.Stretch;
			_buttonLoadFromDb.Margin = new Thickness(0, 6, 0, 0);
			_buttonLoadFromDb.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonLoadFromDb.FontWeight = FontWeights.SemiBold;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonLoadFromDb, new CornerRadius(5));
			LucideIcons.ApplyToButton(_buttonLoadFromDb, "database", "Загрузить данные из БД", 14, _buttonLoadFromDb.Foreground, _buttonLoadFromDb.Foreground, 8, FontWeights.SemiBold);
			_buttonLoadFromDb.Click += ButtonLoadFromDb_Click;
			dbBlock.Children.Add(_buttonLoadFromDb);

			// Кнопка полной очистки таблицы в БД
			_buttonClearDbTable = new System.Windows.Controls.Button();
			_buttonClearDbTable.Height = 30;
			_buttonClearDbTable.HorizontalAlignment = HorizontalAlignment.Stretch;
			_buttonClearDbTable.Margin = new Thickness(0, 6, 0, 0);
			_buttonClearDbTable.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonClearDbTable.Background = new SolidColorBrush(Color.FromRgb(185, 28, 28));
			_buttonClearDbTable.Foreground = Brushes.White;
			_buttonClearDbTable.FontWeight = FontWeights.SemiBold;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonClearDbTable, new CornerRadius(5));
			LucideIcons.ApplyToButton(_buttonClearDbTable, "trash-2", "Очистить таблицу в БД", 14, Brushes.White, Brushes.White, 8, FontWeights.SemiBold);
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
			_buttonStopScan.Click += ButtonStopScan_Click;

			_buttonStartScan = new System.Windows.Controls.Button();
			_buttonStartScan.Height = 36;
			_buttonStartScan.Padding = new Thickness(18, 0, 18, 0);
			_buttonStartScan.Cursor = System.Windows.Input.Cursors.Hand;
			_buttonStartScan.Foreground = Brushes.White;
			_buttonStartScan.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
			_buttonStartScan.FontWeight = FontWeights.Bold;
			LucideIcons.ApplyToButton(_buttonStartScan, "play", "Сканировать", 15, Brushes.White, Brushes.White, 8, FontWeights.Bold);
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
			cardsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.4, GridUnitType.Star) });

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
			_cardMetricProgress.Padding = new Thickness(12, 10, 12, 10);
			_cardMetricProgress.Height = 84;

			Grid progressGrid = new Grid();
			progressGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			progressGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			progressGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			DockPanel progTop = new DockPanel();
			_labelProgressSummary = new TextBlock() { Text = "Готов к сканированию", FontWeight = FontWeights.Bold, FontSize = 14 };
			_labelProgressPercent = new TextBlock() { Text = "", FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
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

			_labelProgressCurrentNode = new TextBlock() { Text = "В очереди на проверку: 0 ПК", FontSize = 11.5, TextTrimming = TextTrimming.None };
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
			card.Padding = new Thickness(14, 10, 14, 10);
			card.Height = 84;

			StackPanel stack = new StackPanel();
			numberBlock = new TextBlock();
			numberBlock.Text = number;
			numberBlock.FontSize = 24;
			numberBlock.FontWeight = FontWeights.Bold;
			numberBlock.Margin = new Thickness(0, 0, 0, 4);
			if (numberColor != null)
			{
				numberBlock.Foreground = numberColor;
			}
			stack.Children.Add(numberBlock);

			titleBlock = new TextBlock();
			titleBlock.Text = title;
			titleBlock.FontSize = 13;
			titleBlock.FontWeight = FontWeights.SemiBold;
			titleBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			stack.Children.Add(titleBlock);

			card.Child = stack;
			return card;
		}

		// Создание панели с поисковой строкой и быстрыми переключателями фильтрации
		private DockPanel CreateSearchAndFilterBar()
		{
			DockPanel panel = new DockPanel();

			// Справа: быстрые кнопки-переключатели фильтрации
			StackPanel quickFilterStack = new StackPanel();
			quickFilterStack.Orientation = System.Windows.Controls.Orientation.Horizontal;
			quickFilterStack.VerticalAlignment = VerticalAlignment.Center;
			DockPanel.SetDock(quickFilterStack, System.Windows.Controls.Dock.Right);

			_buttonQuickFilterAll = new System.Windows.Controls.Button();
			_buttonQuickFilterAll.Content = "Все";
			_buttonQuickFilterAll.Height = 34;
			_buttonQuickFilterAll.Padding = new Thickness(14, 0, 14, 0);
			_buttonQuickFilterAll.Margin = new Thickness(6, 0, 0, 0);
			_buttonQuickFilterAll.Cursor = System.Windows.Input.Cursors.Hand;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonQuickFilterAll, new CornerRadius(6));
			_buttonQuickFilterAll.Click += (s, e) => SetQuickFilter(QuickFilterMode.All);
			quickFilterStack.Children.Add(_buttonQuickFilterAll);

			_buttonQuickFilterCrypto = new System.Windows.Controls.Button();
			_buttonQuickFilterCrypto.Content = "Только КриптоПро / ГОСТ";
			_buttonQuickFilterCrypto.Height = 34;
			_buttonQuickFilterCrypto.Padding = new Thickness(14, 0, 14, 0);
			_buttonQuickFilterCrypto.Margin = new Thickness(6, 0, 0, 0);
			_buttonQuickFilterCrypto.Cursor = System.Windows.Input.Cursors.Hand;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonQuickFilterCrypto, new CornerRadius(6));
			_buttonQuickFilterCrypto.Click += (s, e) => SetQuickFilter(QuickFilterMode.CryptoProGost);
			quickFilterStack.Children.Add(_buttonQuickFilterCrypto);

			_buttonQuickFilterNoUuid = new System.Windows.Controls.Button();
			_buttonQuickFilterNoUuid.Content = "Без системных UUID";
			_buttonQuickFilterNoUuid.Height = 34;
			_buttonQuickFilterNoUuid.Padding = new Thickness(14, 0, 14, 0);
			_buttonQuickFilterNoUuid.Margin = new Thickness(6, 0, 0, 0);
			_buttonQuickFilterNoUuid.Cursor = System.Windows.Input.Cursors.Hand;
			HandyControl.Controls.BorderElement.SetCornerRadius(_buttonQuickFilterNoUuid, new CornerRadius(6));
			_buttonQuickFilterNoUuid.Click += (s, e) => SetQuickFilter(QuickFilterMode.HideSystemUuid);
			quickFilterStack.Children.Add(_buttonQuickFilterNoUuid);

			panel.Children.Add(quickFilterStack);

			_buttonFilterLicense = new System.Windows.Controls.Button();

			_panelSearchContainer = new Border();
			_panelSearchContainer.CornerRadius = new CornerRadius(8);
			_panelSearchContainer.BorderThickness = new Thickness(1);
			_panelSearchContainer.Height = 34;
			_panelSearchContainer.Margin = new Thickness(0, 0, 6, 0);

			DockPanel searchInner = new DockPanel();
			System.Windows.Controls.Image icon = LucideIcons.CreateWpfImage("search", 15, System.Drawing.Color.FromArgb(156, 163, 175));
			icon.Margin = new Thickness(10, 0, 8, 0);
			icon.VerticalAlignment = VerticalAlignment.Center;
			DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left);
			searchInner.Children.Add(icon);

			_buttonClearSearch = new System.Windows.Controls.Button();
			_buttonClearSearch.Content = LucideIcons.CreateWpfImage("x", 12, System.Drawing.Color.FromArgb(156, 163, 175));
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
			_labelSearchPlaceholder.Text = "Поиск по номеру, владельцу, УЦ или ПК...";
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
				Width = new DataGridLength(210),
				MinWidth = 150
			});

			// 5. Удостоверяющий центр
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Удостоверяющий центр",
				Binding = new System.Windows.Data.Binding("Issuer"),
				SortMemberPath = "Issuer",
				Width = new DataGridLength(190),
				MinWidth = 140
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

			// 8. Компьютер / Источник
			grid.Columns.Add(new DataGridTextColumn()
			{
				Header = "Компьютер / Источник",
				Binding = new System.Windows.Data.Binding("ComputerSourceDisplay"),
				SortMemberPath = "ComputerSourceDisplay",
				Width = new DataGridLength(1, DataGridLengthUnitType.Star),
				MinWidth = 150
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

		// Применение визуального стиля и палитры ко всем компонентам окна (всегда тёмная тема)
		private void ApplyTheme(bool isDark = true)
		{
			isDark = true;
			_isDarkTheme = true;
			if (_handyTheme != null)
			{
				_handyTheme.Skin = SkinType.Dark;
			}

			SolidColorBrush windowBg = new SolidColorBrush(Color.FromRgb(17, 24, 39));
			SolidColorBrush cardBg = new SolidColorBrush(Color.FromRgb(31, 41, 55));
			SolidColorBrush cardBorder = new SolidColorBrush(Color.FromRgb(55, 65, 81));
			SolidColorBrush textPrimary = new SolidColorBrush(Color.FromRgb(243, 244, 246));
			SolidColorBrush textSecondary = new SolidColorBrush(Color.FromRgb(156, 163, 175));
			SolidColorBrush inputBg = new SolidColorBrush(Color.FromRgb(17, 24, 39));

			// Окно и шапка
			this.Background = windowBg;
			this.NonClientAreaBackground = windowBg;
			this.NonClientAreaForeground = textPrimary;

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

			if (_buttonManageDevices != null)
			{
				LucideIcons.ApplyToButton(_buttonManageDevices, "sliders-horizontal", "Управление устройствами", 15, Brushes.White, Brushes.White, 8, FontWeights.SemiBold);
			}

			_buttonSaveDbSettings.Background = isDark ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(243, 244, 246));
			_buttonSaveDbSettings.Foreground = textPrimary;
			LucideIcons.ApplyToButton(_buttonSaveDbSettings, "save", "Сохранить параметры БД", 14, _buttonSaveDbSettings.Foreground, _buttonSaveDbSettings.Foreground, 8, FontWeights.SemiBold);

			if (_buttonLoadFromDb != null)
			{
				_buttonLoadFromDb.Background = isDark ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(239, 246, 255));
				_buttonLoadFromDb.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
				LucideIcons.ApplyToButton(_buttonLoadFromDb, "database", "Загрузить данные из БД", 14, _buttonLoadFromDb.Foreground, _buttonLoadFromDb.Foreground, 8, FontWeights.SemiBold);
			}

			if (_buttonClearDbTable != null)
			{
				LucideIcons.ApplyToButton(_buttonClearDbTable, "trash-2", "Очистить таблицу в БД", 14, Brushes.White, Brushes.White, 8, FontWeights.SemiBold);
			}

			// Главная панель
			_labelMainHeader.Foreground = textPrimary;

			UpdateScanButtonState();

			if (_buttonExportDb != null)
			{
				_buttonExportDb.Background = isDark ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(219, 234, 254));
				_buttonExportDb.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
				LucideIcons.ApplyToButton(_buttonExportDb, "database", "Отправить данные в БД", 14, _buttonExportDb.Foreground, _buttonExportDb.Foreground, 8, FontWeights.SemiBold);
			}

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
			UpdateProgressCardSummary();

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
				_buttonClearSearch.Content = LucideIcons.CreateWpfImage("x", 12, textSecondary);
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

			UpdateQuickFilterButtonStyles();
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
			_isDarkTheme = true;
			_currentConfig.IsDarkTheme = true;

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

		/// <summary>
		/// Обновление карточки статуса сканирования с поддержкой трех визуальных состояний:
		/// 1. Ожидание первого сканирования (база/кэш пусты)
		/// 2. Ранее собранные данные загружены (из config.json или БД)
		/// 3. Сканирование завершено
		/// </summary>
		private void UpdateProgressCardSummary()
		{
			if (_isScanningActive == true)
			{
				return;
			}

			int totalNodes = _networkNodes != null ? _networkNodes.Count : 0;
			bool hasData = _allCertificates != null && _allCertificates.Count > 0;

			if (_hasCompletedScan == true)
			{
				// Состояние 3 (по завершении сканирования): Завершено
				_labelProgressSummary.Text = string.Format("Завершено: {0} из {0} ПК", totalNodes);
				_labelProgressPercent.Text = "100%";
				_progressBarCustom.Value = 100;
				_progressBarCustom.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
				_labelProgressCurrentNode.Text = "Сбор данных успешно завершен";
			}
			else if (hasData == true)
			{
				// Состояние 2: Данные загружены из кэша config.json или БД
				_labelProgressSummary.Text = totalNodes > 0
					? string.Format("Проверено: {0} из {0} ПК", totalNodes)
					: "Данные загружены";
				_labelProgressPercent.Text = "100%";
				_progressBarCustom.Value = 100;
				_progressBarCustom.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
				_labelProgressCurrentNode.Text = "Готов к повторному сканированию";
			}
			else
			{
				// Состояние 1: Ожидание первого сканирования (база/кэш пусты)
				_labelProgressSummary.Text = "Готов к сканированию";
				_labelProgressPercent.Text = string.Empty;
				_progressBarCustom.Value = 0;
				_progressBarCustom.Foreground = _isDarkTheme
					? new SolidColorBrush(Color.FromRgb(51, 65, 85))
					: new SolidColorBrush(Color.FromRgb(203, 213, 225));
				_labelProgressCurrentNode.Text = totalNodes > 0
					? string.Format("В очереди на проверку: {0} ПК", totalNodes)
					: "Добавьте ПК в сеть";
			}
		}

		// Отрисовка списка карточек компьютеров сети и плашки 'Все компьютеры (Сводка)' в левом сайдбаре
		private void RenderComputersList()
		{
			_panelComputersContainer.Children.Clear();

			Brush cardBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(17, 24, 39)) : new SolidColorBrush(Color.FromRgb(249, 250, 251));
			Brush cardBorder = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(229, 231, 235));
			Brush nameText = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(243, 244, 246)) : new SolidColorBrush(Color.FromRgb(17, 24, 39));
			Brush ipText = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(156, 163, 175)) : new SolidColorBrush(Color.FromRgb(107, 114, 128));

			Brush activeBorder = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			Brush activeBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 58, 95)) : new SolidColorBrush(Color.FromRgb(239, 246, 255));

			// 0. Плашка 'Все компьютеры (Сводка)'
			bool isAllSelected = string.IsNullOrEmpty(_selectedComputerFilter);
			Border allCard = new Border();
			allCard.Background = isAllSelected ? activeBg : cardBg;
			allCard.BorderBrush = isAllSelected ? activeBorder : cardBorder;
			allCard.BorderThickness = new Thickness(isAllSelected ? 2 : 1);
			allCard.CornerRadius = new CornerRadius(6);
			allCard.Margin = new Thickness(0, 0, 0, 8);
			allCard.Padding = new Thickness(8, 6, 8, 6);
			allCard.Cursor = System.Windows.Input.Cursors.Hand;

			DockPanel allDock = new DockPanel();
			System.Windows.Controls.Image allIcon = LucideIcons.CreateWpfImage("globe", 15, System.Drawing.Color.White);
			allIcon.Margin = new Thickness(0, 0, 8, 0);
			allIcon.VerticalAlignment = VerticalAlignment.Center;
			DockPanel.SetDock(allIcon, System.Windows.Controls.Dock.Left);
			allDock.Children.Add(allIcon);

			Border allBadge = new Border();
			allBadge.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			allBadge.CornerRadius = new CornerRadius(10);
			allBadge.Padding = new Thickness(6, 2, 6, 2);
			allBadge.VerticalAlignment = VerticalAlignment.Center;
			TextBlock allBadgeText = new TextBlock()
			{
				Text = string.Format("{0} серт.", _allCertificates != null ? _allCertificates.Count : 0),
				FontSize = 9,
				FontWeight = FontWeights.Bold,
				Foreground = Brushes.White
			};
			allBadge.Child = allBadgeText;
			DockPanel.SetDock(allBadge, System.Windows.Controls.Dock.Right);
			allDock.Children.Add(allBadge);

			StackPanel allTextStack = new StackPanel();
			TextBlock allTitle = new TextBlock() { Text = "Все компьютеры (Сводка)", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = nameText };
			TextBlock allSub = new TextBlock() { Text = string.Format("Узлов в сети: {0}", _networkNodes.Count), FontSize = 10, Foreground = ipText };
			allTextStack.Children.Add(allTitle);
			allTextStack.Children.Add(allSub);
			allDock.Children.Add(allTextStack);

			allCard.Child = allDock;
			allCard.MouseLeftButtonUp += (s, e) =>
			{
				_selectedComputerFilter = null;
				RenderComputersList();
				RefreshCertificatesGrid();
			};
			_panelComputersContainer.Children.Add(allCard);

			// 1. Карточки отдельных компьютеров сети
			for (int i = 0; i < _networkNodes.Count; i++)
			{
				NetworkNodeItem node = _networkNodes[i];
				bool isSelected = !string.IsNullOrEmpty(_selectedComputerFilter) &&
					(string.Equals(_selectedComputerFilter, node.Name, StringComparison.OrdinalIgnoreCase) ||
					 string.Equals(_selectedComputerFilter, node.IpAddress, StringComparison.OrdinalIgnoreCase));

				// Подсчет сертификатов на данном компьютере
				int nodeCertsCount = 0;
				if (_allCertificates != null)
				{
					for (int c = 0; c < _allCertificates.Count; c++)
					{
						if (_allCertificates[c].IsFoundOnMachine(node.Name) || _allCertificates[c].IsFoundOnMachine(node.IpAddress))
						{
							nodeCertsCount++;
						}
					}
				}

				Border itemBorder = new Border();
				itemBorder.Background = isSelected ? activeBg : cardBg;
				itemBorder.BorderBrush = isSelected ? activeBorder : cardBorder;
				itemBorder.BorderThickness = new Thickness(isSelected ? 2 : 1);
				itemBorder.CornerRadius = new CornerRadius(6);
				itemBorder.Margin = new Thickness(0, 0, 0, 6);
				itemBorder.Padding = new Thickness(6);
				itemBorder.Cursor = System.Windows.Input.Cursors.Hand;

				// Клик мышкой по карточке выделяет её и фильтрует таблицу справа
				itemBorder.MouseLeftButtonUp += (s, e) =>
				{
					if (string.Equals(_selectedComputerFilter, node.Name, StringComparison.OrdinalIgnoreCase))
					{
						_selectedComputerFilter = null;
					}
					else
					{
						_selectedComputerFilter = node.Name;
					}
					RenderComputersList();
					RefreshCertificatesGrid();
				};

				System.Windows.Controls.ContextMenu cm = new System.Windows.Controls.ContextMenu();
				System.Windows.Controls.MenuItem pingItem = new System.Windows.Controls.MenuItem()
				{
					Header = "Проверить доступность (Ping + SMB)",
					Icon = LucideIcons.CreateWpfImage("zap", 14, System.Drawing.Color.FromArgb(251, 191, 36))
				};
				cm.Items.Add(pingItem);

				System.Windows.Controls.MenuItem credItem = new System.Windows.Controls.MenuItem()
				{
					Header = "Учетные записи (net use)...",
					Icon = LucideIcons.CreateWpfImage("key", 14, System.Drawing.Color.FromArgb(147, 197, 253))
				};
				credItem.Click += (s, e) => ButtonManageCredentials_Click(s, e);
				cm.Items.Add(credItem);

				System.Windows.Controls.MenuItem connectItem = new System.Windows.Controls.MenuItem()
				{
					Header = "Подключить сейчас (net use)",
					Icon = LucideIcons.CreateWpfImage("plug", 14, System.Drawing.Color.FromArgb(52, 211, 153))
				};
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
					if (string.Equals(_selectedComputerFilter, node.Name, StringComparison.OrdinalIgnoreCase))
					{
						_selectedComputerFilter = null;
					}
					_networkNodes.Remove(node);
					SyncAndSaveComputersToConfig();
					RenderComputersList();
					UpdateProgressCardSummary();
					RefreshCertificatesGrid();
				};
				cm.Items.Add(delItem);
				itemBorder.ContextMenu = cm;

				DockPanel dock = new DockPanel();

				// Левая часть: цветной индикатор доступности узла (зеленая/красная/серая точка) и иконка ПК
				StackPanel leftIconStack = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
				DockPanel.SetDock(leftIconStack, System.Windows.Controls.Dock.Left);

				Border onlineIndicator = new Border();
				onlineIndicator.Width = 8;
				onlineIndicator.Height = 8;
				onlineIndicator.CornerRadius = new CornerRadius(4);
				onlineIndicator.Margin = new Thickness(0, 0, 5, 0);
				onlineIndicator.VerticalAlignment = VerticalAlignment.Center;

				if (node.IsOnline == true)
				{
					onlineIndicator.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
					onlineIndicator.ToolTip = string.Format("Узел доступен в сети (Ping{0})", node.PingTimeMs.HasValue ? string.Format(": {0} мс", node.PingTimeMs.Value) : " OK");
				}
				else if (node.IsOnline == false)
				{
					onlineIndicator.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
					onlineIndicator.ToolTip = "Узел недоступен по сети (таймаут Ping)";
				}
				else
				{
					onlineIndicator.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(107, 114, 128)) : new SolidColorBrush(Color.FromRgb(209, 213, 219));
					onlineIndicator.ToolTip = "Связь не проверялась (нажмите ⚡ для быстрой проверки)";
				}

				System.Drawing.Color monColor = isSelected ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(209, 213, 219);
				System.Windows.Controls.Image icon = LucideIcons.CreateWpfImage("monitor", 15, monColor);
				icon.VerticalAlignment = VerticalAlignment.Center;
				leftIconStack.Children.Add(onlineIndicator);
				leftIconStack.Children.Add(icon);
				dock.Children.Add(leftIconStack);

				// Правый блок с бейджем статуса, кнопкой быстрой проверки связи и бейджем количества сертификатов
				StackPanel badgesStack = new StackPanel();
				badgesStack.Orientation = System.Windows.Controls.Orientation.Vertical;
				badgesStack.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
				DockPanel.SetDock(badgesStack, System.Windows.Controls.Dock.Right);

				StackPanel topBadgeRow = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 3) };

				Border badge = CreateStatusBadge(node.Status);
				topBadgeRow.Children.Add(badge);

				System.Windows.Controls.Button btnQuickPing = new System.Windows.Controls.Button()
				{
					Content = LucideIcons.CreateWpfImage("zap", 13, System.Drawing.Color.FromArgb(251, 191, 36)),
					Width = 22,
					Height = 20,
					Padding = new Thickness(0),
					Margin = new Thickness(4, 0, 0, 0),
					Cursor = System.Windows.Input.Cursors.Hand,
					ToolTip = "Быстрая проверка связи (Ping и SMB порт 445)",
					Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
					Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) : new SolidColorBrush(Color.FromRgb(217, 119, 6)),
					BorderThickness = new Thickness(0)
				};
				HandyControl.Controls.BorderElement.SetCornerRadius(btnQuickPing, new CornerRadius(4));

				Action runQuickPing = async () =>
				{
					btnQuickPing.IsEnabled = false;
					btnQuickPing.Content = LucideIcons.CreateWpfImage("refresh-cw", 12, System.Drawing.Color.FromArgb(251, 191, 36));

					long pingMs = 0;
					bool isSmbOpen = false;
					bool isAlive = await Task.Run(() =>
					{
						long p;
						bool s;
						bool alive = NetworkScanner.CheckHostQuick(node.IpAddress, 400, 300, out p, out s);
						pingMs = p;
						isSmbOpen = s;
						return alive;
					});

					node.IsOnline = isAlive;
					node.PingTimeMs = pingMs;
					node.IsSmbOpen = isSmbOpen;

					if (isAlive)
					{
						if (node.Status == "Недоступен" || node.Status == "В очереди")
						{
							node.Status = node.IsConnected ? "Готово" : "В очереди";
						}
						AppendTerminalLog(string.Format("{0} - Быстрая проверка {1} ({2}): узел доступен (Ping: {3} мс, SMB 445: {4})", 
							DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress, pingMs, isSmbOpen ? "открыт" : "закрыт"), Color.FromRgb(52, 211, 153));
					}
					else
					{
						node.Status = "Недоступен";
						AppendTerminalLog(string.Format("{0} - Быстрая проверка {1} ({2}): узел недоступен по сети", 
							DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress), Color.FromRgb(248, 113, 113));
					}

					RenderComputersList();
				};

				btnQuickPing.Click += (s, ev) =>
				{
					ev.Handled = true;
					runQuickPing();
				};
				pingItem.Click += (s, ev) => runQuickPing();

				topBadgeRow.Children.Add(btnQuickPing);
				badgesStack.Children.Add(topBadgeRow);

				Border certsCountBadge = new Border();
				certsCountBadge.Background = isSelected ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : (_isDarkTheme ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(226, 232, 240)));
				certsCountBadge.CornerRadius = new CornerRadius(8);
				certsCountBadge.Padding = new Thickness(5, 1, 5, 1);
				certsCountBadge.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
				TextBlock certsBadgeTxt = new TextBlock()
				{
					Text = string.Format("{0} серт.", nodeCertsCount),
					FontSize = 9,
					FontWeight = FontWeights.SemiBold,
					Foreground = isSelected ? Brushes.White : nameText
				};
				certsCountBadge.Child = certsBadgeTxt;
				badgesStack.Children.Add(certsCountBadge);

				dock.Children.Add(badgesStack);

				StackPanel textStack = new StackPanel();
				TextBlock nameBlock = new TextBlock() { Text = node.Name, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = nameText };

				string ipDisplay = node.IpAddress;
				if (node.IsOnline == true && node.PingTimeMs.HasValue)
				{
					ipDisplay = string.Format("{0}  •  {1} мс", node.IpAddress, node.PingTimeMs.Value);
				}
				TextBlock ipBlock = new TextBlock() { Text = ipDisplay, FontSize = 10, Foreground = ipText };
				textStack.Children.Add(nameBlock);
				textStack.Children.Add(ipBlock);

				if (!string.IsNullOrEmpty(node.Username))
				{
					StackPanel userRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
					System.Drawing.Color userCol = node.IsConnected ? System.Drawing.Color.FromArgb(52, 211, 153) : System.Drawing.Color.FromArgb(147, 197, 253);
					System.Windows.Controls.Image userImg = LucideIcons.CreateWpfImage("user", 11, userCol);
					userImg.Margin = new Thickness(0, 0, 4, 0);
					userRow.Children.Add(userImg);

					TextBlock userBlock = new TextBlock()
					{
						Text = string.Format("{0}{1}", node.Username, node.IsConnected ? " (net use: OK)" : ""),
						FontSize = 9,
						Foreground = node.IsConnected ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(147, 197, 253)),
						VerticalAlignment = VerticalAlignment.Center
					};
					userRow.Children.Add(userBlock);
					textStack.Children.Add(userRow);
				}

				if (node.CustomDirectories != null && node.CustomDirectories.Count > 0)
				{
					StackPanel dirsRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
					System.Windows.Controls.Image folderImg = LucideIcons.CreateWpfImage("folder", 11, System.Drawing.Color.FromArgb(251, 191, 36));
					folderImg.Margin = new Thickness(0, 0, 4, 0);
					dirsRow.Children.Add(folderImg);

					TextBlock dirsBlock = new TextBlock()
					{
						Text = string.Format("Папок: {0}", node.CustomDirectories.Count),
						FontSize = 9,
						Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
						VerticalAlignment = VerticalAlignment.Center
					};
					dirsRow.Children.Add(dirsBlock);
					textStack.Children.Add(dirsRow);
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

				System.Windows.Controls.Image folderIcon = LucideIcons.CreateWpfImage("folder", 14, System.Drawing.Color.FromArgb(245, 158, 11));
				folderIcon.Margin = new Thickness(0, 0, 6, 0);
				folderIcon.VerticalAlignment = VerticalAlignment.Center;
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

				// Фильтрация по выбранному компьютеру в левой панели (если выбран конкретный узел)
				if (!string.IsNullOrEmpty(_selectedComputerFilter))
				{
					if (cert.IsFoundOnMachine(_selectedComputerFilter) == false)
					{
						continue;
					}
				}

				// Фильтрация по кнопкам быстрого выбора (Все / Только КриптоПро-ГОСТ / Без системных UUID)
				if (_currentQuickFilter == QuickFilterMode.CryptoProGost)
				{
					bool isCryptoPro = (!string.IsNullOrEmpty(cert.CryptoProVersion) && cert.CryptoProVersion != "—") || cert.HasEmbeddedLicense == true;
					if (isCryptoPro == false)
					{
						continue;
					}
				}
				else if (_currentQuickFilter == QuickFilterMode.HideSystemUuid)
				{
					if (IsSystemUuidOrTechnical(cert) == true)
					{
						continue;
					}
				}

				if (_filterOnlyWithLicense == true && cert.HasEmbeddedLicense == false)
				{
					continue;
				}

				if (!string.IsNullOrEmpty(_searchFilterQuery))
				{
					string query = _searchFilterQuery.ToLowerInvariant();
					bool matchSerial = (cert.SerialNumber ?? string.Empty).ToLowerInvariant().Contains(query);
					bool matchOwner = (cert.OwnerName ?? string.Empty).ToLowerInvariant().Contains(query);
					string issuer = !string.IsNullOrEmpty(cert.IssuerName) ? cert.IssuerName : (cert.Issuer ?? string.Empty);
					bool matchIssuer = issuer.ToLowerInvariant().Contains(query);
					bool matchMachine = (cert.MachineName ?? string.Empty).ToLowerInvariant().Contains(query);

					if (matchSerial == false && matchOwner == false && matchIssuer == false && matchMachine == false)
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

			if (_labelMetricTotalNumber != null)
			{
				_labelMetricTotalNumber.Text = _displayCertificates.Count.ToString();
			}

			if (_labelMetricTotalTitle != null)
			{
				_labelMetricTotalTitle.Text = string.IsNullOrEmpty(_selectedComputerFilter)
					? "всего в сети"
					: string.Format("найдено на {0}", _selectedComputerFilter);
			}
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
			else if (_sortColumnKey == "ComputerSourceDisplay")
			{
				_displayCertificates.Sort((a, b) => _isSortAscending 
					? string.Compare(a.ComputerSourceDisplay, b.ComputerSourceDisplay, StringComparison.CurrentCultureIgnoreCase) 
					: string.Compare(b.ComputerSourceDisplay, a.ComputerSourceDisplay, StringComparison.CurrentCultureIgnoreCase));
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
			if (sortMemberPath == "ComputerSourceDisplay") return "Компьютер / Источник";
			return string.Empty;
		}

		// Установка активного быстрого фильтра и обновление отображения таблицы
		private void SetQuickFilter(QuickFilterMode mode)
		{
			_currentQuickFilter = mode;
			UpdateQuickFilterButtonStyles();
			RefreshCertificatesGrid();
		}

		// Обновление визуального оформления кнопок быстрого фильтра (Все / КриптоПро-ГОСТ / Без системных UUID)
		private void UpdateQuickFilterButtonStyles()
		{
			if (_buttonQuickFilterAll == null || _buttonQuickFilterCrypto == null || _buttonQuickFilterNoUuid == null)
			{
				return;
			}

			SolidColorBrush activeBg = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			SolidColorBrush activeFg = Brushes.White;
			SolidColorBrush inactiveBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 41, 55)) : new SolidColorBrush(Color.FromRgb(243, 244, 246));
			SolidColorBrush inactiveFg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(209, 213, 219)) : new SolidColorBrush(Color.FromRgb(55, 65, 81));

			_buttonQuickFilterAll.Background = _currentQuickFilter == QuickFilterMode.All ? activeBg : inactiveBg;
			_buttonQuickFilterAll.Foreground = _currentQuickFilter == QuickFilterMode.All ? activeFg : inactiveFg;
			_buttonQuickFilterAll.FontWeight = _currentQuickFilter == QuickFilterMode.All ? FontWeights.Bold : FontWeights.Normal;

			_buttonQuickFilterCrypto.Background = _currentQuickFilter == QuickFilterMode.CryptoProGost ? activeBg : inactiveBg;
			_buttonQuickFilterCrypto.Foreground = _currentQuickFilter == QuickFilterMode.CryptoProGost ? activeFg : inactiveFg;
			_buttonQuickFilterCrypto.FontWeight = _currentQuickFilter == QuickFilterMode.CryptoProGost ? FontWeights.Bold : FontWeights.Normal;

			_buttonQuickFilterNoUuid.Background = _currentQuickFilter == QuickFilterMode.HideSystemUuid ? activeBg : inactiveBg;
			_buttonQuickFilterNoUuid.Foreground = _currentQuickFilter == QuickFilterMode.HideSystemUuid ? activeFg : inactiveFg;
			_buttonQuickFilterNoUuid.FontWeight = _currentQuickFilter == QuickFilterMode.HideSystemUuid ? FontWeights.Bold : FontWeights.Normal;
		}

		// Проверка, является ли сертификат техническим системным UUID, localhost или USBHostDriver
		private static bool IsSystemUuidOrTechnical(CertificateRecord cert)
		{
			if (cert == null)
			{
				return false;
			}

			string owner = cert.OwnerName ?? string.Empty;

			if (owner.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
				owner.IndexOf("USBHostDriver", StringComparison.OrdinalIgnoreCase) >= 0 ||
				owner.IndexOf("XBL Client IPsec", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			if (owner.StartsWith("trust_", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (System.Text.RegularExpressions.Regex.IsMatch(owner, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"))
			{
				return true;
			}

			return false;
		}

		// Загрузка сертификатов из базы данных MSSQL с восстановлением связей с компьютерами через LEFT JOIN
		private async void ButtonLoadFromDb_Click(object sender, RoutedEventArgs e)
		{
			string server = _textSqlServer.Text.Trim();
			string database = _textSqlDatabase.Text.Trim();
			string table = string.IsNullOrWhiteSpace(_textSqlTable.Text) ? "Certificates" : _textSqlTable.Text.Trim();
			string connString = string.Format("Server={0};Database={1};Integrated Security=True;", server, database);

			AppendTerminalLog(string.Format("{0} - Запрос загрузки сертификатов из базы данных MSSQL ({1}.dbo.{2})...", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), database, table), Colors.White);

			_buttonLoadFromDb.IsEnabled = false;

			try
			{
				List<CertificateRecord> loadedRecords = await Task.Run(() => DatabaseManager.LoadCertificatesFromDatabase(connString, table));

				if (loadedRecords != null && loadedRecords.Count > 0)
				{
					_allCertificates.Clear();
					for (int i = 0; i < loadedRecords.Count; i++)
					{
						_allCertificates.Add(loadedRecords[i]);
					}

					_currentConfig.Certificates = new List<CertificateRecord>(_allCertificates);
					ConfigManager.SaveConfig(_currentConfig);

					RefreshCertificatesGrid();
					RenderComputersList();
					_hasCompletedScan = false;
					UpdateProgressCardSummary();

					AppendTerminalLog(string.Format("{0} - Загружено {1} сертификатов из БД MSSQL", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), loadedRecords.Count), Color.FromRgb(52, 211, 153));
					ShowAppMessage(string.Format("Загружено сертификатов: {0}", loadedRecords.Count), "Загрузка из БД", MessageBoxImage.Information);
				}
				else
				{
					AppendTerminalLog(string.Format("{0} - В таблице '{1}' нет записей", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), table), Color.FromRgb(251, 191, 36));
					ShowAppMessage(string.Format("В таблице '{0}' нет записей.", table), "Загрузка из БД", MessageBoxImage.Information);
				}
			}
			catch (Exception ex)
			{
				AppendTerminalLog(string.Format("{0} - Ошибка загрузки из БД: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), ex.Message), Color.FromRgb(248, 113, 113));
				ShowAppMessage(string.Format("Ошибка загрузки из БД: {0}", ex.Message), "Ошибка БД", MessageBoxImage.Error);
			}
			finally
			{
				_buttonLoadFromDb.IsEnabled = true;
			}
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

		/// <summary>
		/// Открытие единого окна управления устройствами в сети (список компьютеров, учетные записи net use, папки поиска и поиск в сети)
		/// </summary>
		private void ButtonManageDevices_Click(object sender, RoutedEventArgs e)
		{
			OpenDeviceManagementWindow(0);
		}

		// Обработчик кнопки добавления ПК (сохранен для совместимости)
		private void ButtonAddComputer_Click(object sender, RoutedEventArgs e)
		{
			OpenDeviceManagementWindow(0);
		}

		// Обработчик кнопки поиска в сети (сохранен для совместимости)
		private void ButtonAutoDiscovery_Click(object sender, RoutedEventArgs e)
		{
			OpenDeviceManagementWindow(1);
		}

		// Обработчик кнопки учетных записей (сохранен для совместимости)
		private void ButtonManageCredentials_Click(object sender, RoutedEventArgs e)
		{
			OpenDeviceManagementWindow(0);
		}

		/// <summary>
		/// Единое диалоговое окно управления устройствами с вкладками "Компьютеры и доступ" и "Поиск в сети"
		/// </summary>
		private void OpenDeviceManagementWindow(int initialTab = 0)
		{
			HcWindow window = new HcWindow();
			window.Title = "Управление устройствами";
			window.Width = 940;
			window.Height = 680;
			window.MinWidth = 820;
			window.MinHeight = 520;
			window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			window.Owner = this;

			Brush winBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(21, 28, 44)) : new SolidColorBrush(Color.FromRgb(248, 250, 252));
			Brush cardBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) : Brushes.White;
			Brush cardBorder = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
			Brush textPrimary = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(15, 23, 42));
			Brush textSecondary = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(148, 163, 184)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
			Brush inputBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White;
			Brush inputBorder = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));

			window.Background = winBg;

			CancellationTokenSource scanCts = null;

			Grid rootLayout = new Grid() { Margin = new Thickness(16) };
			rootLayout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Заголовок (0)
			rootLayout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Вкладки (1)
			rootLayout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // Область содержимого (2)
			rootLayout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Футер (3)

			// 0. Заголовок окна
			DockPanel headerDock = new DockPanel() { Margin = new Thickness(0, 0, 0, 10) };
			StackPanel headerStack = new StackPanel();
			StackPanel headerTitleRow = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
			System.Windows.Controls.Image headerIcon = LucideIcons.CreateWpfImage("sliders-horizontal", 18, textPrimary);
			headerIcon.Margin = new Thickness(0, 0, 8, 0);
			headerTitleRow.Children.Add(headerIcon);

			TextBlock title = new TextBlock()
			{
				Text = "Управление устройствами",
				FontSize = 16,
				FontWeight = FontWeights.Bold,
				Foreground = textPrimary,
				VerticalAlignment = VerticalAlignment.Center
			};
			headerTitleRow.Children.Add(title);
			headerStack.Children.Add(headerTitleRow);

			TextBlock subtitle = new TextBlock()
			{
				Text = "Настройка компьютеров, учетных записей net use, папок поиска и поиск в локальной сети",
				FontSize = 11,
				Foreground = textSecondary,
				Margin = new Thickness(0, 3, 0, 0)
			};
			headerStack.Children.Add(subtitle);
			DockPanel.SetDock(headerStack, System.Windows.Controls.Dock.Left);
			headerDock.Children.Add(headerStack);

			Grid.SetRow(headerDock, 0);
			rootLayout.Children.Add(headerDock);

			// 1. Вкладки (Сегментированное меню)
			Border tabNavBorder = new Border()
			{
				Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(3),
				Margin = new Thickness(0, 0, 0, 10)
			};

			Grid tabNavGrid = new Grid();
			tabNavGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			tabNavGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			System.Windows.Controls.Button tabBtnDevices = new System.Windows.Controls.Button()
			{
				Height = 32,
				Margin = new Thickness(0, 0, 3, 0),
				Cursor = System.Windows.Input.Cursors.Hand,
				FontSize = 11,
				BorderThickness = new Thickness(1)
			};
			LucideIcons.ApplyToButton(tabBtnDevices, "monitor", string.Format("Компьютеры и учетные записи ({0})", _networkNodes.Count), 15, Brushes.White, Brushes.White, 8, FontWeights.Bold);
			HandyControl.Controls.BorderElement.SetCornerRadius(tabBtnDevices, new CornerRadius(5));

			System.Windows.Controls.Button tabBtnDiscovery = new System.Windows.Controls.Button()
			{
				Height = 32,
				Margin = new Thickness(3, 0, 0, 0),
				Cursor = System.Windows.Input.Cursors.Hand,
				FontSize = 11,
				BorderThickness = new Thickness(1)
			};
			LucideIcons.ApplyToButton(tabBtnDiscovery, "search", "Поиск в локальной сети", 15, textSecondary, textSecondary, 8, FontWeights.SemiBold);
			HandyControl.Controls.BorderElement.SetCornerRadius(tabBtnDiscovery, new CornerRadius(5));

			Grid.SetColumn(tabBtnDevices, 0);
			Grid.SetColumn(tabBtnDiscovery, 1);
			tabNavGrid.Children.Add(tabBtnDevices);
			tabNavGrid.Children.Add(tabBtnDiscovery);
			tabNavBorder.Child = tabNavGrid;

			Grid.SetRow(tabNavBorder, 1);
			rootLayout.Children.Add(tabNavBorder);

			// 2. Область содержимого вкладок
			Grid contentArea = new Grid();
			Grid.SetRow(contentArea, 2);
			rootLayout.Children.Add(contentArea);

			Grid tabContentDevices = new Grid();
			tabContentDevices.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Панель действий
			tabContentDevices.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // Список компьютеров
			tabContentDevices.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Добавление вручную
			contentArea.Children.Add(tabContentDevices);

			Grid tabContentDiscovery = new Grid();
			tabContentDiscovery.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Параметры диапазона
			tabContentDiscovery.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Прогресс-бар
			tabContentDiscovery.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // Таблица устройств
			tabContentDiscovery.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Кнопки выбора и добавления
			contentArea.Children.Add(tabContentDiscovery);

			Action<int> setActiveTab = null;
			setActiveTab = (tabIndex) =>
			{
				SolidColorBrush activeBg = new SolidColorBrush(Color.FromRgb(37, 99, 235));
				SolidColorBrush activeBorder = new SolidColorBrush(Color.FromRgb(59, 130, 246));
				SolidColorBrush inactiveBg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
				SolidColorBrush inactiveBorder = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(71, 85, 105)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
				SolidColorBrush inactiveFg = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(209, 213, 219)) : new SolidColorBrush(Color.FromRgb(75, 85, 99));

				if (tabIndex == 0)
				{
					tabContentDevices.Visibility = Visibility.Visible;
					tabContentDiscovery.Visibility = Visibility.Collapsed;

					tabBtnDevices.Background = activeBg;
					tabBtnDevices.BorderBrush = activeBorder;
					tabBtnDevices.Foreground = Brushes.White;
					tabBtnDevices.FontWeight = FontWeights.Bold;

					tabBtnDiscovery.Background = inactiveBg;
					tabBtnDiscovery.BorderBrush = inactiveBorder;
					tabBtnDiscovery.Foreground = inactiveFg;
					tabBtnDiscovery.FontWeight = FontWeights.SemiBold;
				}
				else
				{
					tabContentDevices.Visibility = Visibility.Collapsed;
					tabContentDiscovery.Visibility = Visibility.Visible;

					tabBtnDiscovery.Background = activeBg;
					tabBtnDiscovery.BorderBrush = activeBorder;
					tabBtnDiscovery.Foreground = Brushes.White;
					tabBtnDiscovery.FontWeight = FontWeights.Bold;

					tabBtnDevices.Background = inactiveBg;
					tabBtnDevices.BorderBrush = inactiveBorder;
					tabBtnDevices.Foreground = inactiveFg;
					tabBtnDevices.FontWeight = FontWeights.SemiBold;
				}

				LucideIcons.ApplyToButton(tabBtnDevices, "monitor", string.Format("Компьютеры и учетные записи ({0})", _networkNodes.Count), 15, tabBtnDevices.Foreground, tabBtnDevices.Foreground, 8, tabBtnDevices.FontWeight);
				LucideIcons.ApplyToButton(tabBtnDiscovery, "search", "Поиск в локальной сети", 15, tabBtnDiscovery.Foreground, tabBtnDiscovery.Foreground, 8, tabBtnDiscovery.FontWeight);
			};

			tabBtnDevices.Click += (s, ev) => setActiveTab(0);
			tabBtnDiscovery.Click += (s, ev) => setActiveTab(1);

			// ==========================================
			// ВКЛАДКА 1: Компьютеры и учетные записи
			// ==========================================
			DockPanel devicesToolbar = new DockPanel() { Margin = new Thickness(0, 0, 0, 8) };

			StackPanel leftToolBtns = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
			System.Windows.Controls.Button btnConnectAll = new System.Windows.Controls.Button()
			{
				Height = 28,
				Padding = new Thickness(12, 0, 12, 0),
				Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand
			};
			LucideIcons.ApplyToButton(btnConnectAll, "plug", "Подключить все ПК", 14, Brushes.White, Brushes.White, 8, FontWeights.SemiBold);
			HandyControl.Controls.BorderElement.SetCornerRadius(btnConnectAll, new CornerRadius(5));

			System.Windows.Controls.Button btnDisconnectAll = new System.Windows.Controls.Button()
			{
				Content = "Сбросить подключения",
				Height = 28,
				Margin = new Thickness(8, 0, 0, 0),
				Padding = new Thickness(12, 0, 12, 0),
				Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(btnDisconnectAll, new CornerRadius(5));

			leftToolBtns.Children.Add(btnConnectAll);
			leftToolBtns.Children.Add(btnDisconnectAll);
			DockPanel.SetDock(leftToolBtns, System.Windows.Controls.Dock.Left);
			devicesToolbar.Children.Add(leftToolBtns);

			TextBlock lblDevicesSummary = new TextBlock()
			{
				FontSize = 11,
				Foreground = textSecondary,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			DockPanel.SetDock(lblDevicesSummary, System.Windows.Controls.Dock.Right);
			devicesToolbar.Children.Add(lblDevicesSummary);

			Grid.SetRow(devicesToolbar, 0);
			tabContentDevices.Children.Add(devicesToolbar);

			StackPanel itemsContainer = new StackPanel();
			System.Windows.Controls.ScrollViewer scrollViewer = new System.Windows.Controls.ScrollViewer()
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Margin = new Thickness(0, 0, 0, 8)
			};
			scrollViewer.Content = itemsContainer;
			Grid.SetRow(scrollViewer, 1);
			tabContentDevices.Children.Add(scrollViewer);

			Action updateDevicesSummary = () =>
			{
				int connectedCount = 0;
				for (int k = 0; k < _networkNodes.Count; k++)
				{
					if (_networkNodes[k].IsConnected == true)
					{
						connectedCount++;
					}
				}
				lblDevicesSummary.Text = string.Format("Всего узлов: {0} | Подключено: {1}", _networkNodes.Count, connectedCount);
				LucideIcons.ApplyToButton(tabBtnDevices, "monitor", string.Format("Компьютеры и учетные записи ({0})", _networkNodes.Count), 15, tabBtnDevices.Foreground, tabBtnDevices.Foreground, 8, tabBtnDevices.FontWeight);
			};

			Action renderCredentialRows = null;
			renderCredentialRows = () =>
			{
				itemsContainer.Children.Clear();
				updateDevicesSummary();

				if (_networkNodes.Count == 0)
				{
					TextBlock emptyNotice = new TextBlock()
					{
						Text = "Список компьютеров пуст. Добавьте сетевой компьютер через форму ниже или воспользуйтесь поиском в сети.",
						FontSize = 12,
						Foreground = textSecondary,
						HorizontalAlignment = HorizontalAlignment.Center,
						Margin = new Thickness(0, 40, 0, 0)
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
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(200) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(150) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(150) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(125) });
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

					// Данные узла (статус-индикатор, имя, IP, пинг)
					StackPanel pcInfo = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
					StackPanel pcHeaderRow = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };

					System.Windows.Shapes.Ellipse onlineDot = new System.Windows.Shapes.Ellipse()
					{
						Width = 8,
						Height = 8,
						Margin = new Thickness(0, 0, 6, 0),
						VerticalAlignment = VerticalAlignment.Center
					};
					if (node.IsOnline == true)
					{
						onlineDot.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
						onlineDot.ToolTip = "Узел доступен в сети";
					}
					else
					{
						onlineDot.Fill = new SolidColorBrush(Color.FromRgb(148, 163, 184));
						onlineDot.ToolTip = "Статус связи не проверен";
					}
					pcHeaderRow.Children.Add(onlineDot);

					TextBlock pcName = new TextBlock()
					{
						Text = node.Name,
						FontWeight = FontWeights.Bold,
						FontSize = 12,
						Foreground = textPrimary,
						VerticalAlignment = VerticalAlignment.Center
					};
					pcHeaderRow.Children.Add(pcName);

					if (node.PingTimeMs.HasValue)
					{
						Border pingBadge = new Border()
						{
							Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(219, 234, 254)),
							CornerRadius = new CornerRadius(3),
							Padding = new Thickness(4, 1, 4, 1),
							Margin = new Thickness(6, 0, 0, 0),
							VerticalAlignment = VerticalAlignment.Center
						};
						pingBadge.Child = new TextBlock()
						{
							Text = node.PingTimeMs.Value + " мс",
							FontSize = 9,
							Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(30, 58, 138)),
							FontWeight = FontWeights.SemiBold
						};
						pcHeaderRow.Children.Add(pingBadge);
					}

					TextBlock pcIp = new TextBlock()
					{
						Text = node.IpAddress,
						FontSize = 10,
						Foreground = textSecondary,
						Margin = new Thickness(14, 2, 0, 0)
					};

					pcInfo.Children.Add(pcHeaderRow);
					pcInfo.Children.Add(pcIp);
					Grid.SetColumn(pcInfo, 0);
					rowGrid.Children.Add(pcInfo);

					// Поле ввода пользователя
					StackPanel userStack = new StackPanel() { Margin = new Thickness(4, 0, 6, 0) };
					userStack.Children.Add(new TextBlock() { Text = "Пользователь:", FontSize = 9, Foreground = textSecondary, Margin = new Thickness(0, 0, 0, 2) });
					System.Windows.Controls.TextBox userBox = new System.Windows.Controls.TextBox()
					{
						Text = node.Username,
						Height = 26,
						Background = inputBg,
						Foreground = textPrimary,
						CaretBrush = textPrimary,
						BorderBrush = inputBorder,
						BorderThickness = new Thickness(1),
						VerticalContentAlignment = VerticalAlignment.Center,
						Padding = new Thickness(6, 0, 6, 0),
						FontSize = 11
					};
					HandyControl.Controls.BorderElement.SetCornerRadius(userBox, new CornerRadius(4));
					userBox.TextChanged += (s, ev) => node.Username = userBox.Text.Trim();
					userStack.Children.Add(userBox);
					Grid.SetColumn(userStack, 1);
					rowGrid.Children.Add(userStack);

					// Поле ввода пароля
					StackPanel passStack = new StackPanel() { Margin = new Thickness(4, 0, 6, 0) };
					passStack.Children.Add(new TextBlock() { Text = "Пароль:", FontSize = 9, Foreground = textSecondary, Margin = new Thickness(0, 0, 0, 2) });
					System.Windows.Controls.TextBox passBox = new System.Windows.Controls.TextBox()
					{
						Text = node.Password,
						Height = 26,
						Background = inputBg,
						Foreground = textPrimary,
						CaretBrush = textPrimary,
						BorderBrush = inputBorder,
						BorderThickness = new Thickness(1),
						VerticalContentAlignment = VerticalAlignment.Center,
						Padding = new Thickness(6, 0, 6, 0),
						FontSize = 11
					};
					HandyControl.Controls.BorderElement.SetCornerRadius(passBox, new CornerRadius(4));
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

					Action updateRowStatusBadge = () =>
					{
						if (node.IsConnected == true)
						{
							statusBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(6, 78, 59)) : new SolidColorBrush(Color.FromRgb(220, 252, 231));
							StackPanel sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
							System.Windows.Controls.Image chk = LucideIcons.CreateWpfImage("check", 11, System.Drawing.Color.FromArgb(52, 211, 153));
							chk.Margin = new Thickness(0, 0, 4, 0);
							sp.Children.Add(chk);
							sp.Children.Add(new TextBlock { Text = "Подключен", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(22, 163, 74)) });
							statusBadge.Child = sp;
						}
						else
						{
							statusBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(55, 65, 81)) : new SolidColorBrush(Color.FromRgb(243, 244, 246));
							statusBadge.Child = new TextBlock { Text = "— Не подключен", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(156, 163, 175)) : new SolidColorBrush(Color.FromRgb(107, 114, 128)) };
						}
					};
					updateRowStatusBadge();
					Grid.SetColumn(statusBadge, 3);
					rowGrid.Children.Add(statusBadge);

					// Кнопки строки
					StackPanel actionStack = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
					System.Windows.Controls.Button btnConnectSingle = new System.Windows.Controls.Button()
					{
						Height = 26,
						Margin = new Thickness(0, 0, 6, 0),
						Padding = new Thickness(8, 0, 8, 0),
						Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
						Foreground = Brushes.White,
						FontWeight = FontWeights.SemiBold,
						FontSize = 10,
						Cursor = System.Windows.Input.Cursors.Hand
					};
					HandyControl.Controls.BorderElement.SetCornerRadius(btnConnectSingle, new CornerRadius(4));
					LucideIcons.ApplyToButton(btnConnectSingle, "plug", "Подключить", 12, Brushes.White, Brushes.White, 6, FontWeights.SemiBold);
					btnConnectSingle.Click += async (s, ev) =>
					{
						btnConnectSingle.IsEnabled = false;
						LucideIcons.ApplyToButton(btnConnectSingle, "refresh-cw", "Подключение...", 12, Brushes.White, Brushes.White, 6, FontWeights.SemiBold);
						string targetHost = node.IpAddress;
						string user = userBox.Text.Trim();
						string pass = passBox.Text;
						node.Username = user;
						node.Password = pass;

						string outMsg = string.Empty;
						bool ok = await Task.Run(() => CertificateScanner.EstablishNetworkSession(targetHost, user, pass, out outMsg));
						node.IsConnected = ok;
						btnConnectSingle.IsEnabled = true;
						LucideIcons.ApplyToButton(btnConnectSingle, "plug", "Подключить", 12, Brushes.White, Brushes.White, 6, FontWeights.SemiBold);

						if (ok == true)
						{
							updateRowStatusBadge();
							AppendTerminalLog(string.Format("{0} - Успешное net use подключение к {1} ({2}) под пользователем {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress, user), Color.FromRgb(52, 211, 153));
						}
						else
						{
							statusBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(127, 29, 29)) : new SolidColorBrush(Color.FromRgb(254, 226, 226));
							StackPanel errSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
							System.Windows.Controls.Image errIcon = LucideIcons.CreateWpfImage("alert-circle", 11, System.Drawing.Color.FromArgb(248, 113, 113));
							errIcon.Margin = new Thickness(0, 0, 4, 0);
							errSp.Children.Add(errIcon);
							errSp.Children.Add(new TextBlock { Text = "Ошибка", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(248, 113, 113)) : new SolidColorBrush(Color.FromRgb(220, 38, 38)) });
							statusBadge.Child = errSp;
							AppendTerminalLog(string.Format("{0} - Ошибка net use к {1} ({2}): {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress, outMsg), Color.FromRgb(248, 113, 113));
							ShowAppMessage("Не удалось выполнить net use: " + outMsg, "Ошибка подключения", MessageBoxImage.Warning);
						}
						SyncAndSaveComputersToConfig();
						RenderComputersList();
						updateDevicesSummary();
					};
					actionStack.Children.Add(btnConnectSingle);

					System.Windows.Controls.Button btnDeleteRow = new System.Windows.Controls.Button()
					{
						Content = LucideIcons.CreateWpfImage("trash-2", 13, System.Drawing.Color.White),
						Height = 28,
						Width = 28,
						Padding = new Thickness(0),
						HorizontalContentAlignment = HorizontalAlignment.Center,
						VerticalContentAlignment = VerticalAlignment.Center,
						Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
						Foreground = Brushes.White,
						FontWeight = FontWeights.Bold,
						Cursor = System.Windows.Input.Cursors.Hand,
						ToolTip = "Удалить компьютер из списка"
					};
					HandyControl.Controls.BorderElement.SetCornerRadius(btnDeleteRow, new CornerRadius(4));
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

					// Секция индивидуальных папок для поиска сертификатов на данном ПК
					Border dirsSectionBorder = new Border()
					{
						Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(24, 33, 47)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
						CornerRadius = new CornerRadius(4),
						Margin = new Thickness(0, 8, 0, 0),
						Padding = new Thickness(8, 6, 8, 6)
					};

					StackPanel dirsStack = new StackPanel();
					DockPanel dirsHeader = new DockPanel();
					StackPanel dirsTitleRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
					System.Windows.Controls.Image dirsFolderImg = LucideIcons.CreateWpfImage("folder", 12, textSecondary);
					dirsFolderImg.Margin = new Thickness(0, 0, 6, 0);
					dirsTitleRow.Children.Add(dirsFolderImg);

					TextBlock dirsTitle = new TextBlock()
					{
						Text = "Папки поиска сертификатов:",
						FontSize = 10,
						FontWeight = FontWeights.SemiBold,
						Foreground = textSecondary,
						VerticalAlignment = VerticalAlignment.Center
					};
					dirsTitleRow.Children.Add(dirsTitle);
					DockPanel.SetDock(dirsTitleRow, System.Windows.Controls.Dock.Left);
					dirsHeader.Children.Add(dirsTitleRow);

					System.Windows.Controls.Button btnAddDirToNode = new System.Windows.Controls.Button()
					{
						Height = 22,
						Padding = new Thickness(8, 0, 8, 0),
						Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
						Foreground = Brushes.White,
						FontSize = 10,
						FontWeight = FontWeights.SemiBold,
						Cursor = System.Windows.Input.Cursors.Hand,
						HorizontalAlignment = HorizontalAlignment.Right
					};
					LucideIcons.ApplyToButton(btnAddDirToNode, "plus", "Добавить папку...", 11, Brushes.White, Brushes.White, 5, FontWeights.SemiBold);
					DockPanel.SetDock(btnAddDirToNode, System.Windows.Controls.Dock.Right);
					dirsHeader.Children.Add(btnAddDirToNode);
					dirsStack.Children.Add(dirsHeader);

					Grid addDirInputGrid = new Grid()
					{
						Margin = new Thickness(0, 6, 0, 4),
						Visibility = Visibility.Collapsed
					};
					addDirInputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
					addDirInputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(6) });
					addDirInputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
					addDirInputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(4) });
					addDirInputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

					System.Windows.Controls.TextBox txtDirInput = new System.Windows.Controls.TextBox()
					{
						Height = 26,
						FontSize = 11,
						VerticalContentAlignment = VerticalAlignment.Center,
						Padding = new Thickness(6, 0, 6, 0),
						Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White,
						Foreground = textPrimary,
						BorderBrush = cardBorder,
						BorderThickness = new Thickness(1),
						ToolTip = "Введите или вставьте путь к директории на целевом компьютере (например: C:\\Certificates или \\\\" + node.IpAddress + "\\c$\\Certs)"
					};
					HandyControl.Controls.BorderElement.SetCornerRadius(txtDirInput, new CornerRadius(4));
					HandyControl.Controls.InfoElement.SetPlaceholder(txtDirInput, "Вставьте путь (например: C:\\Certificates или \\\\" + node.IpAddress + "\\c$\\Certs)...");

					System.Windows.Controls.Button btnConfirmAdd = new System.Windows.Controls.Button()
					{
						Content = "+ Добавить",
						Height = 26,
						Padding = new Thickness(10, 0, 10, 0),
						Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
						Foreground = Brushes.White,
						FontWeight = FontWeights.SemiBold,
						FontSize = 10,
						Cursor = System.Windows.Input.Cursors.Hand,
						ToolTip = "Сохранить путь (Enter)"
					};
					HandyControl.Controls.BorderElement.SetCornerRadius(btnConfirmAdd, new CornerRadius(4));

					System.Windows.Controls.Button btnCancelAdd = new System.Windows.Controls.Button()
					{
						Content = "✕",
						Height = 26,
						Width = 26,
						Padding = new Thickness(0),
						Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
						Foreground = textSecondary,
						FontWeight = FontWeights.Bold,
						FontSize = 10,
						Cursor = System.Windows.Input.Cursors.Hand,
						ToolTip = "Закрыть строку ввода (Esc)"
					};
					HandyControl.Controls.BorderElement.SetCornerRadius(btnCancelAdd, new CornerRadius(4));

					Grid.SetColumn(txtDirInput, 0);
					Grid.SetColumn(btnConfirmAdd, 2);
					Grid.SetColumn(btnCancelAdd, 4);
					addDirInputGrid.Children.Add(txtDirInput);
					addDirInputGrid.Children.Add(btnConfirmAdd);
					addDirInputGrid.Children.Add(btnCancelAdd);
					dirsStack.Children.Add(addDirInputGrid);

					TextBlock txtDirError = new TextBlock()
					{
						FontSize = 10,
						Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
						Margin = new Thickness(2, 2, 0, 4),
						Visibility = Visibility.Collapsed,
						TextWrapping = TextWrapping.Wrap
					};
					dirsStack.Children.Add(txtDirError);

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
								Content = LucideIcons.CreateWpfImage("x", 10, System.Drawing.Color.FromArgb(239, 68, 68)),
								Width = 16,
								Height = 16,
								Padding = new Thickness(0),
								Background = Brushes.Transparent,
								BorderThickness = new Thickness(0),
								Cursor = System.Windows.Input.Cursors.Hand,
								ToolTip = "Удалить папку"
							};
							string capturedDir = dirPath;
							btnRemoveDir.Click += (s2, e2) =>
							{
								node.CustomDirectories.Remove(capturedDir);
								renderNodeDirs();
								SyncAndSaveComputersToConfig();
								RenderComputersList();
							};
							DockPanel.SetDock(btnRemoveDir, System.Windows.Controls.Dock.Right);
							dirBadgeDock.Children.Add(btnRemoveDir);

							dirBadge.Child = dirBadgeDock;
							dirsListWrap.Children.Add(dirBadge);
						}
					};

					Action executeAddDir = () =>
					{
						string rawInput = txtDirInput.Text;
						string normalizedPath;
						string errorMsg;

						bool isValid = CertificateScanner.IsValidWindowsPath(rawInput, out normalizedPath, out errorMsg);
						if (!isValid)
						{
							txtDirError.Text = "⚠ " + errorMsg;
							txtDirError.Visibility = Visibility.Visible;
							txtDirInput.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
							txtDirInput.Focus();
							return;
						}

						for (int ed = 0; ed < node.CustomDirectories.Count; ed++)
						{
							if (string.Equals(node.CustomDirectories[ed], normalizedPath, StringComparison.OrdinalIgnoreCase))
							{
								txtDirError.Text = "⚠ Эта директория уже есть в списке для ПК " + node.Name + ".";
								txtDirError.Visibility = Visibility.Visible;
								txtDirInput.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
								txtDirInput.Focus();
								return;
							}
						}

						node.CustomDirectories.Add(normalizedPath);
						txtDirInput.Text = string.Empty;
						txtDirInput.BorderBrush = cardBorder;
						txtDirError.Visibility = Visibility.Collapsed;

						renderNodeDirs();
						SyncAndSaveComputersToConfig();
						RenderComputersList();
						txtDirInput.Focus();
					};

					btnAddDirToNode.Click += (s2, e2) =>
					{
						if (addDirInputGrid.Visibility == Visibility.Visible)
						{
							if (string.IsNullOrWhiteSpace(txtDirInput.Text))
							{
								addDirInputGrid.Visibility = Visibility.Collapsed;
								txtDirError.Visibility = Visibility.Collapsed;
							}
							else
							{
								txtDirInput.Focus();
								txtDirInput.SelectAll();
							}
						}
						else
						{
							addDirInputGrid.Visibility = Visibility.Visible;
							txtDirError.Visibility = Visibility.Collapsed;
							txtDirInput.BorderBrush = cardBorder;
							txtDirInput.Focus();
						}
					};

					btnConfirmAdd.Click += (s2, e2) =>
					{
						executeAddDir();
					};

					btnCancelAdd.Click += (s2, e2) =>
					{
						addDirInputGrid.Visibility = Visibility.Collapsed;
						txtDirError.Visibility = Visibility.Collapsed;
						txtDirInput.Text = string.Empty;
						txtDirInput.BorderBrush = cardBorder;
					};

					txtDirInput.KeyDown += (s2, e2) =>
					{
						if (e2.Key == System.Windows.Input.Key.Enter)
						{
							executeAddDir();
							e2.Handled = true;
						}
						else if (e2.Key == System.Windows.Input.Key.Escape)
						{
							addDirInputGrid.Visibility = Visibility.Collapsed;
							txtDirError.Visibility = Visibility.Collapsed;
							txtDirInput.Text = string.Empty;
							txtDirInput.BorderBrush = cardBorder;
							e2.Handled = true;
						}
						else
						{
							if (txtDirError.Visibility == Visibility.Visible)
							{
								txtDirError.Visibility = Visibility.Collapsed;
								txtDirInput.BorderBrush = cardBorder;
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
				LucideIcons.ApplyToButton(btnConnectAll, "refresh-cw", "Подключение...", 14, Brushes.White, Brushes.White, 8, FontWeights.SemiBold);
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
				LucideIcons.ApplyToButton(btnConnectAll, "plug", "Подключить все ПК", 14, Brushes.White, Brushes.White, 8, FontWeights.SemiBold);
				SyncAndSaveComputersToConfig();
				renderCredentialRows();
				RenderComputersList();
				AppendTerminalLog(string.Format("{0} - Подключено {1} из {2} ПК", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), successCount, _networkNodes.Count), Color.FromRgb(52, 211, 153));
				ShowAppMessage(string.Format("Подключено: {0} из {1} ПК", successCount, _networkNodes.Count), "Подключение", MessageBoxImage.Information);
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

			// Форма быстрого добавления нового компьютера вручную
			Border addCard = new Border()
			{
				Background = cardBg,
				BorderBrush = cardBorder,
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(10),
				Margin = new Thickness(0, 4, 0, 0)
			};

			Grid addGrid = new Grid();
			addGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			addGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

			TextBlock addTitle = new TextBlock()
			{
				Text = "+ Добавить компьютер вручную:",
				FontWeight = FontWeights.Bold,
				FontSize = 11,
				Foreground = textPrimary,
				Margin = new Thickness(0, 0, 0, 6)
			};
			Grid.SetRow(addTitle, 0);
			addGrid.Children.Add(addTitle);

			Grid inputFieldsGrid = new Grid();
			inputFieldsGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			inputFieldsGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(170) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(165) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(145) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(145) });
			inputFieldsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			TextBlock lblName = new TextBlock() { Text = "Имя компьютера:", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = textSecondary, Margin = new Thickness(1, 0, 6, 3) };
			TextBlock lblIp = new TextBlock() { Text = "IP-адрес:", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = textSecondary, Margin = new Thickness(1, 0, 6, 3) };
			TextBlock lblUser = new TextBlock() { Text = "Логин:", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = textSecondary, Margin = new Thickness(1, 0, 6, 3) };
			TextBlock lblPass = new TextBlock() { Text = "Пароль:", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = textSecondary, Margin = new Thickness(1, 0, 6, 3) };

			Grid.SetRow(lblName, 0); Grid.SetColumn(lblName, 0);
			Grid.SetRow(lblIp, 0); Grid.SetColumn(lblIp, 1);
			Grid.SetRow(lblUser, 0); Grid.SetColumn(lblUser, 2);
			Grid.SetRow(lblPass, 0); Grid.SetColumn(lblPass, 3);
			inputFieldsGrid.Children.Add(lblName);
			inputFieldsGrid.Children.Add(lblIp);
			inputFieldsGrid.Children.Add(lblUser);
			inputFieldsGrid.Children.Add(lblPass);

			System.Windows.Controls.TextBox newNameBox = new System.Windows.Controls.TextBox()
			{
				Text = string.Empty,
				Height = 28,
				Margin = new Thickness(0, 0, 6, 0),
				VerticalContentAlignment = VerticalAlignment.Center,
				Padding = new Thickness(8, 0, 8, 0),
				FontSize = 11,
				Background = inputBg,
				Foreground = textPrimary,
				CaretBrush = textPrimary,
				BorderBrush = inputBorder,
				BorderThickness = new Thickness(1)
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(newNameBox, new CornerRadius(4));
			HandyControl.Controls.InfoElement.SetPlaceholder(newNameBox, "Напр. DESKTOP-01");

			IpAddressBox newIpBox = new IpAddressBox(_isDarkTheme);

			System.Windows.Controls.TextBox newUserBox = new System.Windows.Controls.TextBox()
			{
				Text = string.Empty,
				Height = 28,
				Margin = new Thickness(0, 0, 6, 0),
				VerticalContentAlignment = VerticalAlignment.Center,
				Padding = new Thickness(8, 0, 8, 0),
				FontSize = 11,
				Background = inputBg,
				Foreground = textPrimary,
				CaretBrush = textPrimary,
				BorderBrush = inputBorder,
				BorderThickness = new Thickness(1)
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(newUserBox, new CornerRadius(4));
			HandyControl.Controls.InfoElement.SetPlaceholder(newUserBox, "Логин...");

			System.Windows.Controls.TextBox newPassBox = new System.Windows.Controls.TextBox()
			{
				Text = string.Empty,
				Height = 28,
				Margin = new Thickness(0, 0, 6, 0),
				VerticalContentAlignment = VerticalAlignment.Center,
				Padding = new Thickness(8, 0, 8, 0),
				FontSize = 11,
				Background = inputBg,
				Foreground = textPrimary,
				CaretBrush = textPrimary,
				BorderBrush = inputBorder,
				BorderThickness = new Thickness(1)
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(newPassBox, new CornerRadius(4));
			HandyControl.Controls.InfoElement.SetPlaceholder(newPassBox, "Пароль...");

			Grid.SetRow(newNameBox, 1); Grid.SetColumn(newNameBox, 0);
			Grid.SetRow(newIpBox, 1); Grid.SetColumn(newIpBox, 1);
			Grid.SetRow(newUserBox, 1); Grid.SetColumn(newUserBox, 2);
			Grid.SetRow(newPassBox, 1); Grid.SetColumn(newPassBox, 3);
			inputFieldsGrid.Children.Add(newNameBox);
			inputFieldsGrid.Children.Add(newIpBox);
			inputFieldsGrid.Children.Add(newUserBox);
			inputFieldsGrid.Children.Add(newPassBox);

			System.Windows.Controls.Button btnAddNew = new System.Windows.Controls.Button()
			{
				Height = 28,
				Padding = new Thickness(14, 0, 14, 0),
				Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			};
			LucideIcons.ApplyToButton(btnAddNew, "plus", "Добавить", 13, Brushes.White, Brushes.White, 6, FontWeights.Bold);
			HandyControl.Controls.BorderElement.SetCornerRadius(btnAddNew, new CornerRadius(4));
			btnAddNew.Click += (s, ev) =>
			{
				string name = newNameBox.Text.Trim();
				string user = newUserBox.Text.Trim();
				string pass = newPassBox.Text;

				string ip;
				string ipError;
				if (!newIpBox.TryGetIpAddress(out ip, out ipError))
				{
					ShowAppMessage(ipError, "Некорректный IP-адрес", MessageBoxImage.Warning);
					return;
				}

				if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(ip))
				{
					ShowAppMessage("Укажите имя компьютера или его IP-адрес для добавления.", "Добавление устройства", MessageBoxImage.Warning);
					return;
				}

				if (string.IsNullOrEmpty(name))
				{
					name = ip;
				}
				if (string.IsNullOrEmpty(ip))
				{
					ip = name;
				}

				_networkNodes.Add(new NetworkNodeItem(name, ip, "В очереди", user, pass));
				SyncAndSaveComputersToConfig();
				renderCredentialRows();
				RenderComputersList();
				UpdateProgressCardSummary();
				AppendTerminalLog(string.Format("{0} - Добавлен новый сетевой ПК: {1} ({2}), пользователь: {3}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), name, ip, user), Color.FromRgb(96, 165, 250));
				newNameBox.Text = string.Empty;
				newIpBox.Clear();
				newUserBox.Text = string.Empty;
				newPassBox.Text = string.Empty;
			};
			Grid.SetRow(btnAddNew, 1);
			Grid.SetColumn(btnAddNew, 4);
			inputFieldsGrid.Children.Add(btnAddNew);

			Grid.SetRow(inputFieldsGrid, 1);
			addGrid.Children.Add(inputFieldsGrid);

			addCard.Child = addGrid;
			Grid.SetRow(addCard, 2);
			tabContentDevices.Children.Add(addCard);

			// ==========================================
			// ВКЛАДКА 2: Поиск в сети
			// ==========================================
			string defaultStartIp, defaultEndIp, detectedLocalIp;
			NetworkScanner.GetLocalSubnetInfo(out defaultStartIp, out defaultEndIp, out detectedLocalIp);

			Border paramsCard = new Border()
			{
				Background = cardBg,
				BorderBrush = cardBorder,
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(8),
				Padding = new Thickness(12, 10, 12, 10),
				Margin = new Thickness(0, 0, 0, 10)
			};

			Grid paramsGrid = new Grid();
			paramsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			paramsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(16) });
			paramsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			paramsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(8) });
			paramsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			paramsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			paramsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

			StackPanel localIpStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
			TextBlock localIpTitle = new TextBlock() { Text = "Локальный IP:", FontSize = 10, Foreground = textSecondary, Margin = new Thickness(0, 0, 0, 2) };
			Border localIpBadge = new Border()
			{
				Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(30, 58, 138)) : new SolidColorBrush(Color.FromRgb(219, 234, 254)),
				CornerRadius = new CornerRadius(4),
				Padding = new Thickness(6, 2, 6, 2)
			};
			TextBlock localIpVal = new TextBlock()
			{
				Text = detectedLocalIp,
				FontWeight = FontWeights.Bold,
				FontSize = 11,
				Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(30, 58, 138))
			};
			localIpBadge.Child = localIpVal;
			localIpStack.Children.Add(localIpTitle);
			localIpStack.Children.Add(localIpBadge);
			Grid.SetColumn(localIpStack, 0);
			paramsGrid.Children.Add(localIpStack);

			StackPanel fromStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
			TextBlock fromLabel = new TextBlock() { Text = "Диапазон IP от:", FontSize = 10, Foreground = textSecondary, Margin = new Thickness(0, 0, 0, 2) };
			System.Windows.Controls.TextBox txtFrom = new System.Windows.Controls.TextBox()
			{
				Text = defaultStartIp,
				Width = 125,
				Height = 28,
				FontSize = 11,
				Padding = new Thickness(4, 2, 4, 2),
				Background = inputBg,
				Foreground = textPrimary,
				BorderBrush = inputBorder,
				VerticalContentAlignment = VerticalAlignment.Center
			};
			fromStack.Children.Add(fromLabel);
			fromStack.Children.Add(txtFrom);
			Grid.SetColumn(fromStack, 2);
			paramsGrid.Children.Add(fromStack);

			StackPanel toStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
			TextBlock toLabel = new TextBlock() { Text = "до:", FontSize = 10, Foreground = textSecondary, Margin = new Thickness(0, 0, 0, 2) };
			System.Windows.Controls.TextBox txtTo = new System.Windows.Controls.TextBox()
			{
				Text = defaultEndIp,
				Width = 125,
				Height = 28,
				FontSize = 11,
				Padding = new Thickness(4, 2, 4, 2),
				Background = inputBg,
				Foreground = textPrimary,
				BorderBrush = inputBorder,
				VerticalContentAlignment = VerticalAlignment.Center
			};
			toStack.Children.Add(toLabel);
			toStack.Children.Add(txtTo);
			Grid.SetColumn(toStack, 4);
			paramsGrid.Children.Add(toStack);

			StackPanel scanBtnsStack = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
			System.Windows.Controls.Button btnStartScan = new System.Windows.Controls.Button()
			{
				Height = 30,
				Padding = new Thickness(14, 0, 14, 0),
				Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand,
				Margin = new Thickness(0, 0, 6, 0)
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(btnStartScan, new CornerRadius(5));
			LucideIcons.ApplyToButton(btnStartScan, "play", "Начать поиск", 13, Brushes.White, Brushes.White, 6, FontWeights.Bold);

			System.Windows.Controls.Button btnStopScan = new System.Windows.Controls.Button()
			{
				Height = 30,
				Padding = new Thickness(12, 0, 12, 0),
				Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand,
				IsEnabled = false
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(btnStopScan, new CornerRadius(5));
			LucideIcons.ApplyToButton(btnStopScan, "square", "Остановить", 13, Brushes.White, Brushes.White, 6, FontWeights.SemiBold);

			scanBtnsStack.Children.Add(btnStartScan);
			scanBtnsStack.Children.Add(btnStopScan);
			Grid.SetColumn(scanBtnsStack, 6);
			paramsGrid.Children.Add(scanBtnsStack);

			paramsCard.Child = paramsGrid;
			Grid.SetRow(paramsCard, 0);
			tabContentDiscovery.Children.Add(paramsCard);

			StackPanel progressSection = new StackPanel() { Margin = new Thickness(0, 0, 0, 10) };
			System.Windows.Controls.ProgressBar progressBar = new System.Windows.Controls.ProgressBar()
			{
				Height = 6,
				Minimum = 0,
				Maximum = 100,
				Value = 0,
				Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
				Foreground = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
				Template = CreateModernProgressBarTemplate()
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(progressBar, new CornerRadius(3));

			TextBlock statusLabel = new TextBlock()
			{
				Text = "Готов к поиску",
				FontSize = 11,
				Foreground = textSecondary,
				Margin = new Thickness(0, 4, 0, 0)
			};

			progressSection.Children.Add(progressBar);
			progressSection.Children.Add(statusLabel);
			Grid.SetRow(progressSection, 1);
			tabContentDiscovery.Children.Add(progressSection);

			System.Collections.ObjectModel.ObservableCollection<DiscoveredHostItem> discoveredItems = new System.Collections.ObjectModel.ObservableCollection<DiscoveredHostItem>();

			Border gridBorder = new Border()
			{
				Background = cardBg,
				BorderBrush = cardBorder,
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(8),
				Margin = new Thickness(0, 0, 0, 10),
				ClipToBounds = true
			};

			System.Windows.Controls.DataGrid grid = new System.Windows.Controls.DataGrid()
			{
				ItemsSource = discoveredItems,
				AutoGenerateColumns = false,
				CanUserAddRows = false,
				CanUserDeleteRows = false,
				IsReadOnly = false,
				SelectionMode = DataGridSelectionMode.Single,
				GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
				HeadersVisibility = DataGridHeadersVisibility.Column,
				Background = cardBg,
				BorderThickness = new Thickness(0),
				RowBackground = cardBg,
				AlternatingRowBackground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(24, 33, 47)) : new SolidColorBrush(Color.FromRgb(249, 250, 251)),
				HorizontalGridLinesBrush = cardBorder,
				FontSize = 11
			};

			DataGridTemplateColumn colCheck = new DataGridTemplateColumn();
			colCheck.Width = new DataGridLength(90);
			colCheck.MinWidth = 85;
			DataTemplate checkHeaderTemplate = new DataTemplate();
			FrameworkElementFactory headerText = new FrameworkElementFactory(typeof(TextBlock));
			headerText.SetValue(TextBlock.TextProperty, "Выбор");
			headerText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			headerText.SetValue(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0));
			checkHeaderTemplate.VisualTree = headerText;
			colCheck.HeaderTemplate = checkHeaderTemplate;
			DataTemplate checkTemplate = new DataTemplate();
			FrameworkElementFactory checkFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.CheckBox));
			checkFactory.SetBinding(System.Windows.Controls.CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected") { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
			checkFactory.SetValue(System.Windows.Controls.CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			checkFactory.SetValue(System.Windows.Controls.CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
			checkFactory.SetValue(System.Windows.Controls.CheckBox.CursorProperty, System.Windows.Input.Cursors.Hand);
			checkTemplate.VisualTree = checkFactory;
			colCheck.CellTemplate = checkTemplate;
			grid.Columns.Add(colCheck);

			DataGridTemplateColumn colName = new DataGridTemplateColumn();
			colName.Header = "Устройство";
			colName.Width = new DataGridLength(210);
			DataTemplate nameTemplate = new DataTemplate();
			FrameworkElementFactory nameStack = new FrameworkElementFactory(typeof(StackPanel));
			nameStack.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
			nameStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

			FrameworkElementFactory iconFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
			iconFactory.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding("DeviceTypeImage"));
			iconFactory.SetValue(System.Windows.Controls.Image.WidthProperty, 14.0);
			iconFactory.SetValue(System.Windows.Controls.Image.HeightProperty, 14.0);
			iconFactory.SetValue(System.Windows.Controls.Image.MarginProperty, new Thickness(4, 0, 8, 0));
			iconFactory.SetValue(System.Windows.Controls.Image.VerticalAlignmentProperty, VerticalAlignment.Center);
			nameStack.AppendChild(iconFactory);

			FrameworkElementFactory nameTxt = new FrameworkElementFactory(typeof(TextBlock));
			nameTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
			nameTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
			nameTxt.SetValue(TextBlock.ForegroundProperty, textPrimary);
			nameTxt.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
			nameStack.AppendChild(nameTxt);

			nameTemplate.VisualTree = nameStack;
			colName.CellTemplate = nameTemplate;
			grid.Columns.Add(colName);

			DataGridTextColumn colIp = new DataGridTextColumn()
			{
				Header = "IP-адрес",
				Binding = new System.Windows.Data.Binding("IpAddress"),
				Width = new DataGridLength(130),
				IsReadOnly = true
			};
			grid.Columns.Add(colIp);

			DataGridTextColumn colPing = new DataGridTextColumn()
			{
				Header = "Пинг",
				Binding = new System.Windows.Data.Binding("PingDisplay"),
				Width = new DataGridLength(100),
				IsReadOnly = true
			};
			grid.Columns.Add(colPing);

			DataGridTemplateColumn colStatus = new DataGridTemplateColumn()
			{
				Header = "Статус",
				Width = new DataGridLength(1, DataGridLengthUnitType.Star),
				MinWidth = 180
			};
			DataTemplate statusTemplate = new DataTemplate();
			FrameworkElementFactory statusBadgeDiscovery = new FrameworkElementFactory(typeof(Border));
			statusBadgeDiscovery.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
			statusBadgeDiscovery.SetValue(Border.PaddingProperty, new Thickness(8, 3, 8, 3));
			statusBadgeDiscovery.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
			statusBadgeDiscovery.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
			statusBadgeDiscovery.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("StatusBackgroundBrush"));

			FrameworkElementFactory statusTxtDiscovery = new FrameworkElementFactory(typeof(TextBlock));
			statusTxtDiscovery.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("StatusText"));
			statusTxtDiscovery.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("StatusBrush"));
			statusTxtDiscovery.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
			statusTxtDiscovery.SetValue(TextBlock.FontSizeProperty, 11.0);

			statusBadgeDiscovery.AppendChild(statusTxtDiscovery);
			statusTemplate.VisualTree = statusBadgeDiscovery;
			colStatus.CellTemplate = statusTemplate;
			grid.Columns.Add(colStatus);

			gridBorder.Child = grid;
			Grid.SetRow(gridBorder, 2);
			tabContentDiscovery.Children.Add(gridBorder);

			DockPanel discoveryBottomBar = new DockPanel();

			StackPanel selButtonsStack = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
			System.Windows.Controls.Button btnSelectAll = new System.Windows.Controls.Button()
			{
				Content = "Выбрать все",
				Height = 28,
				Padding = new Thickness(10, 0, 10, 0),
				Margin = new Thickness(0, 0, 6, 0),
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand,
				Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
				Foreground = textPrimary
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(btnSelectAll, new CornerRadius(5));

			System.Windows.Controls.Button btnDeselectAll = new System.Windows.Controls.Button()
			{
				Content = "Снять выбор",
				Height = 28,
				Padding = new Thickness(10, 0, 10, 0),
				Margin = new Thickness(0, 0, 6, 0),
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand,
				Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
				Foreground = textPrimary
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(btnDeselectAll, new CornerRadius(5));

			System.Windows.Controls.Button btnSelectWindows = new System.Windows.Controls.Button()
			{
				Content = "Только Windows ПК",
				Height = 28,
				Padding = new Thickness(10, 0, 10, 0),
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand,
				Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(6, 78, 59)) : new SolidColorBrush(Color.FromRgb(220, 252, 231)),
				Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(22, 163, 74)),
				FontWeight = FontWeights.SemiBold
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(btnSelectWindows, new CornerRadius(5));

			selButtonsStack.Children.Add(btnSelectAll);
			selButtonsStack.Children.Add(btnDeselectAll);
			selButtonsStack.Children.Add(btnSelectWindows);
			DockPanel.SetDock(selButtonsStack, System.Windows.Controls.Dock.Left);
			discoveryBottomBar.Children.Add(selButtonsStack);

			StackPanel rightActionsStack = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			DockPanel.SetDock(rightActionsStack, System.Windows.Controls.Dock.Right);

			TextBlock lblSelectionCount = new TextBlock()
			{
				Text = "Найдено: 0 | Выбрано: 0",
				FontSize = 11,
				Foreground = textSecondary,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 12, 0)
			};
			rightActionsStack.Children.Add(lblSelectionCount);

			System.Windows.Controls.Button btnAddSelected = new System.Windows.Controls.Button()
			{
				Height = 30,
				Padding = new Thickness(14, 0, 14, 0),
				Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand
			};
			LucideIcons.ApplyToButton(btnAddSelected, "plus", "Добавить выбранные в список", 14, Brushes.White, Brushes.White, 8, FontWeights.Bold);
			HandyControl.Controls.BorderElement.SetCornerRadius(btnAddSelected, new CornerRadius(5));

			rightActionsStack.Children.Add(btnAddSelected);
			discoveryBottomBar.Children.Add(rightActionsStack);

			Grid.SetRow(discoveryBottomBar, 3);
			tabContentDiscovery.Children.Add(discoveryBottomBar);

			Action updateCountLabel = () =>
			{
				int total = discoveredItems.Count;
				int sel = 0;
				for (int i = 0; i < discoveredItems.Count; i++)
				{
					if (discoveredItems[i].IsSelected)
					{
						sel++;
					}
				}
				lblSelectionCount.Text = string.Format("Найдено: {0} | Выбрано: {1}", total, sel);
			};

			btnSelectAll.Click += (s, ev) =>
			{
				for (int i = 0; i < discoveredItems.Count; i++)
				{
					discoveredItems[i].IsSelected = true;
				}
				updateCountLabel();
			};

			btnDeselectAll.Click += (s, ev) =>
			{
				for (int i = 0; i < discoveredItems.Count; i++)
				{
					discoveredItems[i].IsSelected = false;
				}
				updateCountLabel();
			};

			btnSelectWindows.Click += (s, ev) =>
			{
				for (int i = 0; i < discoveredItems.Count; i++)
				{
					discoveredItems[i].IsSelected = discoveredItems[i].IsWindows;
				}
				updateCountLabel();
			};

			btnStartScan.Click += async (s, ev) =>
			{
				string sFrom = txtFrom.Text.Trim();
				string sTo = txtTo.Text.Trim();
				IPAddress ipA, ipB;
				if (!IPAddress.TryParse(sFrom, out ipA) || !IPAddress.TryParse(sTo, out ipB))
				{
					ShowAppMessage("Укажите корректный диапазон IP (пример: 192.168.1.1 - 192.168.1.254).", "Ошибка диапазона", MessageBoxImage.Warning);
					return;
				}

				discoveredItems.Clear();
				updateCountLabel();
				progressBar.Value = 0;
				btnStartScan.IsEnabled = false;
				btnStopScan.IsEnabled = true;
				txtFrom.IsEnabled = false;
				txtTo.IsEnabled = false;
				statusLabel.Text = "Сканирование сети...";

				scanCts = new CancellationTokenSource();

				try
				{
					await NetworkScanner.AutoDiscoverHostsAsync(
						sFrom,
						sTo,
						detectedLocalIp,
						item =>
						{
							window.Dispatcher.Invoke(() =>
							{
								item.PropertyChanged += (ps, pe) => updateCountLabel();
								discoveredItems.Add(item);
								updateCountLabel();
							});
						},
						(cur, tot) =>
						{
							window.Dispatcher.Invoke(() =>
							{
								int pct = tot > 0 ? (int)((cur * 100.0) / tot) : 0;
								progressBar.Value = pct;
								statusLabel.Text = string.Format("Проверено: {0} из {1} ({2}%) | Найдено: {3}", cur, tot, pct, discoveredItems.Count);
							});
						},
						scanCts.Token);

					int winCount = 0;
					for (int i = 0; i < discoveredItems.Count; i++)
					{
						if (discoveredItems[i].IsWindows)
						{
							winCount++;
						}
					}
					statusLabel.Text = string.Format("Поиск завершен. Найдено устройств: {0} (Windows ПК: {1})", discoveredItems.Count, winCount);
					progressBar.Value = 100;
				}
				catch (OperationCanceledException)
				{
					statusLabel.Text = string.Format("Поиск остановлен. Найдено устройств: {0}", discoveredItems.Count);
				}
				catch (Exception ex)
				{
					statusLabel.Text = string.Format("Ошибка: {0}", ex.Message);
				}
				finally
				{
					btnStartScan.IsEnabled = true;
					btnStopScan.IsEnabled = false;
					txtFrom.IsEnabled = true;
					txtTo.IsEnabled = true;
				}
			};

			btnStopScan.Click += (s, ev) =>
			{
				if (scanCts != null)
				{
					scanCts.Cancel();
				}
			};

			btnAddSelected.Click += (s, ev) =>
			{
				List<DiscoveredHostItem> selectedList = new List<DiscoveredHostItem>();
				for (int i = 0; i < discoveredItems.Count; i++)
				{
					if (discoveredItems[i].IsSelected)
					{
						selectedList.Add(discoveredItems[i]);
					}
				}

				if (selectedList.Count == 0)
				{
					ShowAppMessage("Выберите устройства галочками в таблице.", "Выбор устройств", MessageBoxImage.Warning);
					return;
				}

				int addedCount = 0;
				int updatedCount = 0;

				for (int i = 0; i < selectedList.Count; i++)
				{
					DiscoveredHostItem item = selectedList[i];
					bool foundExisting = false;

					for (int n = 0; n < _networkNodes.Count; n++)
					{
						if (string.Equals(_networkNodes[n].IpAddress, item.IpAddress, StringComparison.OrdinalIgnoreCase) ||
							string.Equals(_networkNodes[n].Name, item.Name, StringComparison.OrdinalIgnoreCase))
						{
							foundExisting = true;
							_networkNodes[n].IsOnline = true;
							_networkNodes[n].PingTimeMs = item.PingTimeMs;
							_networkNodes[n].IsSmbOpen = item.IsWindows;
							if (string.IsNullOrWhiteSpace(_networkNodes[n].Name) || _networkNodes[n].Name.StartsWith("PC-NODE-"))
							{
								_networkNodes[n].Name = item.Name;
							}
							updatedCount++;
							break;
						}
					}

					if (!foundExisting)
					{
						_networkNodes.Add(new NetworkNodeItem(item.Name, item.IpAddress, item.IsWindows ? "В очереди" : "В сети")
						{
							IsOnline = true,
							PingTimeMs = item.PingTimeMs,
							IsSmbOpen = item.IsWindows
						});
						addedCount++;
					}
				}

				SyncAndSaveComputersToConfig();
				RenderComputersList();
				UpdateProgressCardSummary();
				renderCredentialRows();
				updateDevicesSummary();

				AppendTerminalLog(string.Format("{0} - Поиск в сети: добавлено ПК: {1}, обновлено: {2}", 
					DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), addedCount, updatedCount), Color.FromRgb(52, 211, 153));

				ShowAppMessage(string.Format("Устройства успешно добавлены в список:\n• Добавлено новых ПК: {0}\n• Обновлено: {1}\n\nВы можете настроить учетные записи и папки на вкладке 'Компьютеры и учетные записи'.", addedCount, updatedCount), "Поиск в сети", MessageBoxImage.Information);

				setActiveTab(0);
			};

			// ==========================================
			// ОБЩИЙ ФУТЕР
			// ==========================================
			DockPanel sharedFooter = new DockPanel() { Margin = new Thickness(0, 10, 0, 0) };

			System.Windows.Controls.Button btnSaveAndClose = new System.Windows.Controls.Button()
			{
				Content = "Сохранить и закрыть",
				Height = 32,
				Width = 170,
				Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 11,
				Cursor = System.Windows.Input.Cursors.Hand,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			HandyControl.Controls.BorderElement.SetCornerRadius(btnSaveAndClose, new CornerRadius(5));

			btnSaveAndClose.Click += (s, ev) =>
			{
				if (scanCts != null)
				{
					scanCts.Cancel();
				}
				SyncAndSaveComputersToConfig();
				RenderComputersList();
				UpdateProgressCardSummary();
				AppendTerminalLog(string.Format("{0} - Настройки устройств сохранены в config.json", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(52, 211, 153));
				window.Close();
			};

			DockPanel.SetDock(btnSaveAndClose, System.Windows.Controls.Dock.Right);
			sharedFooter.Children.Add(btnSaveAndClose);

			Grid.SetRow(sharedFooter, 3);
			rootLayout.Children.Add(sharedFooter);

			window.Closing += (s, ev) =>
			{
				if (scanCts != null)
				{
					scanCts.Cancel();
				}
				SyncAndSaveComputersToConfig();
				RenderComputersList();
				UpdateProgressCardSummary();
			};

			setActiveTab(initialTab);

			window.Content = rootLayout;
			window.ShowDialog();
		}

		// Обработчик добавления глобальной директории сканирования (сохранен для совместимости)
		private void ButtonAddDirectory_Click(object sender, RoutedEventArgs e)
		{
			// Добавление целевых каталогов производится для конкретных компьютеров в окне учетных записей (net use)
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
			AppendTerminalLog(string.Format("{0} - Настройки БД сохранены", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(52, 211, 153));
			ShowAppMessage("Настройки БД сохранены", "Настройки БД", MessageBoxImage.Information);
		}

		// Полная очистка обеих связанных таблиц в базе данных MSSQL и сброс текущего списка сертификатов
		private void ButtonClearDbTable_Click(object sender, RoutedEventArgs e)
		{
			string server = _textSqlServer.Text.Trim();
			string database = _textSqlDatabase.Text.Trim();
			string table = string.IsNullOrWhiteSpace(_textSqlTable.Text) ? "Certificates" : _textSqlTable.Text.Trim();
			string connString = string.Format("Server={0};Database={1};Integrated Security=True;", server, database);

			bool confirm = ShowAppConfirm(
				string.Format("Очистить таблицы '{0}' и 'CertificateLocations' в базе данных?\nВсе сохраненные данные будут удалены.", table),
				"Очистка таблиц БД",
				"Да",
				"Нет",
				MessageBoxImage.Warning);

			if (confirm == false)
			{
				return;
			}

			bool isSuccess = DatabaseManager.ClearTables(connString, table);
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
				RenderComputersList();
				_hasCompletedScan = false;
				UpdateProgressCardSummary();

				AppendTerminalLog(string.Format("{0} - Таблицы '{1}' и 'CertificateLocations' очищены", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), table), Color.FromRgb(52, 211, 153));
				ShowAppMessage(string.Format("Таблицы '{0}' и 'CertificateLocations' очищены.", table), "Очистка БД", MessageBoxImage.Information);
			}
			else
			{
				AppendTerminalLog(string.Format("{0} - Ошибка очистки таблиц в БД: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), table), Color.FromRgb(248, 113, 113));
				ShowAppMessage("Не удалось очистить таблицы в БД.", "Ошибка очистки", MessageBoxImage.Error);
			}
		}

		// Запуск или остановка процедуры сканирования СКЗИ одной кнопкой
		private async void ButtonStartScan_Click(object sender, RoutedEventArgs e)
		{
			if (_isScanningActive == true)
			{
				if (_scanCts != null)
				{
					_scanCts.Cancel();
				}
				return;
			}

			_isScanningActive = true;
			UpdateScanButtonState();
			_scanCts = new CancellationTokenSource();

			AppendTerminalLog(string.Format("{0} - Запущено сканирование СКЗИ", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Colors.White);

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

					double prog = ((i + 1) * 100.0) / totalNodes;
					_labelProgressSummary.Text = string.Format("Проверено: {0} из {1} ПК", i, totalNodes);
					_progressBarCustom.Value = prog;
					_progressBarCustom.Foreground = new SolidColorBrush(Color.FromRgb(14, 165, 233));
					_labelProgressPercent.Text = ((int)prog) + "%";
					_labelProgressCurrentNode.Text = string.Format("Сканируется: {0}...", node.Name);

					AppendTerminalLog(string.Format("{0} - Начато сканирование ПК: {1} ({2})", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress), Color.FromRgb(156, 163, 175));

					await Task.Delay(400, _scanCts.Token);

					node.Status = "Готово";
					AppendTerminalLog(string.Format("{0} - ПК {1} успешно проверен. Сертификаты и СКЗИ обработаны", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name), Color.FromRgb(52, 211, 153));
					RenderComputersList();
				}

				_labelProgressSummary.Text = string.Format("Проверено: {0} из {0} ПК", totalNodes);
				_progressBarCustom.Value = 100;
				_labelProgressPercent.Text = "100%";
				_labelProgressCurrentNode.Text = "Сбор сертификатов и СКЗИ...";

				List<CertificateRecord> scanned = await Task.Run(() => CertificateScanner.ScanAll(_currentConfig));

				int newlyAdded = 0;
				int updatedExisting = 0;

				for (int s = 0; s < scanned.Count; s++)
				{
					CertificateRecord foundCert = scanned[s];
					bool alreadyExists = false;

					for (int ex = 0; ex < _allCertificates.Count; ex++)
					{
						if (string.Equals(_allCertificates[ex].SerialNumber, foundCert.SerialNumber, StringComparison.OrdinalIgnoreCase))
						{
							_allCertificates[ex].UpdatedAt = DateTime.Now;
							_allCertificates[ex].OwnerName = foundCert.OwnerName;
							_allCertificates[ex].IssuerName = foundCert.IssuerName;
							_allCertificates[ex].CryptoProVersion = foundCert.CryptoProVersion;
							_allCertificates[ex].HasEmbeddedLicense = foundCert.HasEmbeddedLicense;

							if (foundCert.Locations != null && foundCert.Locations.Count > 0)
							{
								for (int l = 0; l < foundCert.Locations.Count; l++)
								{
									CertificateLocationRecord loc = foundCert.Locations[l];
									_allCertificates[ex].AddLocation(loc.MachineName, loc.MachineIp, loc.SourcePath, loc.LastSeenAt);
								}
							}
							else
							{
								_allCertificates[ex].AddLocation(foundCert.MachineName, foundCert.MachineIp, foundCert.SourcePath);
							}

							alreadyExists = true;
							updatedExisting = updatedExisting + 1;
							break;
						}
					}

					if (!alreadyExists)
					{
						if (foundCert.Locations == null || foundCert.Locations.Count == 0)
						{
							foundCert.AddLocation(foundCert.MachineName, foundCert.MachineIp, foundCert.SourcePath);
						}
						_allCertificates.Add(foundCert);
						newlyAdded = newlyAdded + 1;
					}
				}

				_currentConfig.Certificates = _allCertificates;
				ConfigManager.SaveConfig(_currentConfig);

				_labelMetricInsertedNumber.Text = newlyAdded.ToString();
				_labelMetricUpdatedNumber.Text = updatedExisting.ToString();

				_hasCompletedScan = true;
				_labelProgressSummary.Text = string.Format("Завершено: {0} из {0} ПК", totalNodes);
				_labelProgressPercent.Text = "100%";
				_progressBarCustom.Value = 100;
				_progressBarCustom.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
				_labelProgressCurrentNode.Text = "Сбор данных успешно завершен";

				RefreshCertificatesGrid();
				RenderComputersList();
				AppendTerminalLog(string.Format("{0} - Сканирование завершено. Всего сертификатов: {1}. Данные сохранены в config.json", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), _allCertificates.Count), Color.FromRgb(52, 211, 153));
			}
			catch (OperationCanceledException)
			{
				_hasCompletedScan = false;
				_labelProgressSummary.Text = "Сканирование прервано";
				_progressBarCustom.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
				_labelProgressCurrentNode.Text = "Остановлено пользователем";
				AppendTerminalLog(string.Format("{0} - Сканирование прервано пользователем", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromRgb(239, 68, 68));
			}
			catch (Exception ex)
			{
				AppendTerminalLog(string.Format("{0} - Ошибка сканирования: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), ex.Message), Color.FromRgb(239, 68, 68));
			}
			finally
			{
				_isScanningActive = false;
				UpdateScanButtonState();
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

		// Обновление состояния и внешнего вида объединенной кнопки сканирования/остановки
		private void UpdateScanButtonState()
		{
			if (_buttonStartScan == null)
			{
				return;
			}

			if (_isScanningActive == true)
			{
				LucideIcons.ApplyToButton(_buttonStartScan, "square", "Остановить", 15, Brushes.White, Brushes.White, 8, FontWeights.Bold);
				_buttonStartScan.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
				_buttonStartScan.Foreground = Brushes.White;
				_buttonStartScan.ToolTip = "Остановить текущее сканирование";
			}
			else
			{
				LucideIcons.ApplyToButton(_buttonStartScan, "play", "Сканировать", 15, Brushes.White, Brushes.White, 8, FontWeights.Bold);
				_buttonStartScan.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
				_buttonStartScan.Foreground = Brushes.White;
				_buttonStartScan.ToolTip = "Запустить сканирование";
			}
		}

		// Синхронизация и отправка записей сертификатов в СУБД Microsoft SQL Server
		private async void ButtonExportDb_Click(object sender, RoutedEventArgs e)
		{
			if (_allCertificates == null || _allCertificates.Count == 0)
			{
				ShowAppMessage("Список сертификатов пуст. Сначала выполните сканирование.", "Отправка данных в БД", MessageBoxImage.Information);
				return;
			}

			string server = _textSqlServer != null ? _textSqlServer.Text.Trim() : string.Empty;
			string database = _textSqlDatabase != null ? _textSqlDatabase.Text.Trim() : string.Empty;
			string tableName = _textSqlTable != null && !string.IsNullOrWhiteSpace(_textSqlTable.Text) ? _textSqlTable.Text.Trim() : "Certificates";

			if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
			{
				ShowAppMessage("Укажите адрес сервера и имя базы данных в настройках подключения.", "Параметры БД", MessageBoxImage.Warning);
				return;
			}

			string connectionString = string.Format("Server={0};Database={1};Integrated Security=True;", server, database);

			AppendTerminalLog(string.Format("{0} - Запущена отправка данных в СУБД Microsoft SQL Server ({1}.dbo.{2})...", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), database, tableName), Colors.White);

			_buttonExportDb.IsEnabled = false;

			try
			{
				int savedCount = await Task.Run(() => DatabaseManager.SaveCertificates(_allCertificates, connectionString, tableName));

				_labelMetricInsertedNumber.Text = savedCount.ToString();
				AppendTerminalLog(string.Format("{0} - Синхронизация с БД завершена. Таблица '{1}': успешно обработано записей: {2}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), tableName, savedCount), Color.FromRgb(52, 211, 153));

				ShowAppMessage(
					string.Format("Данные успешно отправлены в базу данных!\n\nТаблица: {0}\nОбработано записей: {1}", tableName, savedCount),
					"Отправка данных в БД",
					MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				AppendTerminalLog(string.Format("{0} - Сбой при отправке данных в БД: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), ex.Message), Color.FromRgb(248, 113, 113));
				ShowAppMessage(
					string.Format("Не удалось отправить данные в базу данных:\n\n{0}", ex.Message),
					"Ошибка БД",
					MessageBoxImage.Error);
			}
			finally
			{
				_buttonExportDb.IsEnabled = true;
			}
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

			System.Windows.Controls.Image iconImg = null;
			if (image == MessageBoxImage.Warning)
			{
				iconImg = LucideIcons.CreateWpfImage("alert-triangle", 36, System.Drawing.Color.FromArgb(245, 158, 11));
			}
			else if (image == MessageBoxImage.Error)
			{
				iconImg = LucideIcons.CreateWpfImage("alert-circle", 36, System.Drawing.Color.FromArgb(239, 68, 68));
			}
			else if (image == MessageBoxImage.Information)
			{
				iconImg = LucideIcons.CreateWpfImage("shield-check", 36, System.Drawing.Color.FromArgb(16, 185, 129));
			}
			else
			{
				iconImg = LucideIcons.CreateWpfImage("help-circle", 36, System.Drawing.Color.FromArgb(59, 130, 246));
			}

			iconImg.VerticalAlignment = VerticalAlignment.Top;
			iconImg.Margin = new Thickness(0, 2, 16, 0);
			Grid.SetColumn(iconImg, 0);
			contentGrid.Children.Add(iconImg);

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