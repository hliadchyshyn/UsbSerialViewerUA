using System.Diagnostics;
using System.Management;

namespace UsbSerialViewerUA
{
    public sealed class MainForm : Form
    {
        private readonly ListView _listView;
        private readonly Button _refreshButton;
        private readonly ToolStripStatusLabel _statusLabel;

        private ManagementEventWatcher? _insertWatcher;
        private ManagementEventWatcher? _removeWatcher;

        public MainForm()
        {
            Text = "Перегляд серійних номерів USB";
            Width = 650;
            Height = 450;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9);

            // Контекстне меню для копіювання
            var contextMenu = new ContextMenuStrip();
            var copyItem = new ToolStripMenuItem("Копіювати серійний номер");
            copyItem.Click += CopySerialToClipboard;
            contextMenu.Items.Add(copyItem);

            // Список пристроїв
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 10),
                ContextMenuStrip = contextMenu,
            };
            _listView.Columns.Add("Пристрій", 300);
            _listView.Columns.Add("Серійний номер", 310);

            // Кнопка оновлення
            _refreshButton = new Button
            {
                Text = "Оновити",
                Dock = DockStyle.Top,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
            };
            _refreshButton.FlatAppearance.BorderSize = 0;
            _refreshButton.Click += async (_, _) => await LoadUsbSerialsAsync();

            // Статусний рядок
            var statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Готово");
            statusStrip.Items.Add(_statusLabel);

            Controls.Add(_listView);
            Controls.Add(_refreshButton);
            Controls.Add(statusStrip);

            StartUsbWatcher();

            _ = LoadUsbSerialsAsync();
        }

        private async Task LoadUsbSerialsAsync()
        {
            _refreshButton.Enabled = false;
            _statusLabel.Text = "Завантаження...";

            try
            {
                var items = await Task.Run(() =>
                {
                    var results = new List<(string Model, string Serial)>();

                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");

                    foreach (var o in searcher.Get())
                    {
                        var drive = (ManagementObject)o;
                        var model = drive["Model"]?.ToString() ?? "Невідомий пристрій";
                        string? serial = null;

                        try
                        {
                            var deviceId = drive["DeviceID"]?.ToString()?.Replace("'", "\\'") ?? "";
                            using var mediaSearcher = new ManagementObjectSearcher(
                                $"SELECT * FROM Win32_PhysicalMedia WHERE Tag='{deviceId}'");

                            foreach (var managementBaseObject in mediaSearcher.Get())
                            {
                                var media = (ManagementObject)managementBaseObject;
                                var sn = media["SerialNumber"]?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(sn))
                                {
                                    serial = sn;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Помилка Win32_PhysicalMedia: {ex.Message}");
                        }

                        // Якщо серійного номера немає — парсимо PNPDeviceID
                        if (serial is null)
                        {
                            var pnpId = drive["PNPDeviceID"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(pnpId))
                            {
                                var parts = pnpId.Split('\\');
                                if (parts.Length > 2)
                                {
                                    serial = parts[2];
                                    var ampIndex = serial.IndexOf('&');
                                    if (ampIndex > 0)
                                        serial = serial[..ampIndex];
                                }
                            }
                        }

                        results.Add((model, serial ?? "Серійний номер не знайдено"));
                    }

                    return results;
                });

                _listView.Items.Clear();

                foreach (var (model, serial) in items)
                {
                    var item = new ListViewItem(model);
                    item.SubItems.Add(serial);
                    _listView.Items.Add(item);
                }

                if (_listView.Items.Count == 0)
                {
                    var empty = new ListViewItem("USB-накопичувачі не знайдені.");
                    empty.SubItems.Add("");
                    _listView.Items.Add(empty);
                }

                _statusLabel.Text = _listView.Items.Count > 0 && items.Count > 0
                    ? $"Знайдено пристроїв: {items.Count}"
                    : "Пристроїв не знайдено";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка при отриманні серійних номерів: " + ex.Message,
                    "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "Помилка";
            }
            finally
            {
                _refreshButton.Enabled = true;
            }
        }

        private void StartUsbWatcher()
        {
            try
            {
                var insertQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
                var removeQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3");

                _insertWatcher = new ManagementEventWatcher(insertQuery);
                _removeWatcher = new ManagementEventWatcher(removeQuery);

                _insertWatcher.EventArrived += (_, _) =>
                {
                    if (IsHandleCreated && !IsDisposed)
                        Invoke(async () => await LoadUsbSerialsAsync());
                };
                _removeWatcher.EventArrived += (_, _) =>
                {
                    if (IsHandleCreated && !IsDisposed)
                        Invoke(async () => await LoadUsbSerialsAsync());
                };

                _insertWatcher.Start();
                _removeWatcher.Start();

                _statusLabel.Text = "Моніторинг USB...";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Помилка запуску спостерігача: {ex.Message}");
                _statusLabel.Text = "Моніторинг недоступний";
            }
        }

        private void CopySerialToClipboard(object? sender, EventArgs e)
        {
            if (_listView.SelectedItems.Count == 0)
                return;

            var serial = _listView.SelectedItems[0].SubItems[1].Text;
            if (!string.IsNullOrEmpty(serial) && serial != "Серійний номер не знайдено")
                Clipboard.SetText(serial);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _insertWatcher?.Stop();
                _insertWatcher?.Dispose();
                _removeWatcher?.Stop();
                _removeWatcher?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
