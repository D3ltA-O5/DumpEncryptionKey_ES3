using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace KeyFinder
{
    public sealed class Form1 : Form
    {
        public Form1() => BuildUi();

        // ---------- UI ----------
        private readonly Button _btnOpen = new();
        private readonly TextBox _txtOut = new();

        private void BuildUi()
        {
            Text = "ES3 encryptionKey finder";
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.Gainsboro;
            MinimumSize = new Size(780, 420);
            StartPosition = FormStartPosition.CenterScreen;

            _btnOpen.Text = "Открыть .assets";
            _btnOpen.AutoSize = true;
            _btnOpen.Padding = new Padding(12, 6, 12, 6);
            _btnOpen.FlatStyle = FlatStyle.Flat;
            _btnOpen.BackColor = Color.FromArgb(55, 55, 55);
            _btnOpen.ForeColor = Color.White;
            _btnOpen.Click += (_, _) => PickFile();

            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(38, 38, 38),
                Padding = new Padding(10, 10, 10, 10),
                FlowDirection = FlowDirection.LeftToRight
            };
            top.Controls.Add(_btnOpen);

            _txtOut.Multiline = true;
            _txtOut.ReadOnly = true;
            _txtOut.BackColor = Color.FromArgb(24, 24, 24);
            _txtOut.ForeColor = Color.Gainsboro;
            _txtOut.ScrollBars = ScrollBars.Vertical;
            _txtOut.Dock = DockStyle.Fill;

            Controls.Add(_txtOut);
            Controls.Add(top);
        }

        // ---------- выбор файла ----------
        private void PickFile()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Unity assets|*.assets;*.sharedassets*;globalgamemanagers|Все файлы|*.*"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _txtOut.Clear();
            Cursor = Cursors.WaitCursor;
            try
            {
                ExtractKeySafe(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ---------- безопасный анализ .assets ----------
        private void ExtractKeySafe(string path)
        {
            var am = new AssetsManager();
            AssetsFileInstance inst;

            // Пытаемся загрузить с Type‑Tree. Если его нет — повторяем без него.
            try
            {
                inst = am.LoadAssetsFile(path, true);
            }
            catch
            {
                inst = am.LoadAssetsFile(path, false);
            }

            int hits = 0;

            foreach (var info in inst.file.Metadata.AssetInfos)
            {
                AssetTypeValueField? field;
                try
                {
                    field = am.GetBaseField(inst, info);
                }
                catch
                {
                    continue; // пропускаем повреждённый / нестандартный объект
                }

                if (field?.TypeName != "MonoBehaviour") continue;

                if (FindKey(field, out var key, out var name))
                {
                    _txtOut.AppendText($"[{name}]  encryptionKey = {key}{Environment.NewLine}");
                    hits++;
                }
            }

            if (hits == 0)
                _txtOut.Text = "Поле encryptionKey не найдено.";
        }

        // ---------- рекурсивный поиск ----------
        private static bool FindKey(AssetTypeValueField node,
                                    out string key,
                                    out string monoName)
        {
            monoName = node.Children?.FirstOrDefault(c => c.FieldName == "m_Name")
                          ?.AsString ?? "<unnamed>";

            if (node.FieldName is "encryptionKey" or "encryptionPassword" &&
                node.Value?.ValueType == AssetValueType.String)
            {
                key = node.AsString;
                return true;
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                    if (FindKey(child, out key, out monoName))
                        return true;
            }

            key = null!;
            return false;
        }
    }
}
