using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CertHarvester
{
	/// <summary>
	/// Форма графической панели управления для настройки параметров и запуска сканирования (Зона ответственности: Участник 3)
	/// </summary>
	public class ControlPanelForm : Form
	{
		private AppConfig _currentConfig;

		// Элементы управления для директорий сканирования
		private ListBox _listDirectories;
		private TextBox _textDirectoryInput;
		private Button _buttonBrowseDirectory;
		private Button _buttonAddDirectory;
		private Button _buttonRemoveDirectory;

		// Элементы управления для списка компьютеров
		private ListBox _listComputers;
		private TextBox _textComputerInput;
		private Button _buttonAddComputer;
		private Button _buttonRemoveComputer;

		// Элементы управления для подключения к БД
		private TextBox _textConnectionString;
		private Button _buttonTestDb;

		// Общие кнопки управления
		private Button _buttonSaveConfig;
		private Button _buttonRunScan;

		// Конструктор формы панели управления
		public ControlPanelForm()
		{
			// TODO (Участник 3): Вынести разметку компонентов в ControlPanelForm.Designer.cs при переходе к визуальному дизайнеру VS
			InitializeComponentCustom();
			LoadConfigurationData();
		}

		// Инициализация и размещение визуальных элементов формы Windows Forms
		private void InitializeComponentCustom()
		{
			this.Text = "CertHarvester - Панель управления СКЗИ";
			this.Size = new Size(720, 560);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;

			// 1. Группа директорий для сканирования
			GroupBox groupDirs = new GroupBox();
			groupDirs.Text = "Директории для сканирования сертификатов (.cer, .crt)";
			groupDirs.Location = new Point(12, 12);
			groupDirs.Size = new Size(680, 150);

			_listDirectories = new ListBox();
			_listDirectories.Location = new Point(15, 25);
			_listDirectories.Size = new Size(530, 85);

			_textDirectoryInput = new TextBox();
			_textDirectoryInput.Location = new Point(15, 115);
			_textDirectoryInput.Size = new Size(350, 22);

			_buttonBrowseDirectory = new Button();
			_buttonBrowseDirectory.Text = "Обзор...";
			_buttonBrowseDirectory.Location = new Point(370, 113);
			_buttonBrowseDirectory.Size = new Size(85, 26);
			_buttonBrowseDirectory.Click += ButtonBrowseDirectory_Click;

			_buttonAddDirectory = new Button();
			_buttonAddDirectory.Text = "Добавить";
			_buttonAddDirectory.Location = new Point(460, 113);
			_buttonAddDirectory.Size = new Size(85, 26);
			_buttonAddDirectory.Click += ButtonAddDirectory_Click;

			_buttonRemoveDirectory = new Button();
			_buttonRemoveDirectory.Text = "Удалить";
			_buttonRemoveDirectory.Location = new Point(555, 25);
			_buttonRemoveDirectory.Size = new Size(110, 30);
			_buttonRemoveDirectory.Click += ButtonRemoveDirectory_Click;

			groupDirs.Controls.Add(_listDirectories);
			groupDirs.Controls.Add(_textDirectoryInput);
			groupDirs.Controls.Add(_buttonBrowseDirectory);
			groupDirs.Controls.Add(_buttonAddDirectory);
			groupDirs.Controls.Add(_buttonRemoveDirectory);

			// 2. Группа компьютеров сети
			GroupBox groupComputers = new GroupBox();
			groupComputers.Text = "Список компьютеров в локальной сети";
			groupComputers.Location = new Point(12, 170);
			groupComputers.Size = new Size(680, 140);

			_listComputers = new ListBox();
			_listComputers.Location = new Point(15, 25);
			_listComputers.Size = new Size(530, 75);

			_textComputerInput = new TextBox();
			_textComputerInput.Location = new Point(15, 105);
			_textComputerInput.Size = new Size(440, 22);

			_buttonAddComputer = new Button();
			_buttonAddComputer.Text = "Добавить ПК";
			_buttonAddComputer.Location = new Point(460, 103);
			_buttonAddComputer.Size = new Size(85, 26);
			_buttonAddComputer.Click += ButtonAddComputer_Click;

			_buttonRemoveComputer = new Button();
			_buttonRemoveComputer.Text = "Удалить ПК";
			_buttonRemoveComputer.Location = new Point(555, 25);
			_buttonRemoveComputer.Size = new Size(110, 30);
			_buttonRemoveComputer.Click += ButtonRemoveComputer_Click;

			groupComputers.Controls.Add(_listComputers);
			groupComputers.Controls.Add(_textComputerInput);
			groupComputers.Controls.Add(_buttonAddComputer);
			groupComputers.Controls.Add(_buttonRemoveComputer);

			// 3. Группа настройки БД MSSQL
			GroupBox groupDb = new GroupBox();
			groupDb.Text = "Подключение к Microsoft SQL Server 2019";
			groupDb.Location = new Point(12, 320);
			groupDb.Size = new Size(680, 100);

			Label labelConn = new Label();
			labelConn.Text = "Строка подключения:";
			labelConn.Location = new Point(15, 25);
			labelConn.Size = new Size(200, 18);

			_textConnectionString = new TextBox();
			_textConnectionString.Location = new Point(15, 45);
			_textConnectionString.Size = new Size(530, 22);

			_buttonTestDb = new Button();
			_buttonTestDb.Text = "Проверить связь";
			_buttonTestDb.Location = new Point(555, 43);
			_buttonTestDb.Size = new Size(110, 26);
			_buttonTestDb.Click += ButtonTestDb_Click;

			groupDb.Controls.Add(labelConn);
			groupDb.Controls.Add(_textConnectionString);
			groupDb.Controls.Add(_buttonTestDb);

			// 4. Кнопки нижнего бара
			_buttonSaveConfig = new Button();
			_buttonSaveConfig.Text = "Сохранить настройки";
			_buttonSaveConfig.Location = new Point(12, 440);
			_buttonSaveConfig.Size = new Size(180, 36);
			_buttonSaveConfig.Click += ButtonSaveConfig_Click;

			_buttonRunScan = new Button();
			_buttonRunScan.Text = "Запустить сканирование";
			_buttonRunScan.Location = new Point(480, 440);
			_buttonRunScan.Size = new Size(212, 36);
			_buttonRunScan.Font = new Font(this.Font, FontStyle.Bold);
			_buttonRunScan.Click += ButtonRunScan_Click;

			this.Controls.Add(groupDirs);
			this.Controls.Add(groupComputers);
			this.Controls.Add(groupDb);
			this.Controls.Add(_buttonSaveConfig);
			this.Controls.Add(_buttonRunScan);
		}

		// Загрузка сохраненной конфигурации и заполнение компонентов пользовательского интерфейса
		private void LoadConfigurationData()
		{
			// TODO (Участник 3): Добавить блокировку интерфейса на время асинхронной загрузки данных
			_currentConfig = ConfigManager.LoadConfig();

			_listDirectories.Items.Clear();
			for (int i = 0; i < _currentConfig.ScanDirectories.Count; i++)
			{
				_listDirectories.Items.Add(_currentConfig.ScanDirectories[i]);
			}

			_listComputers.Items.Clear();
			for (int j = 0; j < _currentConfig.NetworkComputers.Count; j++)
			{
				_listComputers.Items.Add(_currentConfig.NetworkComputers[j]);
			}

			_textConnectionString.Text = _currentConfig.ConnectionString;
		}

		// Обработчик диалогового окна выбора директории в проводнике Windows
		private void ButtonBrowseDirectory_Click(object sender, EventArgs e)
		{
			using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
			{
				folderDialog.Description = "Выберите каталог для поиска сертификатов";
				if (folderDialog.ShowDialog() == DialogResult.OK)
				{
					_textDirectoryInput.Text = folderDialog.SelectedPath;
				}
			}
		}

		// Обработчик добавления нового локального или сетевого пути в список сканирования
		private void ButtonAddDirectory_Click(object sender, EventArgs e)
		{
			// TODO (Участник 3): Добавить проверку корректности формата пути и предотвращение дублирования
			string newPath = _textDirectoryInput.Text.Trim();
			if (!string.IsNullOrEmpty(newPath))
			{
				_listDirectories.Items.Add(newPath);
				_textDirectoryInput.Clear();
			}
		}

		// Обработчик удаления выбранной директории из списка сканирования
		private void ButtonRemoveDirectory_Click(object sender, EventArgs e)
		{
			// TODO (Участник 3): Добавить подтверждение удаления элемента пользователем
			if (_listDirectories.SelectedIndex >= 0)
			{
				_listDirectories.Items.RemoveAt(_listDirectories.SelectedIndex);
			}
		}

		// Обработчик добавления имени или IP-адреса компьютера в сетевой список
		private void ButtonAddComputer_Click(object sender, EventArgs e)
		{
			// TODO (Участник 3): Реализовать валидацию имени хоста и проверку резолва DNS
			string newComputer = _textComputerInput.Text.Trim();
			if (!string.IsNullOrEmpty(newComputer))
			{
				_listComputers.Items.Add(newComputer);
				_textComputerInput.Clear();
			}
		}

		// Обработчик удаления выбранного компьютера из сетевого списка
		private void ButtonRemoveComputer_Click(object sender, EventArgs e)
		{
			if (_listComputers.SelectedIndex >= 0)
			{
				_listComputers.Items.RemoveAt(_listComputers.SelectedIndex);
			}
		}

		// Обработчик тестирования соединения с базой данных MSSQL
		private void ButtonTestDb_Click(object sender, EventArgs e)
		{
			string connStr = _textConnectionString.Text.Trim();
			bool isConnected = DatabaseManager.TestConnection(connStr);

			if (isConnected)
			{
				MessageBox.Show(this, "Подключение к Microsoft SQL Server успешно установлено!", "Проверка связи", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			else
			{
				MessageBox.Show(this, "Не удалось подключиться к MSSQL. Проверьте строку подключения и доступность службы.", "Ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// Обработчик сохранения актуальных параметров конфигурации в файл
		private void ButtonSaveConfig_Click(object sender, EventArgs e)
		{
			SyncConfigFromUi();
			bool isSaved = ConfigManager.SaveConfig(_currentConfig);

			if (isSaved)
			{
				MessageBox.Show(this, "Конфигурация успешно сохранена в файл config.json", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			else
			{
				MessageBox.Show(this, "Ошибка сохранения файла конфигурации.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// Обработчик запуска консольного сканирования сертификатов из графической панели
		private void ButtonRunScan_Click(object sender, EventArgs e)
		{
			// Сохраняем текущие настройки перед сканированием
			SyncConfigFromUi();
			ConfigManager.SaveConfig(_currentConfig);

			Logger.LogInfo("Запуск сканирования сертификатов из графической панели управления");

			// Выполнение сканирования и отправки данных в MSSQL
			System.Collections.Generic.List<CertificateRecord> records = CertificateScanner.ScanAll(_currentConfig);
			int savedCount = DatabaseManager.SaveCertificates(records, _currentConfig.ConnectionString, _currentConfig.TableName);

			string summaryMessage = string.Format("Сканирование завершено!\nНайдено сертификатов: {0}\nСинхронизировано в БД: {1}\n\nПодробности записаны в файл лога.", records.Count, savedCount);
			MessageBox.Show(this, summaryMessage, "Итоги сканирования СКЗИ", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		// Синхронизация значений из элементов формы в текущий объект конфигурации
		private void SyncConfigFromUi()
		{
			_currentConfig.ScanDirectories.Clear();
			for (int i = 0; i < _listDirectories.Items.Count; i++)
			{
				_currentConfig.ScanDirectories.Add(_listDirectories.Items[i].ToString());
			}

			_currentConfig.NetworkComputers.Clear();
			for (int j = 0; j < _listComputers.Items.Count; j++)
			{
				_currentConfig.NetworkComputers.Add(_listComputers.Items[j].ToString());
			}

			_currentConfig.ConnectionString = _textConnectionString.Text.Trim();
		}
	}
}
