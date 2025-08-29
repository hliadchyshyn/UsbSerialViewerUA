using System.Management;

namespace UsbSerialViewerUA
{
    public sealed class MainForm : Form
    {
        private readonly ListBox _listBox;
        private readonly Button _refreshButton;

        public MainForm()
        {
            Text = "Перегляд серійних номерів USB";
            Width = 600;
            Height = 400;
            StartPosition = FormStartPosition.CenterScreen;

            // Список флешок
            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segue UI", 10),
            };

            // Кнопка оновлення
            _refreshButton = new Button
            {
                Text = "Оновити",
                Dock = DockStyle.Top,
                Height = 35,
            };
            _refreshButton.Click += (_, _) => LoadUsbSerials();

            Controls.Add(_listBox);
            Controls.Add(_refreshButton);

            // Відслідковуємо підключення/відключення USB
            StartUsbWatcher();

            LoadUsbSerials();
        }

        private void LoadUsbSerials()
{
    try
    {
        _listBox.Items.Clear();

        var searcher = new ManagementObjectSearcher(
            $"SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");

        foreach (var o in searcher.Get())
        {
            var drive = (ManagementObject)o;
            var model = drive["Model"]?.ToString() ?? "Невідомий пристрій";
            var serial = "Серійний номер не знайдено";

            try
            {
                // Перший варіант: беремо серійний номер через Win32_PhysicalMedia
                using var mediaSearcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_PhysicalMedia WHERE Tag='{drive["DeviceID"]}'");
                foreach (var managementBaseObject in mediaSearcher.Get())
                {
                    var media = (ManagementObject)managementBaseObject;
                    var sn = media["SerialNumber"]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(sn)) continue;
                    serial = sn;
                    break;
                }
            }
            catch
            {
                // пропускаємо помилки
            }

            // Якщо серійного номера немає — парсимо PNPDeviceID
            if (serial == "Серійний номер не знайдено")
            {
                var deviceId = drive["PNPDeviceID"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(deviceId))
                {
                    var parts = deviceId.Split('\\');
                    if (parts.Length > 2)
                    {
                        serial = parts[2];
                        // Відрізаємо &0 або будь-який суфікс після &
                        var ampIndex = serial.IndexOf('&');
                        if (ampIndex > 0)
                            serial = serial.Substring(0, ampIndex);
                    }
                }
            }

            _listBox.Items.Add($"{model} → {serial}");
        }

        if (_listBox.Items.Count == 0)
            _listBox.Items.Add("USB-накопичувачі не знайдені.");
    }
    catch (Exception ex)
    {
        MessageBox.Show("Помилка при отриманні серійних номерів: " + ex.Message,
            "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

        private void StartUsbWatcher()
        {
            try
            {
                var insertQuery = new WqlEventQuery($"SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
                var removeQuery = new WqlEventQuery($"SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3");

                var insertWatcher = new ManagementEventWatcher(insertQuery);
                var removeWatcher = new ManagementEventWatcher(removeQuery);

                insertWatcher.EventArrived += (_, _) => LoadUsbSerials();
                removeWatcher.EventArrived += (_, _) => LoadUsbSerials();

                insertWatcher.Start();
                removeWatcher.Start();
            }
            catch
            {
                // Можна пропустити помилки
            }
        }
    }
}