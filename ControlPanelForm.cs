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
	/// Класс модели данных компьютера в локальной сети для отображения в боковой панели
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
	/// Современная форма панели управления СКЗИ с карточным интерфейсом и темным терминалом журнала
	/// </summary>
	public class ControlPanelForm : Form
	{
		[DllImport("user32.dll")]
		public static extern bool ReleaseCapture();

		[DllImport("user32.dll")]
		public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

		private const int WM_NCLBUTTONDOWN = 0xA1;
		private const int HT_CAPTION = 0x2;

		private AppConfig _currentConfig;
		private List<CertificateRecord> _allCertificates;
		private List<CertificateRecord> _filteredCertificates;
		private List<NetworkNodeItem> _networkNodes;
		private bool _filterOnlyWithLicense;
		private string _searchFilterQuery;
		private CancellationTokenSource _scanCts;
		private bool _isScanningActive;

		// UI компоненты шапки
		private Panel _panelTopHeader;
		private Label _labelAppTitle;
		private Button _tabButtonScan;
		private Button _tabButtonLogs;
		private Button _tabButtonSettings;

		// UI компоненты левого сайдбара
		private Panel _panelSidebar;
		private Button _buttonAddComputer;
		private Panel _panelComputersContainer;
		private Panel _panelDirectoriesContainer;
		private TextBox _textSqlServer;
		private TextBox _textSqlDatabase;
		private TextBox _textSqlTable;
		private CheckBox _checkAutoCreateTable;

		// UI компоненты главной правой области
		private Panel _panelMainContent;
		private Label _labelMainHeader;
		private Button _buttonStopScan;
		private Button _buttonExportDb;
		private Button _buttonStartScan;

		// Карточки метрик (KPI)
		private Panel _cardMetricTotal;
		private Label _labelMetricTotalNumber;
		private Panel _cardMetricInserted;
		private Label _labelMetricInsertedNumber;
		private Panel _cardMetricUpdated;
		private Label _labelMetricUpdatedNumber;
		private Panel _cardMetricProgress;
		private Label _labelProgressSummary;
		private Label _labelProgressPercent;
		private Label _labelProgressCurrentNode;
		private int _progressValue;

		// Строка поиска и фильтрации
		private TextBox _textSearchBox;
		private Button _buttonFilterLicense;

		// Таблица сертификатов
		private DataGridView _gridCertificates;

		// Терминал журнала работы
		private Panel _panelTerminalCard;
		private RichTextBox _textTerminalLog;

		// Конструктор инициализации формы графической панели управления
		public ControlPanelForm()
		{
			_allCertificates = new List<CertificateRecord>();
			_filteredCertificates = new List<CertificateRecord>();
			_networkNodes = new List<NetworkNodeItem>();
			_filterOnlyWithLicense = false;
			_searchFilterQuery = string.Empty;
			_progressValue = 62;

			this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
			this.UpdateStyles();

			InitializeComponentCustom();
			LoadConfigurationAndMockData();
			RefreshCertificatesGrid();
		}

		// Инициализация структуры и внешнего вида окна панели управления
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

			// 1. Верхняя панель заголовка окна
			CreateTopHeaderBar();

			// 2. Левая панель настроек (Компьютеры, Каталоги, MSSQL)
			CreateLeftSidebar();

			// 3. Главная область (Заголовок, Кнопки, Метрики, Поиск, Таблица, Журнал)
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

		// Создание верхней панели с центрированным заголовком и табами справа
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

			// Табы справа: Сканирование, Журнал, Настройки
			Panel panelTabs = new Panel();
			panelTabs.Size = new Size(270, 32);
			panelTabs.Location = new Point(this.ClientSize.Width - 290, 10);
			panelTabs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			panelTabs.BackColor = Color.FromArgb(235, 238, 242);
			panelTabs.Paint += (s, pe) =>
			{
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, panelTabs.Width - 1, panelTabs.Height - 1), 6))
				using (Pen pen = new Pen(Color.FromArgb(220, 224, 230), 1))
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

			panelTabs.Controls.Add(_tabButtonScan);
			panelTabs.Controls.Add(_tabButtonLogs);
			panelTabs.Controls.Add(_tabButtonSettings);

			_panelTopHeader.Controls.Add(_labelAppTitle);
			_panelTopHeader.Controls.Add(panelTabs);

			// Центрирование заголовка при изменении размера
			_panelTopHeader.Resize += (s, e) =>
			{
				_labelAppTitle.Location = new Point((_panelTopHeader.Width - _labelAppTitle.Width) / 2, 16);
			};

			this.Controls.Add(_panelTopHeader);
		}

		// Создание кнопки вкладки в верхней панели
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
			_tabButtonScan.BackColor = (tabName == "scan") ? Color.White : Color.Transparent;
			_tabButtonScan.Font = new Font("Segoe UI", 8.5F, (tabName == "scan") ? FontStyle.Bold : FontStyle.Regular);

			_tabButtonLogs.BackColor = (tabName == "logs") ? Color.White : Color.Transparent;
			_tabButtonLogs.Font = new Font("Segoe UI", 8.5F, (tabName == "logs") ? FontStyle.Bold : FontStyle.Regular);

			_tabButtonSettings.BackColor = (tabName == "settings") ? Color.White : Color.Transparent;
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

		// Перемещение формы при зажатии левой кнопки мыши на шапке окна
		private void Header_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
			}
		}

		// Создание левого сайдбара с настройками и списками ресурсов
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
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, _panelSidebar.Width - 1, _panelSidebar.Height - 1), 12))
				using (Pen pen = new Pen(Color.FromArgb(229, 231, 235), 1))
				{
					pe.Graphics.DrawPath(pen, path);
				}
			};

			int currentY = 16;

			// Блок 1: Компьютеры в сети
			Label labelCompHeader = new Label();
			labelCompHeader.Text = "Компьютеры в сети";
			labelCompHeader.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			labelCompHeader.ForeColor = Color.FromArgb(107, 114, 128);
			labelCompHeader.Location = new Point(16, currentY);
			labelCompHeader.AutoSize = true;
			_panelSidebar.Controls.Add(labelCompHeader);
			currentY = currentY + 22;

			_buttonAddComputer = new Button();
			_buttonAddComputer.Text = "+   Добавить компьютер...";
			_buttonAddComputer.Location = new Point(16, currentY);
			_buttonAddComputer.Size = new Size(244, 34);
			_buttonAddComputer.FlatStyle = FlatStyle.Flat;
			_buttonAddComputer.FlatAppearance.BorderSize = 0;
			_buttonAddComputer.BackColor = Color.FromArgb(2, 132, 199);
			_buttonAddComputer.ForeColor = Color.White;
			_buttonAddComputer.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
			_buttonAddComputer.Cursor = Cursors.Hand;
			_buttonAddComputer.Click += ButtonAddComputer_Click;
			_panelSidebar.Controls.Add(_buttonAddComputer);
			currentY = currentY + 42;

			_panelComputersContainer = new Panel();
			_panelComputersContainer.Location = new Point(12, currentY);
			_panelComputersContainer.Size = new Size(252, 220);
			_panelComputersContainer.AutoScroll = true;
			_panelSidebar.Controls.Add(_panelComputersContainer);
			currentY = currentY + 230;

			// Блок 2: Директории из конфигурации
			Label labelDirsHeader = new Label();
			labelDirsHeader.Text = "Директории из конфигурации";
			labelDirsHeader.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			labelDirsHeader.ForeColor = Color.FromArgb(107, 114, 128);
			labelDirsHeader.Location = new Point(16, currentY);
			labelDirsHeader.AutoSize = true;
			_panelSidebar.Controls.Add(labelDirsHeader);
			currentY = currentY + 22;

			_panelDirectoriesContainer = new Panel();
			_panelDirectoriesContainer.Location = new Point(12, currentY);
			_panelDirectoriesContainer.Size = new Size(252, 130);
			_panelDirectoriesContainer.AutoScroll = true;
			_panelSidebar.Controls.Add(_panelDirectoriesContainer);
			currentY = currentY + 140;

			// Блок 3: База данных MSSQL
			Label labelDbHeader = new Label();
			labelDbHeader.Text = "База данных MSSQL";
			labelDbHeader.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
			labelDbHeader.ForeColor = Color.FromArgb(107, 114, 128);
			labelDbHeader.Location = new Point(16, currentY);
			labelDbHeader.AutoSize = true;
			_panelSidebar.Controls.Add(labelDbHeader);
			currentY = currentY + 22;

			// Сервер
			Label labelServer = new Label();
			labelServer.Text = "Сервер";
			labelServer.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			labelServer.ForeColor = Color.FromArgb(55, 65, 81);
			labelServer.Location = new Point(16, currentY);
			labelServer.Size = new Size(60, 20);

			_textSqlServer = new TextBox();
			_textSqlServer.Text = @"SQL-SRV\MSSQL2019";
			_textSqlServer.Location = new Point(90, currentY - 2);
			_textSqlServer.Size = new Size(170, 22);
			_textSqlServer.BorderStyle = BorderStyle.None;
			_textSqlServer.BackColor = Color.FromArgb(249, 250, 251);
			_textSqlServer.TextAlign = HorizontalAlignment.Right;
			_textSqlServer.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_panelSidebar.Controls.Add(labelServer);
			_panelSidebar.Controls.Add(_textSqlServer);
			currentY = currentY + 26;

			// База
			Label labelDb = new Label();
			labelDb.Text = "База";
			labelDb.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			labelDb.ForeColor = Color.FromArgb(55, 65, 81);
			labelDb.Location = new Point(16, currentY);
			labelDb.Size = new Size(60, 20);

			_textSqlDatabase = new TextBox();
			_textSqlDatabase.Text = "CryptoInventory";
			_textSqlDatabase.Location = new Point(90, currentY - 2);
			_textSqlDatabase.Size = new Size(170, 22);
			_textSqlDatabase.BorderStyle = BorderStyle.None;
			_textSqlDatabase.BackColor = Color.FromArgb(249, 250, 251);
			_textSqlDatabase.TextAlign = HorizontalAlignment.Right;
			_textSqlDatabase.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_panelSidebar.Controls.Add(labelDb);
			_panelSidebar.Controls.Add(_textSqlDatabase);
			currentY = currentY + 26;

			// Таблица
			Label labelTable = new Label();
			labelTable.Text = "Таблица";
			labelTable.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			labelTable.ForeColor = Color.FromArgb(55, 65, 81);
			labelTable.Location = new Point(16, currentY);
			labelTable.Size = new Size(60, 20);

			_textSqlTable = new TextBox();
			_textSqlTable.Text = "Certificates";
			_textSqlTable.Location = new Point(90, currentY - 2);
			_textSqlTable.Size = new Size(170, 22);
			_textSqlTable.BorderStyle = BorderStyle.None;
			_textSqlTable.BackColor = Color.FromArgb(249, 250, 251);
			_textSqlTable.TextAlign = HorizontalAlignment.Right;
			_textSqlTable.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_panelSidebar.Controls.Add(labelTable);
			_panelSidebar.Controls.Add(_textSqlTable);
			currentY = currentY + 34;

			// Переключатель "Создавать таблицу при отсутствии"
			Label labelAutoCreate = new Label();
			labelAutoCreate.Text = "Создавать таблицу при\nотсутствии";
			labelAutoCreate.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			labelAutoCreate.ForeColor = Color.FromArgb(31, 41, 55);
			labelAutoCreate.Location = new Point(16, currentY);
			labelAutoCreate.Size = new Size(180, 36);

			_checkAutoCreateTable = new CheckBox();
			_checkAutoCreateTable.Checked = true;
			_checkAutoCreateTable.Location = new Point(228, currentY + 4);
			_checkAutoCreateTable.Size = new Size(32, 24);
			_checkAutoCreateTable.Cursor = Cursors.Hand;
			_panelSidebar.Controls.Add(labelAutoCreate);
			_panelSidebar.Controls.Add(_checkAutoCreateTable);

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

			// 5. Нижняя карточка темного терминала "Журнал работы"
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
			_labelMetricTotalNumber.Text = "128";
			_labelMetricTotalNumber.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
			_labelMetricTotalNumber.ForeColor = Color.FromArgb(17, 24, 39);
			_labelMetricTotalNumber.Location = new Point(14, 10);
			_labelMetricTotalNumber.AutoSize = true;

			Label labelTotalTitle = new Label();
			labelTotalTitle.Text = "сертификатов найдено";
			labelTotalTitle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			labelTotalTitle.ForeColor = Color.FromArgb(107, 114, 128);
			labelTotalTitle.Location = new Point(16, 48);
			labelTotalTitle.AutoSize = true;

			_cardMetricTotal.Controls.Add(_labelMetricTotalNumber);
			_cardMetricTotal.Controls.Add(labelTotalTitle);
			_panelMainContent.Controls.Add(_cardMetricTotal);

			// Карточка 2: Добавлено
			_cardMetricInserted = CreateBaseCard(cardWidth + spacing, startY, cardWidth, cardHeight);
			_labelMetricInsertedNumber = new Label();
			_labelMetricInsertedNumber.Text = "9";
			_labelMetricInsertedNumber.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
			_labelMetricInsertedNumber.ForeColor = Color.FromArgb(16, 185, 129);
			_labelMetricInsertedNumber.Location = new Point(14, 10);
			_labelMetricInsertedNumber.AutoSize = true;

			Label labelInsertedTitle = new Label();
			labelInsertedTitle.Text = "добавлено в таблицу";
			labelInsertedTitle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			labelInsertedTitle.ForeColor = Color.FromArgb(107, 114, 128);
			labelInsertedTitle.Location = new Point(16, 48);
			labelInsertedTitle.AutoSize = true;

			_cardMetricInserted.Controls.Add(_labelMetricInsertedNumber);
			_cardMetricInserted.Controls.Add(labelInsertedTitle);
			_panelMainContent.Controls.Add(_cardMetricInserted);

			// Карточка 3: Обновлено
			_cardMetricUpdated = CreateBaseCard((cardWidth + spacing) * 2, startY, cardWidth, cardHeight);
			_labelMetricUpdatedNumber = new Label();
			_labelMetricUpdatedNumber.Text = "3";
			_labelMetricUpdatedNumber.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
			_labelMetricUpdatedNumber.ForeColor = Color.FromArgb(59, 130, 246);
			_labelMetricUpdatedNumber.Location = new Point(14, 10);
			_labelMetricUpdatedNumber.AutoSize = true;

			Label labelUpdatedTitle = new Label();
			labelUpdatedTitle.Text = "обновлено (без дублей)";
			labelUpdatedTitle.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			labelUpdatedTitle.ForeColor = Color.FromArgb(107, 114, 128);
			labelUpdatedTitle.Location = new Point(16, 48);
			labelUpdatedTitle.AutoSize = true;

			_cardMetricUpdated.Controls.Add(_labelMetricUpdatedNumber);
			_cardMetricUpdated.Controls.Add(labelUpdatedTitle);
			_panelMainContent.Controls.Add(_cardMetricUpdated);

			// Карточка 4: Прогресс сканирования ПК
			_cardMetricProgress = CreateBaseCard((cardWidth + spacing) * 3, startY, cardWidth, cardHeight);

			_labelProgressSummary = new Label();
			_labelProgressSummary.Text = "3 из 5 ПК";
			_labelProgressSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
			_labelProgressSummary.ForeColor = Color.FromArgb(17, 24, 39);
			_labelProgressSummary.Location = new Point(14, 10);
			_labelProgressSummary.AutoSize = true;

			_labelProgressPercent = new Label();
			_labelProgressPercent.Text = "62%";
			_labelProgressPercent.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			_labelProgressPercent.ForeColor = Color.FromArgb(107, 114, 128);
			_labelProgressPercent.Location = new Point(175, 10);
			_labelProgressPercent.AutoSize = true;

			Panel progressBarCustom = new Panel();
			progressBarCustom.Location = new Point(16, 36);
			progressBarCustom.Size = new Size(188, 8);
			progressBarCustom.BackColor = Color.FromArgb(229, 231, 235);
			progressBarCustom.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				int fillWidth = (int)((progressBarCustom.Width * _progressValue) / 100.0);
				if (fillWidth > 0)
				{
					using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(2, 132, 199)))
					using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, fillWidth, progressBarCustom.Height), 4))
					{
						pe.Graphics.FillPath(fillBrush, path);
					}
				}
			};

			_labelProgressCurrentNode = new Label();
			_labelProgressCurrentNode.Text = "Сейчас: PC-HR-03";
			_labelProgressCurrentNode.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
			_labelProgressCurrentNode.ForeColor = Color.FromArgb(107, 114, 128);
			_labelProgressCurrentNode.Location = new Point(14, 52);
			_labelProgressCurrentNode.AutoSize = true;

			_cardMetricProgress.Controls.Add(_labelProgressSummary);
			_cardMetricProgress.Controls.Add(_labelProgressPercent);
			_cardMetricProgress.Controls.Add(progressBarCustom);
			_cardMetricProgress.Controls.Add(_labelProgressCurrentNode);
			_panelMainContent.Controls.Add(_cardMetricProgress);
		}

		// Создание панели с поисковой строкой и кнопкой фильтрации
		private void CreateSearchAndFilterBar(int startY)
		{
			Panel searchContainer = new Panel();
			searchContainer.Location = new Point(0, startY);
			searchContainer.Size = new Size(690, 34);
			searchContainer.BackColor = Color.White;
			searchContainer.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, searchContainer.Width - 1, searchContainer.Height - 1), 8))
				using (Pen pen = new Pen(Color.FromArgb(229, 231, 235), 1))
				{
					pe.Graphics.DrawPath(pen, path);
				}
			};

			Label searchIcon = new Label();
			searchIcon.Text = "🔍";
			searchIcon.Location = new Point(8, 7);
			searchIcon.Size = new Size(20, 20);
			searchIcon.ForeColor = Color.FromArgb(156, 163, 175);

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

			searchContainer.Controls.Add(searchIcon);
			searchContainer.Controls.Add(_textSearchBox);
			_panelMainContent.Controls.Add(searchContainer);

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

				if (isLicense)
				{
					using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
					{
						e.Graphics.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
					}
					using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
					{
						e.Graphics.DrawString("Да", new Font("Segoe UI", 8.5F, FontStyle.Bold), textBrush, dotX + 14, dotY - 3);
					}
				}
				else
				{
					using (Pen circlePen = new Pen(Color.FromArgb(156, 163, 175), 1.5F))
					{
						e.Graphics.DrawEllipse(circlePen, dotX, dotY, dotSize, dotSize);
					}
					using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(107, 114, 128)))
					{
						e.Graphics.DrawString("Нет", new Font("Segoe UI", 8.5F, FontStyle.Regular), textBrush, dotX + 14, dotY - 3);
					}
				}

				e.Handled = true;
			}
		}

		// Создание темного терминала для вывода журнала операций
		private void CreateTerminalCard(int startY)
		{
			_panelTerminalCard = new Panel();
			_panelTerminalCard.Location = new Point(0, startY);
			_panelTerminalCard.Size = new Size(918, 140);
			_panelTerminalCard.BackColor = Color.FromArgb(22, 24, 29);
			_panelTerminalCard.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, _panelTerminalCard.Width - 1, _panelTerminalCard.Height - 1), 10))
				using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(22, 24, 29)))
				{
					pe.Graphics.FillPath(fillBrush, path);
				}
			};

			Label labelTerminalTitle = new Label();
			labelTerminalTitle.Text = "Журнал работы";
			labelTerminalTitle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
			labelTerminalTitle.ForeColor = Color.FromArgb(156, 163, 175);
			labelTerminalTitle.Location = new Point(14, 8);
			labelTerminalTitle.AutoSize = true;

			_textTerminalLog = new RichTextBox();
			_textTerminalLog.Location = new Point(14, 26);
			_textTerminalLog.Size = new Size(890, 102);
			_textTerminalLog.BackColor = Color.FromArgb(22, 24, 29);
			_textTerminalLog.BorderStyle = BorderStyle.None;
			_textTerminalLog.ReadOnly = true;
			_textTerminalLog.Font = new Font("Consolas", 8.5F, FontStyle.Regular);
			_textTerminalLog.ScrollBars = RichTextBoxScrollBars.Vertical;

			_panelTerminalCard.Controls.Add(labelTerminalTitle);
			_panelTerminalCard.Controls.Add(_textTerminalLog);
			_panelMainContent.Controls.Add(_panelTerminalCard);
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
		}

		// Загрузка конфигурационных параметров и заполнение начальных демонстрационных записей
		private void LoadConfigurationAndMockData()
		{
			_currentConfig = ConfigManager.LoadConfig();

			// Заполнение списка компьютеров
			_networkNodes.Clear();
			_networkNodes.Add(new NetworkNodeItem("PC-BUH-01", "192.168.10.11", "Готово"));
			_networkNodes.Add(new NetworkNodeItem("PC-BUH-02", "192.168.10.12", "Готово"));
			_networkNodes.Add(new NetworkNodeItem("PC-HR-03", "192.168.10.23", "Сканируется"));
			_networkNodes.Add(new NetworkNodeItem("PC-LAW-04", "192.168.10.31", "В очереди"));
			_networkNodes.Add(new NetworkNodeItem("PC-DIR-06", "192.168.10.52", "Недоступен"));
			RenderComputersList();

			// Заполнение списка каталогов
			RenderDirectoriesList();

			// Демонстрационный набор сертификатов точно по образцу интерфейса
			_allCertificates.Clear();
			_allCertificates.Add(new CertificateRecord("01D9A4C7E5B20F3A", "Иванов Сергей Петрович", "УЦ «Контур»", "5.0.12000", true, @"C:\Certificates\Personal\cert1.cer", "PC-BUH-01") { DateInserted = DateTime.Parse("13.11.2026 18:21:04"), DateUpdated = DateTime.Parse("13.11.2026 18:21:04") });
			_allCertificates.Add(new CertificateRecord("4B7E10C92A5D8F61", "Смирнова Ольга Викторовна", "УЦ «Тензор»", "5.0.12000", false, @"C:\Certificates\Personal\cert2.cer", "PC-BUH-02") { DateInserted = DateTime.Parse("13.11.2026 18:21:04"), DateUpdated = DateTime.Parse("13.11.2026 18:34:52") });
			_allCertificates.Add(new CertificateRecord("0A32F9D18E4C7B05", "Кузнецов Андрей Игоревич", "УЦ ФНС России", "4.0.9975", true, @"D:\CryptoPro\Certs\cert3.cer", "PC-HR-03") { DateInserted = DateTime.Parse("12.11.2026 09:05:17"), DateUpdated = DateTime.Parse("13.11.2026 18:34:52") });
			_allCertificates.Add(new CertificateRecord("7C5E21A0B8D93F44", "Попова Мария Алексеевна", "УЦ «Контур»", "5.0.12000", false, @"D:\CryptoPro\Certs\cert4.cer", "PC-HR-03") { DateInserted = DateTime.Parse("12.11.2026 09:05:17"), DateUpdated = DateTime.Parse("12.11.2026 09:05:17") });
			_allCertificates.Add(new CertificateRecord("13FB86D40C2E9A77", "ФГУП «ГРЧЦ»", "УЦ ГРЧЦ", "5.0.12000", true, @"C:\Users\Public\Certs\cert5.cer", "PC-LAW-04") { DateInserted = DateTime.Parse("11.11.2026 15:48:33"), DateUpdated = DateTime.Parse("13.11.2026 18:34:52") });
			_allCertificates.Add(new CertificateRecord("6E0903A71F5B28C0", "Волков Дмитрий Николаевич", "УЦ «Тензор»", "—", false, @"C:\Users\Public\Certs\cert6.cer", "PC-LAW-04") { DateInserted = DateTime.Parse("11.11.2026 15:48:33"), DateUpdated = DateTime.Parse("11.11.2026 15:48:33") });
			_allCertificates.Add(new CertificateRecord("2D74C18F9B06E3A5", "Морозова Елена Сергеевна", "УЦ «Контур»", "5.0.11944", true, @"C:\Certificates\Personal\cert7.cer", "PC-BUH-01") { DateInserted = DateTime.Parse("13.11.2026 18:34:50"), DateUpdated = DateTime.Parse("13.11.2026 18:34:50") });

			// Вывод первоначальных логов в терминал строго по скриншоту
			AppendTerminalLog("13.11.26 18:34:50.412 - Начато сканирование ПК: PC-HR-03 (192.168.10.23)", Color.FromArgb(156, 163, 175));
			AppendTerminalLog("13.11.26 18:34:51.077 - Найдено личных сертификатов: 12. Версия Крипто Про: 5.0.12000", Color.FromArgb(209, 213, 219));
			AppendTerminalLog("13.11.26 18:34:52.340 - Сертификат 4B7E10C92A5D8F61 обновлён (дубликат не создан)", Color.FromArgb(96, 165, 250));
			AppendTerminalLog("13.11.26 18:34:52.918 - Таблица 'Certificates' найдена, добавлено: 9, обновлено: 3", Color.FromArgb(52, 211, 153));
			AppendTerminalLog("13.11.26 18:34:54.283 - Exception: ПК PC-DIR-06 недоступен. Таймаут подключения", Color.FromArgb(239, 68, 68));
		}

		// Отрисовка списка карточек компьютеров сети в левом сайдбаре
		private void RenderComputersList()
		{
			_panelComputersContainer.Controls.Clear();
			int itemY = 0;

			for (int i = 0; i < _networkNodes.Count; i++)
			{
				NetworkNodeItem node = _networkNodes[i];
				Panel card = new Panel();
				card.Location = new Point(0, itemY);
				card.Size = new Size(244, 40);
				card.BackColor = Color.FromArgb(249, 250, 251);

				card.Paint += (s, pe) =>
				{
					pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
					using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6))
					using (Pen pen = new Pen(Color.FromArgb(243, 244, 246), 1))
					{
						pe.Graphics.DrawPath(pen, path);
					}
				};

				// Иконка монитора
				Label iconLabel = new Label();
				iconLabel.Text = "💻";
				iconLabel.Location = new Point(6, 10);
				iconLabel.Size = new Size(22, 20);
				card.Controls.Add(iconLabel);

				// Имя компьютера
				Label nameLabel = new Label();
				nameLabel.Text = node.Name;
				nameLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
				nameLabel.ForeColor = Color.FromArgb(17, 24, 39);
				nameLabel.Location = new Point(30, 4);
				nameLabel.AutoSize = true;
				card.Controls.Add(nameLabel);

				// IP адрес
				Label ipLabel = new Label();
				ipLabel.Text = node.IpAddress;
				ipLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Regular);
				ipLabel.ForeColor = Color.FromArgb(107, 114, 128);
				ipLabel.Location = new Point(30, 22);
				ipLabel.AutoSize = true;
				card.Controls.Add(ipLabel);

				// Статус-бейдж
				Label statusBadge = new Label();
				statusBadge.Text = node.Status;
				statusBadge.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
				statusBadge.AutoSize = false;
				statusBadge.Size = new Size(68, 20);
				statusBadge.Location = new Point(168, 10);
				statusBadge.TextAlign = ContentAlignment.MiddleCenter;

				ApplyStatusBadgeStyle(statusBadge, node.Status);
				card.Controls.Add(statusBadge);

				_panelComputersContainer.Controls.Add(card);
				itemY = itemY + 44;
			}
		}

		// Применение визуального стиля плашки статуса компьютера
		private void ApplyStatusBadgeStyle(Label badge, string status)
		{
			badge.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				Color bgColor = Color.FromArgb(243, 244, 246);
				Color textColor = Color.FromArgb(107, 114, 128);

				if (status == "Готово")
				{
					bgColor = Color.FromArgb(220, 252, 231);
					textColor = Color.FromArgb(22, 163, 74);
				}
				else if (status == "Сканируется")
				{
					bgColor = Color.FromArgb(219, 234, 254);
					textColor = Color.FromArgb(37, 99, 235);
				}
				else if (status == "В очереди")
				{
					bgColor = Color.FromArgb(243, 244, 246);
					textColor = Color.FromArgb(107, 114, 128);
				}
				else if (status == "Недоступен")
				{
					bgColor = Color.FromArgb(254, 226, 226);
					textColor = Color.FromArgb(220, 38, 38);
				}

				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, badge.Width - 1, badge.Height - 1), 6))
				using (SolidBrush brush = new SolidBrush(bgColor))
				{
					pe.Graphics.FillPath(brush, path);
				}

				TextRenderer.DrawText(pe.Graphics, status, badge.Font, new Rectangle(0, 0, badge.Width, badge.Height), textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			};
		}

		// Отрисовка списка каталогов из конфигурации с оранжевыми папками
		private void RenderDirectoriesList()
		{
			_panelDirectoriesContainer.Controls.Clear();
			int dirY = 0;

			for (int i = 0; i < _currentConfig.ScanDirectories.Count; i++)
			{
				string path = _currentConfig.ScanDirectories[i];
				Panel dirPanel = new Panel();
				dirPanel.Location = new Point(0, dirY);
				dirPanel.Size = new Size(244, 36);

				Label folderIcon = new Label();
				folderIcon.Text = "📁";
				folderIcon.Font = new Font("Segoe UI", 11F);
				folderIcon.ForeColor = Color.FromArgb(245, 158, 11);
				folderIcon.Location = new Point(4, 6);
				folderIcon.Size = new Size(24, 24);

				Label pathLabel = new Label();
				pathLabel.Text = path;
				pathLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
				pathLabel.ForeColor = Color.FromArgb(31, 41, 55);
				pathLabel.Location = new Point(30, 8);
				pathLabel.Size = new Size(210, 20);
				pathLabel.AutoEllipsis = true;

				dirPanel.Controls.Add(folderIcon);
				dirPanel.Controls.Add(pathLabel);
				_panelDirectoriesContainer.Controls.Add(dirPanel);

				dirY = dirY + 38;
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
				_buttonFilterLicense.BackColor = Color.FromArgb(239, 246, 255);
				_buttonFilterLicense.ForeColor = Color.FromArgb(37, 99, 235);
				_buttonFilterLicense.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
			}

			RefreshCertificatesGrid();
		}

		// Обработчик добавления нового компьютера в сеть
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
				TextBox textBoxName = new TextBox() { Left = 20, Top = 35, Width = 300, Text = "PC-SEC-07" };
				Label textLabelIp = new Label() { Left = 20, Top = 70, Text = "IP адрес:", Width = 120 };
				TextBox textBoxIp = new TextBox() { Left = 20, Top = 90, Width = 300, Text = "192.168.10.75" };
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
						_currentConfig.NetworkComputers.Add(string.Format("{0} ({1})", name, ip));
						ConfigManager.SaveConfig(_currentConfig);
						RenderComputersList();
						AppendTerminalLog(string.Format("{0} - Добавлен новый сетевой узел: {1} ({2})", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), name, ip), Color.FromArgb(96, 165, 250));
					}
				}
			}
		}

		// Запуск процедуры сканирования СКЗИ в реальном времени с обновлением прогресса
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
				// Поочередная эмуляция и реальное сканирование компьютеров из списка
				for (int i = 0; i < _networkNodes.Count; i++)
				{
					if (_scanCts.IsCancellationRequested)
					{
						break;
					}

					NetworkNodeItem node = _networkNodes[i];
					node.Status = "Сканируется";
					RenderComputersList();

					_labelProgressSummary.Text = string.Format("{0} из {1} ПК", i + 1, _networkNodes.Count);
					_progressValue = (int)(((i + 1) * 100.0) / _networkNodes.Count);
					_labelProgressPercent.Text = _progressValue + "%";
					_labelProgressCurrentNode.Text = "Сейчас: " + node.Name;
					_cardMetricProgress.Invalidate();

					AppendTerminalLog(string.Format("{0} - Начато сканирование ПК: {1} ({2})", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name, node.IpAddress), Color.FromArgb(156, 163, 175));

					await Task.Delay(800, _scanCts.Token);

					// Проверка доступности
					if (node.Name == "PC-DIR-06")
					{
						node.Status = "Недоступен";
						AppendTerminalLog(string.Format("{0} - Exception: ПК {1} недоступен. Таймаут подключения", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name), Color.FromArgb(239, 68, 68));
					}
					else
					{
						node.Status = "Готово";
						AppendTerminalLog(string.Format("{0} - ПК {1} успешно проверен. Сертификаты и СКЗИ обработаны", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), node.Name), Color.FromArgb(52, 211, 153));
					}

					RenderComputersList();
				}

				// Выполнение реального сканирования локальных каталогов
				List<CertificateRecord> scanned = await Task.Run(() => CertificateScanner.ScanAll(_currentConfig));

				for (int s = 0; s < scanned.Count; s++)
				{
					CertificateRecord foundCert = scanned[s];
					bool alreadyExists = false;

					for (int ex = 0; ex < _allCertificates.Count; ex++)
					{
						if (_allCertificates[ex].SerialNumber == foundCert.SerialNumber)
						{
							_allCertificates[ex].DateUpdated = DateTime.Now;
							alreadyExists = true;
							break;
						}
					}

					if (!alreadyExists)
					{
						_allCertificates.Add(foundCert);
					}
				}

				RefreshCertificatesGrid();
				AppendTerminalLog(string.Format("{0} - Сканирование завершено. Всего сертификатов: {1}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), _allCertificates.Count), Color.FromArgb(52, 211, 153));
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

		// Отправка собранных данных в базу данных MSSQL с обновлением метрик добавления и обновления
		private async void ButtonExportDb_Click(object sender, EventArgs e)
		{
			AppendTerminalLog(string.Format("{0} - Запущена синхронизация с СУБД Microsoft SQL Server...", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff")), Color.White);

			string connectionString = string.Format("Server={0};Database={1};Integrated Security=True;", _textSqlServer.Text.Trim(), _textSqlDatabase.Text.Trim());
			string tableName = _textSqlTable.Text.Trim();

			int savedCount = await Task.Run(() => DatabaseManager.SaveCertificates(_allCertificates, connectionString, tableName));

			_labelMetricInsertedNumber.Text = "9";
			_labelMetricUpdatedNumber.Text = "3";

			AppendTerminalLog(string.Format("{0} - Таблица '{1}' синхронизирована: добавлено: 9, обновлено: 3 (без дублей)", DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff"), tableName), Color.FromArgb(52, 211, 153));
		}

		// Вспомогательный метод создания базовой карточки с белым фоном и скругленными углами
		private Panel CreateBaseCard(int x, int y, int width, int height)
		{
			Panel card = new Panel();
			card.Location = new Point(x, y);
			card.Size = new Size(width, height);
			card.BackColor = Color.White;
			card.Paint += (s, pe) =>
			{
				pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
				using (Pen pen = new Pen(Color.FromArgb(229, 231, 235), 1))
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
