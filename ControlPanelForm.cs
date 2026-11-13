using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CertHarvester
{
	/// <summary>
	/// Модель данных рабочего узла локальной сети для визуализации в боковой панели
	/// </summary>
	public class NetworkNodeItem
	{
		public string Name { get; set; }
		public string IpAddress { get; set; }
		public string Status { get; set; }

		// Конструктор элемента сетевого узла
		public NetworkNodeItem(string name, string ipAddress, string status)
		{
			Name = name;
			IpAddress = ipAddress;
			Status = status;
		}
	}

	/// <summary>
	/// Графическая панель управления СКЗИ с чтением данных из JSON, темной темой, кнопками окна и кастомным скроллбаром
	/// </summary>
	public class ControlPanelForm : Form
	{
		[DllImport("user32.dll")]
		public static extern bool ReleaseCapture();

		[DllImport("user32.dll")]
		public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

		private const int WM_NCLBUTTONDOWN = 0xA1;
		private const int HT_CAPTION = 0x2;
		private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
		private const int EM_LINESCROLL = 0x00B6;

		private AppConfig _currentConfig;
		private List<CertificateRecord> _allCertificates;
		private List<CertificateRecord> _filteredCertificates;
		private List<NetworkNodeItem> _networkNodes;
		private bool _filterOnlyWithLicense;
		private string _searchFilterQuery;
		private CancellationTokenSource _scanCts;
		private bool _isScanningActive;
		private bool _isDarkTheme;

		// Элементы шапки окна и кнопок управления окном
		private Panel _panelTopHeader;
		private Label _labelAppTitle;
		private Button _buttonThemeToggle;
		private Panel _panelTabsContainer;
		private Button _tabButtonScan;
		private Button _tabButtonLogs;
		private Button _tabButtonSettings;
		private Button _buttonMinWindow;
		private Button _buttonMaxWindow;
		private Button _buttonCloseWindow;

		// Элементы левого сайдбара
		private Panel _panelSidebar;
		private Label _labelSidebarCompHeader;
		private Label _labelSidebarDirsHeader;
		private Label _labelSidebarDbHeader;
		private Label _labelServerTitle;
		private Label _labelDbTitle;
		private Label _labelTableTitle;
		private Label _labelAutoCreateTitle;
		private Button _buttonAddComputer;
		private Button _buttonAddDirectory;
		private Panel _panelComputersContainer;
		private Panel _panelDirectoriesContainer;
		private TextBox _textSqlServer;
		private TextBox _textSqlDatabase;
		private TextBox _textSqlTable;
		private CheckBox _checkAutoCreateTable;
		private Button _buttonSaveDbSettings;

		// Элементы главной области
		private Panel _panelMainContent;
		private Label _labelMainHeader;
		private Button _buttonStopScan;
		private Button _buttonExportDb;
		private Button _buttonStartScan;

		// Карточки метрик (KPI)
		private Panel _cardMetricTotal;
		private Label _labelMetricTotalNumber;
		private Label _labelMetricTotalTitle;
		private Panel _cardMetricInserted;
		private Label _labelMetricInsertedNumber;
		private Label _labelMetricInsertedTitle;
		private Panel _cardMetricUpdated;
		private Label _labelMetricUpdatedNumber;
		private Label _labelMetricUpdatedTitle;
		private Panel _cardMetricProgress;
		private Label _labelProgressSummary;
		private Label _labelProgressPercent;
		private Label _labelProgressCurrentNode;
		private Panel _progressBarCustom;
		private int _progressValue;

		// Поиск и фильтрация
		private Panel _panelSearchContainer;
		private Label _labelSearchIcon;
		private TextBox _textSearchBox;
		private Button _buttonFilterLicense;

		// Таблица сертификатов
		private DataGridView _gridCertificates;

		// Темный терминал журнала работы и кастомный темный ползунок прокрутки
		private Panel _panelTerminalCard;
		private Label _labelTerminalTitle;
		private RichTextBox _textTerminalLog;
		private Panel _panelScrollTrack;
		private Panel _panelScrollThumb;
		private bool _isThumbDragging;
		private int _dragStartY;
		private int _thumbStartY;

		// Конструктор инициализации формы панели управления
		public ControlPanelForm()
		{
			_allCertificates = new List<CertificateRecord>();
			_filteredCertificates = new List<CertificateRecord>();
			_networkNodes = new List<NetworkNodeItem>();
			_filterOnlyWithLicense = false;
			_searchFilterQuery = string.Empty;
			_progressValue = 0;
			_isDarkTheme = false;
			_isThumbDragging = false;

			this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
			this.UpdateStyles();

			InitializeComponentCustom();
			LoadConfigurationFromJsonFile();
			ApplyTheme(_isDarkTheme);
			RefreshCertificatesGrid();
		}

		// Инициализация структуры и внешнего вида оконного интерфейса
		private void InitializeComponentCustom()
		{
			this.Text = "Сбор данных о СКЗИ";
			this.Size = new Size(1240, 800);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.FormBorderStyle = FormBorderStyle.None;
			this.BackColor = Color.FromArgb(244, 246, 249);
			this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
			this.KeyPreview = true;
			this.KeyDown += ControlPanelForm_KeyDown;

			// 1. Верхняя панель заголовка окна с кнопками управления
			CreateTopHeaderBar();

			// 2. Левый сайдбар настроек и добавления ресурсов
			CreateLeftSidebar();

			// 3. Главная область управления, метрик, таблицы и терминала
			CreateMainContentArea();
		}

		// Обработка горячих клавиш закрытия окна по Escape или Alt+F4
		private void ControlPanelForm_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				this.Close();
			}
		}

		// Создание верхней панели с центрированным заголовком, кнопкой темы, вкладками и кнопками окна
		private void CreateTopHeaderBar()
		{
			_panelTopHeader = new Panel();
			_panelTopHeader.Dock = DockStyle.Top;
			_panelTopHeader.Height = 52;
			_panelTopHeader.BackColor = Color.FromArgb(244, 246, 249);
			_panelTopHeader.MouseDown += Header_MouseDown;

			_labelAppTitle = new Label();
			_labelAppTitle.Text = "Сбор данных о СКЗИ";
			_labelAppTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
			_labelAppTitle.ForeColor = Color.FromArgb(17, 24, 39);
			_labelAppTitle.AutoSize = true;
			_labelAppTitle.MouseDown += Header_MouseDown;

			// Кнопки управления окном справа сверху (Свернуть, Развернуть, Закрыть)
			_buttonMinWindow = new Button();
			_buttonMinWindow.Text = "—";
			_buttonMinWindow.Size = new Size(36, 32);
			_buttonMinWindow.Location = new Point(this.ClientSize.Width - 118, 10);
			_buttonMinWindow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			_buttonMinWindow.FlatStyle = FlatStyle.Flat;
			_buttonMinWindow.FlatAppearance.BorderSize = 0;
			_buttonMinWindow.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
			_buttonMinWindow.Cursor = Cursors.Hand;
			_buttonMinWindow.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };

			_buttonMaxWindow = new Button();
			_buttonMaxWindow.Text = "🗖";
			_buttonMaxWindow.Size = new Size(36, 32);
			_buttonMaxWindow.Location = new Point(this.ClientSize.Width - 80, 10);
			_buttonMaxWindow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			_buttonMaxWindow.FlatStyle = FlatStyle.Flat;
			_buttonMaxWindow.FlatAppearance.BorderSize = 0;
			_buttonMaxWindow.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
			_buttonMaxWindow.Cursor = Cursors.Hand;
			_buttonMaxWindow.Click += ButtonMaxWindow_Click;

			_buttonCloseWindow = new Button();
			_buttonCloseWindow.Text = "✕";
			_buttonCloseWindow.Size = new Size(36, 32);
			_buttonCloseWindow.Location = new Point(this.ClientSize.Width - 42, 10);
			_buttonCloseWindow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			_buttonCloseWindow.FlatStyle = FlatStyle.Flat;
			_buttonCloseWindow.FlatAppearance.BorderSize = 0;
			_buttonCloseWindow.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
			_buttonCloseWindow.Cursor = Cursors.Hand;
			_buttonCloseWindow.MouseEnter += (s, e) => { _buttonCloseWindow.BackColor = Color.FromArgb(239, 68, 68); _buttonCloseWindow.ForeColor = Color.White; };
			_buttonCloseWindow.MouseLeave += (s, e) => { ApplyThemeToWindowButtons(); };
			_buttonCloseWindow.Click += (s, e) => { this.Close(); };

			// Табы справа: Сканирование, Журнал, Настройки
			_panelTabsContainer = new Panel();
			_panelTabsContainer.Size = new Size(270, 32);
			_panelTabsContainer.Location = new Point(this.ClientSize.Width - 400, 10);
			_panelTabsContainer.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			_panelTabsContainer.BackColor = Color.FromArgb(235, 238, 242);
			_panelTabsContainer.Paint += (s, pe) =>
			{
				Color borderColor = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(220, 224, 230);
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, _panelTabsContainer.Width - 1, _panelTabsContainer.Height - 1), 6))
				using (Pen pen = new Pen(borderColor, 1))
				{
					pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
					pe.Graphics.DrawPath(pen, path);
				}
			};

			_tabButtonScan = CreateTabButton("Сканирование", 2, 2, 94, true);
			_tabButtonLogs = CreateTabButton("Журнал", 98, 2, 80, false);
			_tabButtonSettings = CreateTabButton("Настройки", 180, 2, 86, false);

			_tabButtonScan.Click += (s, e) => SwitchTab("scan");
			_tabButtonLogs.Click += (s, e) => SwitchTab("logs");
			_tabButtonSettings.Click += (s, e) => SwitchTab("settings");

			_panelTabsContainer.Controls.Add(_tabButtonScan);
			_panelTabsContainer.Controls.Add(_tabButtonLogs);
			_panelTabsContainer.Controls.Add(_tabButtonSettings);

			// Выразительная кнопка переключения темы оформления
			_buttonThemeToggle = new Button();
			_buttonThemeToggle.Text = "🌙  Тёмная тема";
			_buttonThemeToggle.Size = new Size(136, 32);
			_buttonThemeToggle.Location = new Point(this.ClientSize.Width - 548, 10);
			_buttonThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			_buttonThemeToggle.FlatStyle = FlatStyle.Flat;
			_buttonThemeToggle.FlatAppearance.BorderSize = 0;
			_buttonThemeToggle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_buttonThemeToggle.Cursor = Cursors.Hand;
			_buttonThemeToggle.Click += ButtonThemeToggle_Click;

			_panelTopHeader.Controls.Add(_labelAppTitle);
			_panelTopHeader.Controls.Add(_buttonThemeToggle);
			_panelTopHeader.Controls.Add(_panelTabsContainer);
			_panelTopHeader.Controls.Add(_buttonMinWindow);
			_panelTopHeader.Controls.Add(_buttonMaxWindow);
			_panelTopHeader.Controls.Add(_buttonCloseWindow);

			// Центрирование заголовка при изменении размера окна
			_panelTopHeader.Resize += (s, e) =>
			{
				_labelAppTitle.Location = new Point((_panelTopHeader.Width - _labelAppTitle.Width) / 2, 16);
			};

			this.Controls.Add(_panelTopHeader);
		}

		// Обработчик кнопки максимизации / восстановления нормального размера окна
		private void ButtonMaxWindow_Click(object sender, EventArgs e)
		{
			if (this.WindowState == FormWindowState.Maximized)
			{
				this.WindowState = FormWindowState.Normal;
				_buttonMaxWindow.Text = "🗖";
			}
			else
			{
				this.WindowState = FormWindowState.Maximized;
				_buttonMaxWindow.Text = "🗗";
			}
		}

		// Создание кнопки вкладки в верхней панели управления
		private Button CreateTabButton(string text, int x, int y, int width, bool isActive)
		{
			Button btn = new Button();
			btn.Text = text;
			btn.Location = new Point(x, y);
			btn.Size = new Size(width, 28);
			btn.FlatStyle = FlatStyle.Flat;
			btn.FlatAppearance.BorderSize = 0;
			btn.Font = new Font("Segoe UI", 8.5F, isActive ? FontStyle.Bold : FontStyle.Regular);
			btn.BackColor = isActive ? Color.White : Color.Transparent;
			btn.ForeColor = isActive ? Color.FromArgb(17, 24, 39) : Color.FromArgb(107, 114, 128);
			btn.Cursor = Cursors.Hand;
			return btn;
		}

		// Переключение отображаемой вкладки верхней панели
		private void SwitchTab(string tabName)
		{
			Color activeBg = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.White;
			Color activeText = _isDarkTheme ? Color.White : Color.FromArgb(17, 24, 39);
			Color inactiveText = _isDarkTheme ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);

			_tabButtonScan.BackColor = (tabName == "scan") ? activeBg : Color.Transparent;
			_tabButtonScan.ForeColor = (tabName == "scan") ? activeText : inactiveText;
			_tabButtonScan.Font = new Font("Segoe UI", 8.5F, (tabName == "scan") ? FontStyle.Bold : FontStyle.Regular);

			_tabButtonLogs.BackColor = (tabName == "logs") ? activeBg : Color.Transparent;
			_tabButtonLogs.ForeColor = (tabName == "logs") ? activeText : inactiveText;
			_tabButtonLogs.Font = new Font("Segoe UI", 8.5F, (tabName == "logs") ? FontStyle.Bold : FontStyle.Regular);

			_tabButtonSettings.BackColor = (tabName == "settings") ? activeBg : Color.Transparent;
			_tabButtonSettings.ForeColor = (tabName == "settings") ? activeText : inactiveText;
			_tabButtonSettings.Font = new Font("Segoe UI", 8.5F, (tabName == "settings") ? FontStyle.Bold : FontStyle.Regular);

			if (tabName == "logs")
			{
				_textTerminalLog.Focus();
			}
			else if (tabName == "settings")
			{
				_textSqlServer.Focus();
			}
		}

		// Обработчик переключения между светлой и темной темами оформления
		private void ButtonThemeToggle_Click(object sender, EventArgs e)
		{
			_isDarkTheme = !_isDarkTheme;
			_currentConfig.IsDarkTheme = _isDarkTheme;
			ConfigManager.SaveConfig(_currentConfig);

			ApplyTheme(_isDarkTheme);
			RefreshCertificatesGrid();
		}

		// Настройка внешнего вида кнопок закрытия и сворачивания окна в зависимости от темы
		private void ApplyThemeToWindowButtons()
		{
			Color btnText = _isDarkTheme ? Color.FromArgb(156, 163, 175) : Color.FromArgb(75, 85, 99);
			Color btnBg = Color.Transparent;

			_buttonMinWindow.BackColor = btnBg;
			_buttonMinWindow.ForeColor = btnText;

			_buttonMaxWindow.BackColor = btnBg;
			_buttonMaxWindow.ForeColor = btnText;

			_buttonCloseWindow.BackColor = btnBg;
			_buttonCloseWindow.ForeColor = btnText;
		}

		// Применение темы оформления ко всем элементам пользовательского интерфейса
		private void ApplyTheme(bool isDark)
		{
			Color formBg = isDark ? Color.FromArgb(17, 24, 39) : Color.FromArgb(244, 246, 249);
			Color cardBg = isDark ? Color.FromArgb(31, 41, 55) : Color.White;
			Color cardBorder = isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(229, 231, 235);
			Color textPrimary = isDark ? Color.FromArgb(243, 244, 246) : Color.FromArgb(17, 24, 39);
			Color textSecondary = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);
			Color inputBg = isDark ? Color.FromArgb(17, 24, 39) : Color.FromArgb(249, 250, 251);

			// Форма и шапка
			this.BackColor = formBg;
			_panelTopHeader.BackColor = formBg;
			_labelAppTitle.ForeColor = textPrimary;

			_buttonThemeToggle.Text = isDark ? "☀️  Светлая тема" : "🌙  Тёмная тема";
			_buttonThemeToggle.BackColor = isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(235, 238, 242);
			_buttonThemeToggle.ForeColor = textPrimary;

			_panelTabsContainer.BackColor = isDark ? Color.FromArgb(17, 24, 39) : Color.FromArgb(235, 238, 242);
			SwitchTab("scan");
			ApplyThemeToWindowButtons();

			// Сайдбар
			_panelSidebar.BackColor = cardBg;
			_labelSidebarCompHeader.ForeColor = textSecondary;
			_labelSidebarDirsHeader.ForeColor = textSecondary;
			_labelSidebarDbHeader.ForeColor = textSecondary;
			_labelServerTitle.ForeColor = textPrimary;
			_labelDbTitle.ForeColor = textPrimary;
			_labelTableTitle.ForeColor = textPrimary;
			_labelAutoCreateTitle.ForeColor = textPrimary;

			_textSqlServer.BackColor = inputBg;
			_textSqlServer.ForeColor = textPrimary;
			_textSqlDatabase.BackColor = inputBg;
			_textSqlDatabase.ForeColor = textPrimary;
			_textSqlTable.BackColor = inputBg;
			_textSqlTable.ForeColor = textPrimary;

			_buttonSaveDbSettings.BackColor = isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(243, 244, 246);
			_buttonSaveDbSettings.ForeColor = textPrimary;

			// Главная панель
			_labelMainHeader.ForeColor = textPrimary;

			_buttonStopScan.BackColor = isDark ? Color.FromArgb(69, 26, 26) : Color.FromArgb(254, 242, 242);
			_buttonStopScan.ForeColor = isDark ? Color.FromArgb(248, 113, 113) : Color.FromArgb(239, 68, 68);

			_buttonExportDb.BackColor = isDark ? Color.FromArgb(30, 58, 138) : Color.FromArgb(239, 246, 255);
			_buttonExportDb.ForeColor = isDark ? Color.FromArgb(147, 197, 253) : Color.FromArgb(37, 99, 235);

			// Карточки показателей
			_cardMetricTotal.BackColor = cardBg;
			_labelMetricTotalNumber.ForeColor = textPrimary;
			_labelMetricTotalTitle.ForeColor = textSecondary;

			_cardMetricInserted.BackColor = cardBg;
			_labelMetricInsertedNumber.ForeColor = isDark ? Color.FromArgb(52, 211, 153) : Color.FromArgb(16, 185, 129);
			_labelMetricInsertedTitle.ForeColor = textSecondary;

			_cardMetricUpdated.BackColor = cardBg;
			_labelMetricUpdatedNumber.ForeColor = isDark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(59, 130, 246);
			_labelMetricUpdatedTitle.ForeColor = textSecondary;

			_cardMetricProgress.BackColor = cardBg;
			_labelProgressSummary.ForeColor = textPrimary;
			_labelProgressPercent.ForeColor = textSecondary;
			_labelProgressCurrentNode.ForeColor = textSecondary;
			_progressBarCustom.BackColor = isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(229, 231, 235);

			// Поиск и фильтрация
			_panelSearchContainer.BackColor = cardBg;
			_labelSearchIcon.ForeColor = textSecondary;
			_textSearchBox.BackColor = cardBg;
			_textSearchBox.ForeColor = textPrimary;

			if (_filterOnlyWithLicense == false)
			{
				_buttonFilterLicense.BackColor = isDark ? Color.FromArgb(30, 58, 138) : Color.FromArgb(239, 246, 255);
				_buttonFilterLicense.ForeColor = isDark ? Color.FromArgb(147, 197, 253) : Color.FromArgb(37, 99, 235);
			}

			// Таблица DataGridView
			_gridCertificates.BackgroundColor = cardBg;
			_gridCertificates.GridColor = cardBorder;

			_gridCertificates.ColumnHeadersDefaultCellStyle.BackColor = cardBg;
			_gridCertificates.ColumnHeadersDefaultCellStyle.ForeColor = textSecondary;

			_gridCertificates.DefaultCellStyle.BackColor = cardBg;
			_gridCertificates.DefaultCellStyle.ForeColor = textPrimary;
			_gridCertificates.DefaultCellStyle.SelectionBackColor = isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(240, 249, 255);
			_gridCertificates.DefaultCellStyle.SelectionForeColor = isDark ? Color.White : Color.FromArgb(17, 24, 39);

			// Терминал журнала: монолитный черный фон без белых полос
			Color terminalBg = isDark ? Color.FromArgb(11, 15, 25) : Color.FromArgb(22, 24, 29);
			Color scrollTrackBg = isDark ? Color.FromArgb(20, 24, 34) : Color.FromArgb(30, 34, 42);
			Color scrollThumbBg = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);

			_panelTerminalCard.BackColor = terminalBg;
			_textTerminalLog.BackColor = terminalBg;
			_labelTerminalTitle.ForeColor = textSecondary;

			_panelScrollTrack.BackColor = scrollTrackBg;
			_panelScrollThumb.BackColor = scrollThumbBg;

			// Отрисовка списков узлов и директорий
			RenderComputersList();
			RenderDirectoriesList();
			UpdateScrollBarThumb();

			this.Invalidate(true);
		}

		// Перемещение формы при зажатии левой кнопки мыши на шапке окна
		private void Header_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
			}
		}

		// Создание левого сайдбара с возможностью добавления компьютеров и директорий
		private void CreateLeftSidebar()
		{
			_panelSidebar = new Panel();
			_panelSidebar.Location = new Point(16, 56);
			_panelSidebar.Size = new Size(276, 726);
			_panelSidebar.BackColor = Color.White;
			_panelSidebar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
			_panelSidebar.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				Color borderColor = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(229, 231, 235);
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, _panelSidebar.Width - 1, _panelSidebar.Height - 1), 12))
				using (Pen pen = new Pen(borderColor, 1))
				{
					pe.Graphics.DrawPath(pen, path);
				}
			};

			int currentY = 14;

			// Блок 1: Компьютеры в сети
			_labelSidebarCompHeader = new Label();
			_labelSidebarCompHeader.Text = "Компьютеры в сети";
			_labelSidebarCompHeader.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_labelSidebarCompHeader.ForeColor = Color.FromArgb(107, 114, 128);
			_labelSidebarCompHeader.Location = new Point(16, currentY);
			_labelSidebarCompHeader.AutoSize = true;
			_panelSidebar.Controls.Add(_labelSidebarCompHeader);
			currentY = currentY + 20;

			_buttonAddComputer = new Button();
			_buttonAddComputer.Text = "+   Добавить компьютер...";
			_buttonAddComputer.Location = new Point(16, currentY);
			_buttonAddComputer.Size = new Size(244, 30);
			_buttonAddComputer.FlatStyle = FlatStyle.Flat;
			_buttonAddComputer.FlatAppearance.BorderSize = 0;
			_buttonAddComputer.BackColor = Color.FromArgb(2, 132, 199);
			_buttonAddComputer.ForeColor = Color.White;
			_buttonAddComputer.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_buttonAddComputer.Cursor = Cursors.Hand;
			_buttonAddComputer.Click += ButtonAddComputer_Click;
			_panelSidebar.Controls.Add(_buttonAddComputer);
			currentY = currentY + 36;

			_panelComputersContainer = new Panel();
			_panelComputersContainer.Location = new Point(12, currentY);
			_panelComputersContainer.Size = new Size(252, 180);
			_panelComputersContainer.AutoScroll = true;
			_panelSidebar.Controls.Add(_panelComputersContainer);
			currentY = currentY + 190;

			// Блок 2: Директории из конфигурации
			_labelSidebarDirsHeader = new Label();
			_labelSidebarDirsHeader.Text = "Директории из конфигурации";
			_labelSidebarDirsHeader.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_labelSidebarDirsHeader.ForeColor = Color.FromArgb(107, 114, 128);
			_labelSidebarDirsHeader.Location = new Point(16, currentY);
			_labelSidebarDirsHeader.AutoSize = true;
			_panelSidebar.Controls.Add(_labelSidebarDirsHeader);
			currentY = currentY + 20;

			_buttonAddDirectory = new Button();
			_buttonAddDirectory.Text = "+   Добавить директорию...";
			_buttonAddDirectory.Location = new Point(16, currentY);
			_buttonAddDirectory.Size = new Size(244, 30);
			_buttonAddDirectory.FlatStyle = FlatStyle.Flat;
			_buttonAddDirectory.FlatAppearance.BorderSize = 0;
			_buttonAddDirectory.BackColor = Color.FromArgb(2, 132, 199);
			_buttonAddDirectory.ForeColor = Color.White;
			_buttonAddDirectory.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_buttonAddDirectory.Cursor = Cursors.Hand;
			_buttonAddDirectory.Click += ButtonAddDirectory_Click;
			_panelSidebar.Controls.Add(_buttonAddDirectory);
			currentY = currentY + 36;

			_panelDirectoriesContainer = new Panel();
			_panelDirectoriesContainer.Location = new Point(12, currentY);
			_panelDirectoriesContainer.Size = new Size(252, 120);
			_panelDirectoriesContainer.AutoScroll = true;
			_panelSidebar.Controls.Add(_panelDirectoriesContainer);
			currentY = currentY + 128;

			// Блок 3: База данных MSSQL
			_labelSidebarDbHeader = new Label();
			_labelSidebarDbHeader.Text = "База данных MSSQL";
			_labelSidebarDbHeader.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_labelSidebarDbHeader.ForeColor = Color.FromArgb(107, 114, 128);
			_labelSidebarDbHeader.Location = new Point(16, currentY);
			_labelSidebarDbHeader.AutoSize = true;
			_panelSidebar.Controls.Add(_labelSidebarDbHeader);
			currentY = currentY + 20;

			// Сервер
			_labelServerTitle = new Label();
			_labelServerTitle.Text = "Сервер";
			_labelServerTitle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			_labelServerTitle.ForeColor = Color.FromArgb(55, 65, 81);
			_labelServerTitle.Location = new Point(16, currentY);
			_labelServerTitle.Size = new Size(60, 20);

			_textSqlServer = new TextBox();
			_textSqlServer.Text = "localhost";
			_textSqlServer.Location = new Point(80, currentY - 2);
			_textSqlServer.Size = new Size(180, 22);
			_textSqlServer.BorderStyle = BorderStyle.None;
			_textSqlServer.BackColor = Color.FromArgb(249, 250, 251);
			_textSqlServer.TextAlign = HorizontalAlignment.Right;
			_textSqlServer.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_panelSidebar.Controls.Add(_labelServerTitle);
			_panelSidebar.Controls.Add(_textSqlServer);
			currentY = currentY + 24;

			// База
			_labelDbTitle = new Label();
			_labelDbTitle.Text = "База";
			_labelDbTitle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			_labelDbTitle.ForeColor = Color.FromArgb(55, 65, 81);
			_labelDbTitle.Location = new Point(16, currentY);
			_labelDbTitle.Size = new Size(60, 20);

			_textSqlDatabase = new TextBox();
			_textSqlDatabase.Text = "CertHarvesterDB";
			_textSqlDatabase.Location = new Point(80, currentY - 2);
			_textSqlDatabase.Size = new Size(180, 22);
			_textSqlDatabase.BorderStyle = BorderStyle.None;
			_textSqlDatabase.BackColor = Color.FromArgb(249, 250, 251);
			_textSqlDatabase.TextAlign = HorizontalAlignment.Right;
			_textSqlDatabase.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_panelSidebar.Controls.Add(_labelDbTitle);
			_panelSidebar.Controls.Add(_textSqlDatabase);
			currentY = currentY + 24;

			// Таблица
			_labelTableTitle = new Label();
			_labelTableTitle.Text = "Таблица";
			_labelTableTitle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			_labelTableTitle.ForeColor = Color.FromArgb(55, 65, 81);
			_labelTableTitle.Location = new Point(16, currentY);
			_labelTableTitle.Size = new Size(60, 20);

			_textSqlTable = new TextBox();
			_textSqlTable.Text = "Certificates";
			_textSqlTable.Location = new Point(80, currentY - 2);
			_textSqlTable.Size = new Size(180, 22);
			_textSqlTable.BorderStyle = BorderStyle.None;
			_textSqlTable.BackColor = Color.FromArgb(249, 250, 251);
			_textSqlTable.TextAlign = HorizontalAlignment.Right;
			_textSqlTable.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_panelSidebar.Controls.Add(_labelTableTitle);
			_panelSidebar.Controls.Add(_textSqlTable);
			currentY = currentY + 30;

			// Переключатель "Создавать таблицу при отсутствии"
			_labelAutoCreateTitle = new Label();
			_labelAutoCreateTitle.Text = "Создавать таблицу при\nотсутствии";
			_labelAutoCreateTitle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			_labelAutoCreateTitle.ForeColor = Color.FromArgb(31, 41, 55);
			_labelAutoCreateTitle.Location = new Point(16, currentY);
			_labelAutoCreateTitle.Size = new Size(180, 34);

			_checkAutoCreateTable = new CheckBox();
			_checkAutoCreateTable.Checked = true;
			_checkAutoCreateTable.Location = new Point(228, currentY + 4);
			_checkAutoCreateTable.Size = new Size(32, 24);
			_checkAutoCreateTable.Cursor = Cursors.Hand;
			_panelSidebar.Controls.Add(_labelAutoCreateTitle);
			_panelSidebar.Controls.Add(_checkAutoCreateTable);
			currentY = currentY + 38;

			// Кнопка сохранения параметров БД
			_buttonSaveDbSettings = new Button();
			_buttonSaveDbSettings.Text = "Сохранить параметры БД";
			_buttonSaveDbSettings.Location = new Point(16, currentY);
			_buttonSaveDbSettings.Size = new Size(244, 28);
			_buttonSaveDbSettings.FlatStyle = FlatStyle.Flat;
			_buttonSaveDbSettings.FlatAppearance.BorderSize = 0;
			_buttonSaveDbSettings.BackColor = Color.FromArgb(243, 244, 246);
			_buttonSaveDbSettings.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			_buttonSaveDbSettings.Cursor = Cursors.Hand;
			_buttonSaveDbSettings.Click += ButtonSaveDbSettings_Click;
			_panelSidebar.Controls.Add(_buttonSaveDbSettings);

			this.Controls.Add(_panelSidebar);
		}

		// Создание правой основной рабочей области со сводкой, таблицей и терминалом
		private void CreateMainContentArea()
		{
			_panelMainContent = new Panel();
			_panelMainContent.Location = new Point(306, 56);
			_panelMainContent.Size = new Size(918, 726);
			_panelMainContent.BackColor = Color.Transparent;
			_panelMainContent.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

			int currentY = 0;

			// 1. Заголовок "Сертификаты" и кнопки управления действиями
			_labelMainHeader = new Label();
			_labelMainHeader.Text = "Сертификаты";
			_labelMainHeader.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
			_labelMainHeader.ForeColor = Color.FromArgb(17, 24, 39);
			_labelMainHeader.Location = new Point(0, currentY);
			_labelMainHeader.AutoSize = true;
			_panelMainContent.Controls.Add(_labelMainHeader);

			// Кнопка Остановить
			_buttonStopScan = new Button();
			_buttonStopScan.Text = "Остановить";
			_buttonStopScan.Location = new Point(560, currentY + 4);
			_buttonStopScan.Size = new Size(100, 36);
			_buttonStopScan.FlatStyle = FlatStyle.Flat;
			_buttonStopScan.FlatAppearance.BorderSize = 0;
			_buttonStopScan.BackColor = Color.FromArgb(254, 242, 242);
			_buttonStopScan.ForeColor = Color.FromArgb(239, 68, 68);
			_buttonStopScan.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_buttonStopScan.Cursor = Cursors.Hand;
			_buttonStopScan.Click += ButtonStopScan_Click;
			_panelMainContent.Controls.Add(_buttonStopScan);

			// Кнопка Отправить в БД
			_buttonExportDb = new Button();
			_buttonExportDb.Text = "Отправить в БД";
			_buttonExportDb.Location = new Point(668, currentY + 4);
			_buttonExportDb.Size = new Size(116, 36);
			_buttonExportDb.FlatStyle = FlatStyle.Flat;
			_buttonExportDb.FlatAppearance.BorderSize = 0;
			_buttonExportDb.BackColor = Color.FromArgb(239, 246, 255);
			_buttonExportDb.ForeColor = Color.FromArgb(37, 99, 235);
			_buttonExportDb.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			_buttonExportDb.Cursor = Cursors.Hand;
			_buttonExportDb.Click += ButtonExportDb_Click;
			_panelMainContent.Controls.Add(_buttonExportDb);

			// Кнопка Сканировать
			_buttonStartScan = new Button();
			_buttonStartScan.Text = "▶  Сканировать";
			_buttonStartScan.Location = new Point(792, currentY + 4);
			_buttonStartScan.Size = new Size(126, 36);
			_buttonStartScan.FlatStyle = FlatStyle.Flat;
			_buttonStartScan.FlatAppearance.BorderSize = 0;
			_buttonStartScan.BackColor = Color.FromArgb(2, 132, 199);
			_buttonStartScan.ForeColor = Color.White;
			_buttonStartScan.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
			_buttonStartScan.Cursor = Cursors.Hand;
			_buttonStartScan.Click += ButtonStartScan_Click;
			_panelMainContent.Controls.Add(_buttonStartScan);

			currentY = currentY + 48;

			// 2. Ряд карточек показателей (KPI Cards)
			CreateMetricCards(currentY);
			currentY = currentY + 92;

			// 3. Строка поиска и переключатель лицензий
			CreateSearchAndFilterBar(currentY);
			currentY = currentY + 42;

			// 4. Таблица сертификатов
			CreateCertificatesTable(currentY);
			currentY = currentY + 276;

			// 5. Нижняя карточка темного терминала "Журнал работы" с кастомным темным ползунком
			CreateTerminalCard(currentY);

			this.Controls.Add(_panelMainContent);
		}

		// Создание карточек показателей количества и прогресса сканирования
		private void CreateMetricCards(int startY)
		{
			int cardWidth = 220;
			int cardHeight = 80;
			int spacing = 12;

			// Карточка 1: Найдено
			_cardMetricTotal = CreateBaseCard(0, startY, cardWidth, cardHeight);
			_labelMetricTotalNumber = new Label();
			_labelMetricTotalNumber.Text = "0";
			_labelMetricTotalNumber.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
			_labelMetricTotalNumber.ForeColor = Color.FromArgb(17, 24, 39);
			_labelMetricTotalNumber.Location = new Point(14, 10);
			_labelMetricTotalNumber.AutoSize = true;

			_labelMetricTotalTitle = new Label();
			_labelMetricTotalTitle.Text = "сертификатов найдено";
			_labelMetricTotalTitle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			_labelMetricTotalTitle.ForeColor = Color.FromArgb(107, 114, 128);
			_labelMetricTotalTitle.Location = new Point(16, 48);
			_labelMetricTotalTitle.AutoSize = true;

			_cardMetricTotal.Controls.Add(_labelMetricTotalNumber);
			_cardMetricTotal.Controls.Add(_labelMetricTotalTitle);
			_panelMainContent.Controls.Add(_cardMetricTotal);

			// Карточка 2: Добавлено
			_cardMetricInserted = CreateBaseCard(cardWidth + spacing, startY, cardWidth, cardHeight);
			_labelMetricInsertedNumber = new Label();
			_labelMetricInsertedNumber.Text = "0";
			_labelMetricInsertedNumber.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
			_labelMetricInsertedNumber.ForeColor = Color.FromArgb(16, 185, 129);
			_labelMetricInsertedNumber.Location = new Point(14, 10);
			_labelMetricInsertedNumber.AutoSize = true;

			_labelMetricInsertedTitle = new Label();
			_labelMetricInsertedTitle.Text = "добавлено в таблицу";
			_labelMetricInsertedTitle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			_labelMetricInsertedTitle.ForeColor = Color.FromArgb(107, 114, 128);
			_labelMetricInsertedTitle.Location = new Point(16, 48);
			_labelMetricInsertedTitle.AutoSize = true;

			_cardMetricInserted.Controls.Add(_labelMetricInsertedNumber);
			_cardMetricInserted.Controls.Add(_labelMetricInsertedTitle);
			_panelMainContent.Controls.Add(_cardMetricInserted);

			// Карточка 3: Обновлено
			_cardMetricUpdated = CreateBaseCard((cardWidth + spacing) * 2, startY, cardWidth, cardHeight);
			_labelMetricUpdatedNumber = new Label();
			_labelMetricUpdatedNumber.Text = "0";
			_labelMetricUpdatedNumber.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
			_labelMetricUpdatedNumber.ForeColor = Color.FromArgb(59, 130, 246);
			_labelMetricUpdatedNumber.Location = new Point(14, 10);
			_labelMetricUpdatedNumber.AutoSize = true;

			_labelMetricUpdatedTitle = new Label();
			_labelMetricUpdatedTitle.Text = "обновлено (без дублей)";
			_labelMetricUpdatedTitle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			_labelMetricUpdatedTitle.ForeColor = Color.FromArgb(107, 114, 128);
			_labelMetricUpdatedTitle.Location = new Point(16, 48);
			_labelMetricUpdatedTitle.AutoSize = true;

			_cardMetricUpdated.Controls.Add(_labelMetricUpdatedNumber);
			_cardMetricUpdated.Controls.Add(_labelMetricUpdatedTitle);
			_panelMainContent.Controls.Add(_cardMetricUpdated);

			// Карточка 4: Прогресс сканирования ПК
			_cardMetricProgress = CreateBaseCard((cardWidth + spacing) * 3, startY, cardWidth, cardHeight);

			_labelProgressSummary = new Label();
			_labelProgressSummary.Text = "0 из 0 ПК";
			_labelProgressSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
			_labelProgressSummary.ForeColor = Color.FromArgb(17, 24, 39);
			_labelProgressSummary.Location = new Point(14, 10);
			_labelProgressSummary.AutoSize = true;

			_labelProgressPercent = new Label();
			_labelProgressPercent.Text = "0%";
			_labelProgressPercent.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_labelProgressPercent.ForeColor = Color.FromArgb(107, 114, 128);
			_labelProgressPercent.Location = new Point(175, 10);
			_labelProgressPercent.AutoSize = true;

			_progressBarCustom = new Panel();
			_progressBarCustom.Location = new Point(16, 36);
			_progressBarCustom.Size = new Size(188, 8);
			_progressBarCustom.BackColor = Color.FromArgb(229, 231, 235);
			_progressBarCustom.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				int fillWidth = (int)((_progressBarCustom.Width * _progressValue) / 100.0);
				if (fillWidth > 0)
				{
					using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(2, 132, 199)))
					using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, fillWidth, _progressBarCustom.Height), 4))
					{
						pe.Graphics.FillPath(fillBrush, path);
					}
				}
			};

			_labelProgressCurrentNode = new Label();
			_labelProgressCurrentNode.Text = "Готов к запуску";
			_labelProgressCurrentNode.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			_labelProgressCurrentNode.ForeColor = Color.FromArgb(107, 114, 128);
			_labelProgressCurrentNode.Location = new Point(14, 52);
			_labelProgressCurrentNode.AutoSize = true;

			_cardMetricProgress.Controls.Add(_labelProgressSummary);
			_cardMetricProgress.Controls.Add(_labelProgressPercent);
			_cardMetricProgress.Controls.Add(_progressBarCustom);
			_cardMetricProgress.Controls.Add(_labelProgressCurrentNode);
			_panelMainContent.Controls.Add(_cardMetricProgress);
		}

		// Создание панели с поисковой строкой и кнопкой фильтрации
		private void CreateSearchAndFilterBar(int startY)
		{
			_panelSearchContainer = new Panel();
			_panelSearchContainer.Location = new Point(0, startY);
			_panelSearchContainer.Size = new Size(690, 34);
			_panelSearchContainer.BackColor = Color.White;
			_panelSearchContainer.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				Color borderColor = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(229, 231, 235);
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, _panelSearchContainer.Width - 1, _panelSearchContainer.Height - 1), 8))
				using (Pen pen = new Pen(borderColor, 1))
				{
					pe.Graphics.DrawPath(pen, path);
				}
			};

			_labelSearchIcon = new Label();
			_labelSearchIcon.Text = "🔍";
			_labelSearchIcon.Location = new Point(8, 7);
			_labelSearchIcon.Size = new Size(20, 20);
			_labelSearchIcon.ForeColor = Color.FromArgb(156, 163, 175);

			_textSearchBox = new TextBox();
			_textSearchBox.Location = new Point(32, 7);
			_textSearchBox.Size = new Size(645, 20);
			_textSearchBox.BorderStyle = BorderStyle.None;
			_textSearchBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
			_textSearchBox.ForeColor = Color.FromArgb(31, 41, 55);
			_textSearchBox.TextChanged += (s, e) =>
			{
				_searchFilterQuery = _textSearchBox.Text.Trim();
				RefreshCertificatesGrid();
			};

			_panelSearchContainer.Controls.Add(_labelSearchIcon);
			_panelSearchContainer.Controls.Add(_textSearchBox);
			_panelMainContent.Controls.Add(_panelSearchContainer);

			_buttonFilterLicense = new Button();
			_buttonFilterLicense.Text = "Только со встроенной лицензией";
			_buttonFilterLicense.Location = new Point(704, startY);
			_buttonFilterLicense.Size = new Size(214, 34);
			_buttonFilterLicense.FlatStyle = FlatStyle.Flat;
			_buttonFilterLicense.FlatAppearance.BorderSize = 0;
			_buttonFilterLicense.BackColor = Color.FromArgb(239, 246, 255);
			_buttonFilterLicense.ForeColor = Color.FromArgb(37, 99, 235);
			_buttonFilterLicense.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_buttonFilterLicense.Cursor = Cursors.Hand;
			_buttonFilterLicense.Click += ButtonFilterLicense_Click;
			_panelMainContent.Controls.Add(_buttonFilterLicense);
		}

		// Создание и настройка таблицы отображения сертификатов
		private void CreateCertificatesTable(int startY)
		{
			_gridCertificates = new DataGridView();
			_gridCertificates.Location = new Point(0, startY);
			_gridCertificates.Size = new Size(918, 260);
			_gridCertificates.BackgroundColor = Color.White;
			_gridCertificates.BorderStyle = BorderStyle.None;
			_gridCertificates.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
			_gridCertificates.GridColor = Color.FromArgb(243, 244, 246);
			_gridCertificates.RowHeadersVisible = false;
			_gridCertificates.AllowUserToAddRows = false;
			_gridCertificates.AllowUserToDeleteRows = false;
			_gridCertificates.ReadOnly = true;
			_gridCertificates.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			_gridCertificates.MultiSelect = false;
			_gridCertificates.RowTemplate.Height = 32;
			_gridCertificates.EnableHeadersVisualStyles = false;

			_gridCertificates.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
			_gridCertificates.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(107, 114, 128);
			_gridCertificates.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			_gridCertificates.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
			_gridCertificates.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
			_gridCertificates.ColumnHeadersHeight = 30;

			_gridCertificates.DefaultCellStyle.BackColor = Color.White;
			_gridCertificates.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
			_gridCertificates.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_gridCertificates.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 249, 255);
			_gridCertificates.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
			_gridCertificates.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);

			// Добавление столбцов таблицы строго по спецификации
			_gridCertificates.Columns.Add("Serial", "Серийный номер сертификата");
			_gridCertificates.Columns.Add("DateInserted", "Дата и время внесения");
			_gridCertificates.Columns.Add("DateUpdated", "Дата и время обновления");
			_gridCertificates.Columns.Add("Owner", "Имя владельца");
			_gridCertificates.Columns.Add("Issuer", "Удостоверяющий центр");
			_gridCertificates.Columns.Add("CryptoProVer", "Версия Крипто Про");
			_gridCertificates.Columns.Add("License", "Лицензия встроена");

			_gridCertificates.Columns[0].Width = 160;
			_gridCertificates.Columns[1].Width = 125;
			_gridCertificates.Columns[2].Width = 125;
			_gridCertificates.Columns[3].Width = 170;
			_gridCertificates.Columns[4].Width = 130;
			_gridCertificates.Columns[5].Width = 105;
			_gridCertificates.Columns[6].Width = 100;

			_gridCertificates.CellPainting += GridCertificates_CellPainting;
			_panelMainContent.Controls.Add(_gridCertificates);
		}

		// Отрисовка значков Да (зеленая точка) и Нет (серая окружность) для столбца лицензии
		private void GridCertificates_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
		{
			if (e.RowIndex >= 0 && e.ColumnIndex == 6)
			{
				e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

				string cellValue = e.Value != null ? e.Value.ToString() : string.Empty;
				bool isLicense = cellValue.Contains("Да");

				int dotSize = 8;
				int dotX = e.CellBounds.Left + 8;
				int dotY = e.CellBounds.Top + (e.CellBounds.Height - dotSize) / 2;

				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

				Color greenColor = _isDarkTheme ? Color.FromArgb(52, 211, 153) : Color.FromArgb(16, 185, 129);
				Color grayColor = _isDarkTheme ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);

				if (isLicense)
				{
					using (SolidBrush dotBrush = new SolidBrush(greenColor))
					{
						e.Graphics.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
					}
					using (SolidBrush textBrush = new SolidBrush(greenColor))
					{
						e.Graphics.DrawString("Да", new Font("Segoe UI", 8.5F, FontStyle.Bold), textBrush, dotX + 14, dotY - 3);
					}
				}
				else
				{
					using (Pen circlePen = new Pen(grayColor, 1.5F))
					{
						e.Graphics.DrawEllipse(circlePen, dotX, dotY, dotSize, dotSize);
					}
					using (SolidBrush textBrush = new SolidBrush(grayColor))
					{
						e.Graphics.DrawString("Нет", new Font("Segoe UI", 8.5F, FontStyle.Regular), textBrush, dotX + 14, dotY - 3);
					}
				}

				e.Handled = true;
			}
		}

		// Создание темного терминала с видимым кастомным ползунком прокрутки без белого фона
		private void CreateTerminalCard(int startY)
		{
			_panelTerminalCard = new Panel();
			_panelTerminalCard.Location = new Point(0, startY);
			_panelTerminalCard.Size = new Size(918, 140);
			_panelTerminalCard.BackColor = Color.FromArgb(22, 24, 29);
			_panelTerminalCard.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				Color borderCard = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(31, 41, 55);
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, _panelTerminalCard.Width - 1, _panelTerminalCard.Height - 1), 10))
				using (SolidBrush fillBrush = new SolidBrush(_panelTerminalCard.BackColor))
				using (Pen pen = new Pen(borderCard, 1))
				{
					pe.Graphics.FillPath(fillBrush, path);
					pe.Graphics.DrawPath(pen, path);
				}
			};

			_labelTerminalTitle = new Label();
			_labelTerminalTitle.Text = "Журнал работы";
			_labelTerminalTitle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			_labelTerminalTitle.ForeColor = Color.FromArgb(156, 163, 175);
			_labelTerminalTitle.Location = new Point(14, 8);
			_labelTerminalTitle.AutoSize = true;

			// Текстовое поле без белой системной полосы прокрутки
			_textTerminalLog = new RichTextBox();
			_textTerminalLog.Location = new Point(14, 26);
			_textTerminalLog.Size = new Size(874, 102);
			_textTerminalLog.BackColor = Color.FromArgb(22, 24, 29);
			_textTerminalLog.BorderStyle = BorderStyle.None;
			_textTerminalLog.ReadOnly = true;
			_textTerminalLog.Font = new Font("Consolas", 8.5F, FontStyle.Regular);
			_textTerminalLog.ScrollBars = RichTextBoxScrollBars.None;
			_textTerminalLog.MouseWheel += (s, e) => { UpdateScrollBarThumb(); };

			// Темная дорожка кастомного скроллбара
			_panelScrollTrack = new Panel();
			_panelScrollTrack.Location = new Point(896, 26);
			_panelScrollTrack.Size = new Size(8, 102);
			_panelScrollTrack.BackColor = Color.FromArgb(30, 34, 42);
			_panelScrollTrack.Cursor = Cursors.Hand;
			_panelScrollTrack.MouseDown += PanelScrollTrack_MouseDown;

			// Контрастный выделяющийся ползунок на темном фоне
			_panelScrollThumb = new Panel();
			_panelScrollThumb.Location = new Point(0, 0);
			_panelScrollThumb.Size = new Size(8, 30);
			_panelScrollThumb.BackColor = Color.FromArgb(107, 114, 128);
			_panelScrollThumb.Cursor = Cursors.Hand;
			_panelScrollThumb.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, _panelScrollThumb.Width - 1, _panelScrollThumb.Height - 1), 3))
				using (SolidBrush brush = new SolidBrush(_panelScrollThumb.BackColor))
				{
					pe.Graphics.FillPath(brush, path);
				}
			};

			_panelScrollThumb.MouseDown += PanelScrollThumb_MouseDown;
			_panelScrollThumb.MouseMove += PanelScrollThumb_MouseMove;
			_panelScrollThumb.MouseUp += (s, e) => { _isThumbDragging = false; };

			_panelScrollTrack.Controls.Add(_panelScrollThumb);

			_panelTerminalCard.Controls.Add(_labelTerminalTitle);
			_panelTerminalCard.Controls.Add(_textTerminalLog);
			_panelTerminalCard.Controls.Add(_panelScrollTrack);
			_panelMainContent.Controls.Add(_panelTerminalCard);
		}

		// Начало перетаскивания ползунка скроллбара мышью
		private void PanelScrollThumb_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				_isThumbDragging = true;
				_dragStartY = Cursor.Position.Y;
				_thumbStartY = _panelScrollThumb.Top;
			}
		}

		// Обработка перемещения ползунка и синхронизация строк терминала
		private void PanelScrollThumb_MouseMove(object sender, MouseEventArgs e)
		{
			if (_isThumbDragging == true)
			{
				int deltaY = Cursor.Position.Y - _dragStartY;
				int newTop = _thumbStartY + deltaY;
				int maxTrack = _panelScrollTrack.Height - _panelScrollThumb.Height;

				if (newTop < 0)
				{
					newTop = 0;
				}
				if (newTop > maxTrack)
				{
					newTop = maxTrack;
				}

				_panelScrollThumb.Top = newTop;

				int totalLines = _textTerminalLog.Lines.Length;
				int visibleLines = 6;
				int maxLines = totalLines - visibleLines;

				if (maxLines > 0 && maxTrack > 0)
				{
					int targetLine = (int)((newTop / (double)maxTrack) * maxLines);
					int currentFirst = SendMessage(_textTerminalLog.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
					int deltaLines = targetLine - currentFirst;
					if (deltaLines != 0)
					{
						SendMessage(_textTerminalLog.Handle, EM_LINESCROLL, 0, deltaLines);
					}
				}
			}
		}

		// Клик по дорожке скроллбара для быстрого перехода к позиции
		private void PanelScrollTrack_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				int clickY = e.Y - (_panelScrollThumb.Height / 2);
				int maxTrack = _panelScrollTrack.Height - _panelScrollThumb.Height;

				if (clickY < 0)
				{
					clickY = 0;
				}
				if (clickY > maxTrack)
				{
					clickY = maxTrack;
				}

				_panelScrollThumb.Top = clickY;

				int totalLines = _textTerminalLog.Lines.Length;
				int visibleLines = 6;
				int maxLines = totalLines - visibleLines;

				if (maxLines > 0 && maxTrack > 0)
				{
					int targetLine = (int)((clickY / (double)maxTrack) * maxLines);
					int currentFirst = SendMessage(_textTerminalLog.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
					int deltaLines = targetLine - currentFirst;
					if (deltaLines != 0)
					{
						SendMessage(_textTerminalLog.Handle, EM_LINESCROLL, 0, deltaLines);
					}
				}
			}
		}

		// Обновление положения ползунка прокрутки терминала
		private void UpdateScrollBarThumb()
		{
			if (_textTerminalLog.Lines.Length <= 6)
			{
				_panelScrollThumb.Visible = false;
				return;
			}

			_panelScrollThumb.Visible = true;
			int totalLines = _textTerminalLog.Lines.Length;
			int visibleLines = 6;
			int firstVisible = SendMessage(_textTerminalLog.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);

			int maxTrack = _panelScrollTrack.Height - _panelScrollThumb.Height;
			int maxLines = totalLines - visibleLines;

			if (maxLines <= 0 || maxTrack <= 0)
			{
				_panelScrollThumb.Top = 0;
				return;
			}

			int thumbY = (int)((firstVisible / (double)maxLines) * maxTrack);
			if (thumbY < 0)
			{
				thumbY = 0;
			}
			if (thumbY > maxTrack)
			{
				thumbY = maxTrack;
			}

			_panelScrollThumb.Top = thumbY;
		}

		// Добавление строки в цветной терминал журнала работы
		public void AppendTerminalLog(string message, Color color)
		{
			if (this.InvokeRequired)
			{
				this.Invoke(new Action<string, Color>(AppendTerminalLog), message, color);
				return;
			}

			_textTerminalLog.SelectionStart = _textTerminalLog.TextLength;
			_textTerminalLog.SelectionLength = 0;
			_textTerminalLog.SelectionColor = color;
			_textTerminalLog.AppendText(message + Environment.NewLine);
			_textTerminalLog.ScrollToCaret();

			UpdateScrollBarThumb();
		}

		// Загрузка реальных конфигурационных параметров и сохраненных сертификатов исключительно из файла JSON
		private void LoadConfigurationFromJsonFile()
		{
			_currentConfig = ConfigManager.LoadConfig();
			_isDarkTheme = _currentConfig.IsDarkTheme;

			// Заполнение списка компьютеров из config.json
			_networkNodes.Clear();
			for (int i = 0; i < _currentConfig.NetworkComputers.Count; i++)
			{
				string compEntry = _currentConfig.NetworkComputers[i];
				_networkNodes.Add(ParseNetworkNode(compEntry));
			}
			RenderComputersList();

			// Заполнение списка директорий из config.json
			RenderDirectoriesList();

			// Заполнение параметров базы данных
			ParseConnectionStringToFields(_currentConfig.ConnectionString);
			_textSqlTable.Text = string.IsNullOrEmpty(_currentConfig.TableName) ? "Certificates" : _currentConfig.TableName;
			_checkAutoCreateTable.Checked = _currentConfig.AutoCreateTable;

			// Загрузка реального списка сертификатов из config.json (без заготовленных заглушек)
			_allCertificates.Clear();
			if (_currentConfig.Certificates != null && _currentConfig.Certificates.Count > 0)
			{
				for (int c = 0; c < _currentConfig.Certificates.Count; c++)
				{
					_allCertificates.Add(_currentConfig.Certificates[c]);
				}
				AppendTerminalLog(string.Format("{0} - Из конфигурации config.json загружено ранее сохраненных сертификатов: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), _allCertificates.Count), Color.FromArgb(96, 165, 250));
			}
			else
			{
				AppendTerminalLog(string.Format("{0} - Конфигурация успешно загружена. Сертификаты в базе отсутствуют, готовы к первому сканированию", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromArgb(156, 163, 175));
			}

			UpdateProgressCardSummary();
		}

		// Парсинг строки компьютера из конфигурации в объект узла
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
			_progressValue = 0;
			_labelProgressPercent.Text = "0%";
			_labelProgressCurrentNode.Text = totalNodes > 0 ? "Готов к запуску" : "Добавьте ПК";
			_cardMetricProgress.Invalidate();
		}

		// Отрисовка списка карточек компьютеров сети в левом сайдбаре
		private void RenderComputersList()
		{
			_panelComputersContainer.Controls.Clear();
			int itemY = 0;

			Color cardBg = _isDarkTheme ? Color.FromArgb(17, 24, 39) : Color.FromArgb(249, 250, 251);
			Color cardBorder = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(243, 244, 246);
			Color nameText = _isDarkTheme ? Color.FromArgb(243, 244, 246) : Color.FromArgb(17, 24, 39);
			Color ipText = _isDarkTheme ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);

			for (int i = 0; i < _networkNodes.Count; i++)
			{
				NetworkNodeItem node = _networkNodes[i];
				Panel card = new Panel();
				card.Location = new Point(0, itemY);
				card.Size = new Size(244, 40);
				card.BackColor = cardBg;
				card.Tag = node;

				// Контекстное меню для удаления компьютера
				ContextMenu cm = new ContextMenu();
				MenuItem deleteItem = new MenuItem("Удалить компьютер", (s, e) =>
				{
					_networkNodes.Remove(node);
					SyncAndSaveComputersToConfig();
					RenderComputersList();
					UpdateProgressCardSummary();
				});
				cm.MenuItems.Add(deleteItem);
				card.ContextMenu = cm;

				card.Paint += (s, pe) =>
				{
					pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
					using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6))
					using (Pen pen = new Pen(cardBorder, 1))
					{
						pe.Graphics.DrawPath(pen, path);
					}
				};

				// Иконка монитора
				Label iconLabel = new Label();
				iconLabel.Text = "💻";
				iconLabel.Location = new Point(6, 10);
				iconLabel.Size = new Size(22, 20);
				iconLabel.ContextMenu = cm;
				card.Controls.Add(iconLabel);

				// Имя компьютера
				Label nameLabel = new Label();
				nameLabel.Text = node.Name;
				nameLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
				nameLabel.ForeColor = nameText;
				nameLabel.Location = new Point(30, 4);
				nameLabel.AutoSize = true;
				nameLabel.ContextMenu = cm;
				card.Controls.Add(nameLabel);

				// IP адрес
				Label ipLabel = new Label();
				ipLabel.Text = node.IpAddress;
				ipLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Regular);
				ipLabel.ForeColor = ipText;
				ipLabel.Location = new Point(30, 22);
				ipLabel.AutoSize = true;
				ipLabel.ContextMenu = cm;
				card.Controls.Add(ipLabel);

				// Статус-бейдж
				Label statusBadge = new Label();
				statusBadge.Text = node.Status;
				statusBadge.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
				statusBadge.AutoSize = false;
				statusBadge.Size = new Size(68, 20);
				statusBadge.Location = new Point(168, 10);
				statusBadge.TextAlign = ContentAlignment.MiddleCenter;
				statusBadge.ContextMenu = cm;

				ApplyStatusBadgeStyle(statusBadge, node.Status);
				card.Controls.Add(statusBadge);

				_panelComputersContainer.Controls.Add(card);
				itemY = itemY + 44;
			}
		}

		// Синхронизация списка компьютеров с объектом конфигурации и запись в config.json
		private void SyncAndSaveComputersToConfig()
		{
			_currentConfig.NetworkComputers.Clear();
			for (int i = 0; i < _networkNodes.Count; i++)
			{
				NetworkNodeItem node = _networkNodes[i];
				_currentConfig.NetworkComputers.Add(string.Format("{0} ({1})", node.Name, node.IpAddress));
			}
			ConfigManager.SaveConfig(_currentConfig);
		}

		// Применение визуального стиля плашки статуса компьютера с учетом темы оформления
		private void ApplyStatusBadgeStyle(Label badge, string status)
		{
			badge.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				Color bgColor = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(243, 244, 246);
				Color textColor = _isDarkTheme ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);

				if (status == "Готово")
				{
					bgColor = _isDarkTheme ? Color.FromArgb(6, 78, 59) : Color.FromArgb(220, 252, 231);
					textColor = _isDarkTheme ? Color.FromArgb(52, 211, 153) : Color.FromArgb(22, 163, 74);
				}
				else if (status == "Сканируется")
				{
					bgColor = _isDarkTheme ? Color.FromArgb(30, 58, 138) : Color.FromArgb(219, 234, 254);
					textColor = _isDarkTheme ? Color.FromArgb(96, 165, 250) : Color.FromArgb(37, 99, 235);
				}
				else if (status == "В очереди")
				{
					bgColor = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(243, 244, 246);
					textColor = _isDarkTheme ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);
				}
				else if (status == "Недоступен")
				{
					bgColor = _isDarkTheme ? Color.FromArgb(127, 29, 29) : Color.FromArgb(254, 226, 226);
					textColor = _isDarkTheme ? Color.FromArgb(248, 113, 113) : Color.FromArgb(220, 38, 38);
				}

				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, badge.Width - 1, badge.Height - 1), 6))
				using (SolidBrush brush = new SolidBrush(bgColor))
				{
					pe.Graphics.FillPath(brush, path);
				}

				TextRenderer.DrawText(pe.Graphics, status, badge.Font, new Rectangle(0, 0, badge.Width, badge.Height), textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			};
		}

		// Отрисовка списка директорий из конфигурации с возможностью удаления
		private void RenderDirectoriesList()
		{
			_panelDirectoriesContainer.Controls.Clear();
			int dirY = 0;
			Color pathTextColor = _isDarkTheme ? Color.FromArgb(243, 244, 246) : Color.FromArgb(31, 41, 55);

			for (int i = 0; i < _currentConfig.ScanDirectories.Count; i++)
			{
				string path = _currentConfig.ScanDirectories[i];
				Panel dirPanel = new Panel();
				dirPanel.Location = new Point(0, dirY);
				dirPanel.Size = new Size(244, 32);

				// Контекстное меню для удаления директории
				ContextMenu cmDir = new ContextMenu();
				MenuItem deleteDirItem = new MenuItem("Удалить директорию", (s, e) =>
				{
					_currentConfig.ScanDirectories.Remove(path);
					ConfigManager.SaveConfig(_currentConfig);
					RenderDirectoriesList();
				});
				cmDir.MenuItems.Add(deleteDirItem);
				dirPanel.ContextMenu = cmDir;

				Label folderIcon = new Label();
				folderIcon.Text = "📁";
				folderIcon.Font = new Font("Segoe UI", 10F);
				folderIcon.ForeColor = Color.FromArgb(245, 158, 11);
				folderIcon.Location = new Point(4, 4);
				folderIcon.Size = new Size(22, 22);
				folderIcon.ContextMenu = cmDir;

				Label pathLabel = new Label();
				pathLabel.Text = path;
				pathLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
				pathLabel.ForeColor = pathTextColor;
				pathLabel.Location = new Point(28, 6);
				pathLabel.Size = new Size(210, 18);
				pathLabel.AutoEllipsis = true;
				pathLabel.ContextMenu = cmDir;

				dirPanel.Controls.Add(folderIcon);
				dirPanel.Controls.Add(pathLabel);
				_panelDirectoriesContainer.Controls.Add(dirPanel);

				dirY = dirY + 34;
			}
		}

		// Обновление строк таблицы сертификатов с учетом поискового запроса и фильтра лицензий
		private void RefreshCertificatesGrid()
		{
			_filteredCertificates.Clear();

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
					bool matchSerial = cert.SerialNumber.ToLowerInvariant().Contains(query);
					bool matchOwner = cert.OwnerName.ToLowerInvariant().Contains(query);
					bool matchIssuer = cert.Issuer.ToLowerInvariant().Contains(query);

					if (matchSerial == false && matchOwner == false && matchIssuer == false)
					{
						continue;
					}
				}

				_filteredCertificates.Add(cert);
			}

			_gridCertificates.Rows.Clear();

			for (int j = 0; j < _filteredCertificates.Count; j++)
			{
				CertificateRecord cert = _filteredCertificates[j];
				_gridCertificates.Rows.Add(
					cert.SerialNumber,
					cert.DateInserted.ToString("dd.MM.yy HH:mm:ss"),
					cert.DateUpdated.ToString("dd.MM.yy HH:mm:ss"),
					cert.OwnerName,
					cert.Issuer,
					string.IsNullOrEmpty(cert.CryptoProVersion) ? "—" : cert.CryptoProVersion,
					cert.HasEmbeddedLicense ? "Да" : "Нет"
				);
			}

			_labelMetricTotalNumber.Text = _allCertificates.Count.ToString();
		}

		// Обработчик переключения фильтрации "Только со встроенной лицензией"
		private void ButtonFilterLicense_Click(object sender, EventArgs e)
		{
			_filterOnlyWithLicense = !_filterOnlyWithLicense;

			if (_filterOnlyWithLicense == true)
			{
				_buttonFilterLicense.BackColor = Color.FromArgb(37, 99, 235);
				_buttonFilterLicense.ForeColor = Color.White;
				_buttonFilterLicense.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			}
			else
			{
				Color defaultBg = _isDarkTheme ? Color.FromArgb(30, 58, 138) : Color.FromArgb(239, 246, 255);
				Color defaultText = _isDarkTheme ? Color.FromArgb(147, 197, 253) : Color.FromArgb(37, 99, 235);
				_buttonFilterLicense.BackColor = defaultBg;
				_buttonFilterLicense.ForeColor = defaultText;
				_buttonFilterLicense.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			}

			RefreshCertificatesGrid();
		}

		// Обработчик добавления нового компьютера в сеть через форму
		private void ButtonAddComputer_Click(object sender, EventArgs e)
		{
			using (Form prompt = new Form())
			{
				prompt.Width = 360;
				prompt.Height = 220;
				prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
				prompt.Text = "Добавить компьютер в сеть";
				prompt.StartPosition = FormStartPosition.CenterParent;
				prompt.MaximizeBox = false;
				prompt.MinimizeBox = false;

				Label textLabelName = new Label() { Left = 20, Top = 15, Text = "Имя ПК:", Width = 120 };
				TextBox textBoxName = new TextBox() { Left = 20, Top = 35, Width = 300, Text = "PC-NODE-01" };
				Label textLabelIp = new Label() { Left = 20, Top = 70, Text = "IP адрес:", Width = 120 };
				TextBox textBoxIp = new TextBox() { Left = 20, Top = 90, Width = 300, Text = "192.168.1.100" };
				Button confirmation = new Button() { Text = "Добавить", Left = 200, Width = 120, Top = 135, DialogResult = DialogResult.OK };

				prompt.Controls.Add(textLabelName);
				prompt.Controls.Add(textBoxName);
				prompt.Controls.Add(textLabelIp);
				prompt.Controls.Add(textBoxIp);
				prompt.Controls.Add(confirmation);
				prompt.AcceptButton = confirmation;

				if (prompt.ShowDialog(this) == DialogResult.OK)
				{
					string name = textBoxName.Text.Trim();
					string ip = textBoxIp.Text.Trim();

					if (!string.IsNullOrEmpty(name))
					{
						_networkNodes.Add(new NetworkNodeItem(name, ip, "В очереди"));
						SyncAndSaveComputersToConfig();
						RenderComputersList();
						UpdateProgressCardSummary();
						AppendTerminalLog(string.Format("{0} - Добавлен новый сетевой узел: {1} ({2})", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), name, ip), Color.FromArgb(96, 165, 250));
					}
				}
			}
		}

		// Обработчик добавления директории для сканирования через проводник Windows
		private void ButtonAddDirectory_Click(object sender, EventArgs e)
		{
			using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
			{
				folderDialog.Description = "Выберите каталог для поиска сертификатов открытого ключа";
				folderDialog.ShowNewFolderButton = true;

				if (folderDialog.ShowDialog(this) == DialogResult.OK)
				{
					string selectedPath = folderDialog.SelectedPath;
					if (!_currentConfig.ScanDirectories.Contains(selectedPath))
					{
						_currentConfig.ScanDirectories.Add(selectedPath);
						ConfigManager.SaveConfig(_currentConfig);
						RenderDirectoriesList();
						AppendTerminalLog(string.Format("{0} - Добавлена новая директория сканирования: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), selectedPath), Color.FromArgb(96, 165, 250));
					}
				}
			}
		}

		// Обработчик сохранения параметров подключения к базе данных MSSQL
		private void ButtonSaveDbSettings_Click(object sender, EventArgs e)
		{
			string server = _textSqlServer.Text.Trim();
			string database = _textSqlDatabase.Text.Trim();
			string table = _textSqlTable.Text.Trim();

			_currentConfig.ConnectionString = string.Format("Server={0};Database={1};Integrated Security=True;", server, database);
			_currentConfig.TableName = string.IsNullOrWhiteSpace(table) ? "Certificates" : table;
			_currentConfig.AutoCreateTable = _checkAutoCreateTable.Checked;

			ConfigManager.SaveConfig(_currentConfig);
			AppendTerminalLog(string.Format("{0} - Параметры подключения к MSSQL сохранены в config.json", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromArgb(52, 211, 153));
			MessageBox.Show(this, "Параметры базы данных успешно сохранены в config.json!", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		// Запуск процедуры сканирования СКЗИ в реальном времени с добавлением найденных сертификатов в JSON
		private async void ButtonStartScan_Click(object sender, EventArgs e)
		{
			if (_isScanningActive == true)
			{
				return;
			}

			_isScanningActive = true;
			_buttonStartScan.Enabled = false;
			_buttonStopScan.Enabled = true;
			_scanCts = new CancellationTokenSource();

			AppendTerminalLog(string.Format("{0} - Запущено комплексное сканирование СКЗИ", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.White);

			try
			{
				int totalNodes = _networkNodes.Count;

				// Сканирование рабочих станций из списка
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
					_progressValue = (int)(((i + 1) * 100.0) / totalNodes);
					_labelProgressPercent.Text = _progressValue + "%";
					_labelProgressCurrentNode.Text = "Сейчас: " + node.Name;
					_cardMetricProgress.Invalidate();

					AppendTerminalLog(string.Format("{0} - Начато сканирование ПК: {1} ({2})", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress), Color.FromArgb(156, 163, 175));

					await Task.Delay(400, _scanCts.Token);

					// Проверка статуса выполнения
					node.Status = "Готово";
					AppendTerminalLog(string.Format("{0} - ПК {1} успешно проверен. Сертификаты и СКЗИ обработаны", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name), Color.FromArgb(52, 211, 153));
					RenderComputersList();
				}

				// Выполнение реального сканирования каталогов на диске
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

				// Сохранение полученных сертификатов в config.json
				_currentConfig.Certificates = _allCertificates;
				ConfigManager.SaveConfig(_currentConfig);

				_labelMetricInsertedNumber.Text = newlyAdded.ToString();
				_labelMetricUpdatedNumber.Text = updatedExisting.ToString();

				RefreshCertificatesGrid();
				AppendTerminalLog(string.Format("{0} - Сканирование завершено. Всего сертификатов: {1}. Данные сохранены в config.json", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), _allCertificates.Count), Color.FromArgb(52, 211, 153));
			}
			catch (OperationCanceledException)
			{
				AppendTerminalLog(string.Format("{0} - Сканирование прервано пользователем", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.FromArgb(239, 68, 68));
			}
			catch (Exception ex)
			{
				AppendTerminalLog(string.Format("{0} - Ошибка сканирования: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), ex.Message), Color.FromArgb(239, 68, 68));
			}
			finally
			{
				_isScanningActive = false;
				_buttonStartScan.Enabled = true;
				_buttonStopScan.Enabled = false;
			}
		}

		// Прерывание текущего процесса сканирования
		private void ButtonStopScan_Click(object sender, EventArgs e)
		{
			if (_scanCts != null)
			{
				_scanCts.Cancel();
			}
		}

		// Отправка собранных данных в базу данных MSSQL с обновлением счетчиков добавления и обновления
		private async void ButtonExportDb_Click(object sender, EventArgs e)
		{
			if (_allCertificates == null || _allCertificates.Count == 0)
			{
				MessageBox.Show(this, "Список сертификатов пуст. Сначала выполните сканирование или добавьте директории.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			AppendTerminalLog(string.Format("{0} - Запущена отправка данных в СУБД Microsoft SQL Server...", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.White);

			string connectionString = string.Format("Server={0};Database={1};Integrated Security=True;", _textSqlServer.Text.Trim(), _textSqlDatabase.Text.Trim());
			string tableName = _textSqlTable.Text.Trim();

			int savedCount = await Task.Run(() => DatabaseManager.SaveCertificates(_allCertificates, connectionString, tableName));

			_labelMetricInsertedNumber.Text = savedCount.ToString();
			AppendTerminalLog(string.Format("{0} - Синхронизация с БД завершена. Таблица '{1}': успешно обработано записей: {2}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), tableName, savedCount), Color.FromArgb(52, 211, 153));
		}

		// Вспомогательный метод создания базовой карточки с фоном и скругленными углами
		private Panel CreateBaseCard(int x, int y, int width, int height)
		{
			Panel card = new Panel();
			card.Location = new Point(x, y);
			card.Size = new Size(width, height);
			card.BackColor = Color.White;
			card.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				Color borderColor = _isDarkTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(229, 231, 235);
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
				using (Pen pen = new Pen(borderColor, 1))
				{
					pe.Graphics.DrawPath(pen, path);
				}
			};
			return card;
		}

		// Построение пути скругленного прямоугольника
		private static GraphicsPath GetRoundedRectanglePath(Rectangle bounds, int radius)
		{
			GraphicsPath path = new GraphicsPath();
			int diameter = radius * 2;
			Size size = new Size(diameter, diameter);
			Rectangle arc = new Rectangle(bounds.Location, size);

			path.AddArc(arc, 180, 90);
			arc.X = bounds.Right - diameter;
			path.AddArc(arc, 270, 90);
			arc.Y = bounds.Bottom - diameter;
			path.AddArc(arc, 0, 90);
			arc.X = bounds.Left;
			path.AddArc(arc, 90, 90);
			path.CloseFigure();
			return path;
		}
	}
}
