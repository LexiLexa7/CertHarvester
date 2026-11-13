using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CertHarvester
{
	/// <summary>
	/// Форма графической панели управления для настройки параметров и запуска сканирования СКЗИ
	/// </summary>
	public class ControlPanelForm : Form
	{
		private AppConfig _currentConfig;

		// Элементы управления для работы со списком директорий
		private ListBox _listDirectories;
		private TextBox _textDirectoryInput;
		private Button _buttonBrowseDirectory;
		private Button _buttonAddDirectory;
		private Button _buttonRemoveDirectory;

		// Элементы управления для работы со списком компьютеров локальной сети
		private ListBox _listComputers;
		private TextBox _textComputerInput;
		private Button _buttonAddComputer;
		private Button _buttonRemoveComputer;

		// Элементы управления для параметров базы данных MSSQL
		private TextBox _textConnectionString;
		private TextBox _textTableName;
		private Button _buttonTestDb;

		// Кнопки управления операциями формы
		private Button _buttonSaveConfig;
		private Button _buttonRunScan;

		// Конструктор формы графической панели управления
		public ControlPanelForm()
		{
			InitializeComponentCustom();
			LoadConfigurationData();
		}

		// Инициализация и компоновка визуальных элементов окна настроек Windows Forms
		private void InitializeComponentCustom()
		{
			this.Text = "CertHarvester - Панель управления сбором СКЗИ";
			this.Size = new Size(740, 590);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;

			// 1. Группа каталогов для сканирования сертификатов
			GroupBox groupDirs = new GroupBox();
			groupDirs.Text = "Директории для сканирования сертификатов (.cer, .crt)";
			groupDirs.Location = new Point(14, 12);
			groupDirs.Size = new Size(696, 150);

			_listDirectories = new ListBox();
			_listDirectories.Location = new Point(16, 24);
			_listDirectories.Size = new Size(540, 84);

			_textDirectoryInput = new TextBox();
			_textDirectoryInput.Location = new Point(16, 114);
			_textDirectoryInput.Size = new Size(350, 22);

			_buttonBrowseDirectory = new Button();
			_buttonBrowseDirectory.Text = "Обзор...";
			_buttonBrowseDirectory.Location = new Point(372, 112);
			_buttonBrowseDirectory.Size = new Size(88, 26);
			_buttonBrowseDirectory.Click += ButtonBrowseDirectory_Click;

			_buttonAddDirectory = new Button();
			_buttonAddDirectory.Text = "Добавить";
			_buttonAddDirectory.Location = new Point(466, 112);
			_buttonAddDirectory.Size = new Size(90, 26);
			_buttonAddDirectory.Click += ButtonAddDirectory_Click;

			_buttonRemoveDirectory = new Button();
			_buttonRemoveDirectory.Text = "Удалить путь";
			_buttonRemoveDirectory.Location = new Point(568, 24);
			_buttonRemoveDirectory.Size = new Size(114, 30);
			_buttonRemoveDirectory.Click += ButtonRemoveDirectory_Click;

			groupDirs.Controls.Add(_listDirectories);
			groupDirs.Controls.Add(_textDirectoryInput);
			groupDirs.Controls.Add(_buttonBrowseDirectory);
			groupDirs.Controls.Add(_buttonAddDirectory);
			groupDirs.Controls.Add(_buttonRemoveDirectory);

			// 2. Группа компьютеров локальной сети
			GroupBox groupComputers = new GroupBox();
			groupComputers.Text = "Список компьютеров в локальной сети";
			groupComputers.Location = new Point(14, 170);
			groupComputers.Size = new Size(696, 140);

			_listComputers = new ListBox();
			_listComputers.Location = new Point(16, 24);
			_listComputers.Size = new Size(540, 74);

			_textComputerInput = new TextBox();
			_textComputerInput.Location = new Point(16, 104);
			_textComputerInput.Size = new Size(444, 22);

			_buttonAddComputer = new Button();
			_buttonAddComputer.Text = "Добавить ПК";
			_buttonAddComputer.Location = new Point(466, 102);
			_buttonAddComputer.Size = new Size(90, 26);
			_buttonAddComputer.Click += ButtonAddComputer_Click;

			_buttonRemoveComputer = new Button();
			_buttonRemoveComputer.Text = "Удалить ПК";
			_buttonRemoveComputer.Location = new Point(568, 24);
			_buttonRemoveComputer.Size = new Size(114, 30);
			_buttonRemoveComputer.Click += ButtonRemoveComputer_Click;

			groupComputers.Controls.Add(_listComputers);
			groupComputers.Controls.Add(_textComputerInput);
			groupComputers.Controls.Add(_buttonAddComputer);
			groupComputers.Controls.Add(_buttonRemoveComputer);

			// 3. Группа подключения к СУБД Microsoft SQL Server 2019
			GroupBox groupDb = new GroupBox();
			groupDb.Text = "Подключение к Microsoft SQL Server 2019";
			groupDb.Location = new Point(14, 318);
			groupDb.Size = new Size(696, 130);

			Label labelConn = new Label();
			labelConn.Text = "Строка подключения к MSSQL:";
			labelConn.Location = new Point(16, 24);
			labelConn.Size = new Size(240, 18);

			_textConnectionString = new TextBox();
			_textConnectionString.Location = new Point(16, 44);
			_textConnectionString.Size = new Size(540, 22);

			_buttonTestDb = new Button();
			_buttonTestDb.Text = "Проверить связь";
			_buttonTestDb.Location = new Point(568, 42);
			_buttonTestDb.Size = new Size(114, 26);
			_buttonTestDb.Click += ButtonTestDb_Click;

			Label labelTable = new Label();
			labelTable.Text = "Имя таблицы для сохранения данных сертификатов:";
			labelTable.Location = new Point(16, 74);
			labelTable.Size = new Size(320, 18);

			_textTableName = new TextBox();
			_textTableName.Location = new Point(16, 94);
			_textTableName.Size = new Size(240, 22);

			groupDb.Controls.Add(labelConn);
			groupDb.Controls.Add(_textConnectionString);
			groupDb.Controls.Add(_buttonTestDb);
			groupDb.Controls.Add(labelTable);
			groupDb.Controls.Add(_textTableName);

			// 4. Панель кнопок действий
			_buttonSaveConfig = new Button();
			_buttonSaveConfig.Text = "Сохранить настройки";
			_buttonSaveConfig.Location = new Point(14, 466);
			_buttonSaveConfig.Size = new Size(190, 40);
			_buttonSaveConfig.Click += ButtonSaveConfig_Click;

			_buttonRunScan = new Button();
			_buttonRunScan.Text = "Запустить сканирование";
			_buttonRunScan.Location = new Point(470, 466);
			_buttonRunScan.Size = new Size(240, 40);
			_buttonRunScan.Font = new Font(this.Font, FontStyle.Bold);
			_buttonRunScan.Click += ButtonRunScan_Click;

			this.Controls.Add(groupDirs);
			this.Controls.Add(groupComputers);
			this.Controls.Add(groupDb);
			this.Controls.Add(_buttonSaveConfig);
			this.Controls.Add(_buttonRunScan);
		}

		// Загрузка параметров конфигурации и отображение значений в элементах формы
		private void LoadConfigurationData()
		{
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
			_textTableName.Text = string.IsNullOrEmpty(_currentConfig.TableName) ? "Certificates" : _currentConfig.TableName;
		}

		// Отображение диалогового окна выбора каталога через стандартный FolderBrowserDialog
		private void ButtonBrowseDirectory_Click(object sender, EventArgs e)
		{
			using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
			{
				folderDialog.Description = "Выберите каталог для поиска сертификатов открытого ключа";
				folderDialog.ShowNewFolderButton = false;

				if (folderDialog.ShowDialog(this) == DialogResult.OK)
				{
					_textDirectoryInput.Text = folderDialog.SelectedPath;
				}
			}
		}

		// Добавление введенного пути директории в общий список сканирования
		private void ButtonAddDirectory_Click(object sender, EventArgs e)
		{
			string newPath = _textDirectoryInput.Text.Trim();
			if (!string.IsNullOrEmpty(newPath))
			{
				if (!_listDirectories.Items.Contains(newPath))
				{
					_listDirectories.Items.Add(newPath);
					_textDirectoryInput.Clear();
				}
				else
				{
					MessageBox.Show(this, "Данная директория уже присутствует в списке сканирования.", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
		}

		// Удаление выделенного элемента директории из списка сканирования
		private void ButtonRemoveDirectory_Click(object sender, EventArgs e)
		{
			if (_listDirectories.SelectedIndex >= 0)
			{
				_listDirectories.Items.RemoveAt(_listDirectories.SelectedIndex);
			}
			else
			{
				MessageBox.Show(this, "Пожалуйста, выберите элемент из списка для удаления.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		// Добавление имени или сетевого адреса рабочей станции в список сканируемых ПК
		private void ButtonAddComputer_Click(object sender, EventArgs e)
		{
			string newComputer = _textComputerInput.Text.Trim();
			if (!string.IsNullOrEmpty(newComputer))
			{
				if (!_listComputers.Items.Contains(newComputer))
				{
					_listComputers.Items.Add(newComputer);
					_textComputerInput.Clear();
				}
				else
				{
					MessageBox.Show(this, "Данный компьютер уже присутствует в списке сканирования.", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
		}

		// Удаление выделенного имени компьютера из списка сетевого сканирования
		private void ButtonRemoveComputer_Click(object sender, EventArgs e)
		{
			if (_listComputers.SelectedIndex >= 0)
			{
				_listComputers.Items.RemoveAt(_listComputers.SelectedIndex);
			}
			else
			{
				MessageBox.Show(this, "Пожалуйста, выберите компьютер из списка для удаления.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		// Проверка соединения с базой данных Microsoft SQL Server по введенной строке подключения
		private void ButtonTestDb_Click(object sender, EventArgs e)
		{
			string connStr = _textConnectionString.Text.Trim();
			bool isConnected = DatabaseManager.TestConnection(connStr);

			if (isConnected == true)
			{
				MessageBox.Show(this, "Подключение к Microsoft SQL Server успешно установлено!", "Проверка связи с БД", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			else
			{
				MessageBox.Show(this, "Не удалось подключиться к СУБД MSSQL.\nПроверьте строку подключения, доступность сервера и учетные данные.", "Сбой подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// Сохранение отредактированных пользователем параметров конфигурации в файл config.json
		private void ButtonSaveConfig_Click(object sender, EventArgs e)
		{
			SyncConfigFromUi();
			bool isSaved = ConfigManager.SaveConfig(_currentConfig);

			if (isSaved == true)
			{
				MessageBox.Show(this, "Конфигурация успешно сохранена в файл config.json", "Сохранение параметров", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			else
			{
				MessageBox.Show(this, "Не удалось сохранить файл конфигурации.", "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// Запуск процедуры сканирования сертификатов и синхронизации с базой данных из графического окна
		private void ButtonRunScan_Click(object sender, EventArgs e)
		{
			SyncConfigFromUi();
			ConfigManager.SaveConfig(_currentConfig);

			Logger.LogInfo("Запуск процедуры сканирования сертификатов по кнопке графической панели управления");

			List<CertificateRecord> records = CertificateScanner.ScanAll(_currentConfig);
			int savedCount = DatabaseManager.SaveCertificates(records, _currentConfig.ConnectionString, _currentConfig.TableName);

			string summaryMessage = string.Format("Сканирование завершено!\n\nВсего найдено сертификатов: {0}\nУспешно обработано в БД: {1}\n\nПодробный отчет записан в журнал: {2}", records.Count, savedCount, Logger.GetLogFilePath());
			MessageBox.Show(this, summaryMessage, "Итоги сбора сведений о СКЗИ", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		// Считывание значений из элементов формы в поля текущего объекта конфигурации
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
			_currentConfig.TableName = string.IsNullOrWhiteSpace(_textTableName.Text) ? "Certificates" : _textTableName.Text.Trim();
		}
	}
}
