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
            _btnOpen.FlatStyle = FlatStyle.Flat;
            _btnOpen.BackColor = Color.FromArgb(45, 45, 45);
            _btnOpen.ForeColor = Color.White;
            _btnOpen.Click += (_, _) => PickFile();

            var top = new Panel { Dock = DockStyle.Top, Height = 50 };
            top.Controls.Add(_btnOpen);
            _btnOpen.Location = new Point(12, 10);

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
            try { ExtractKey(dlg.FileName); }
            catch (Exception ex) { _txtOut.Text = ex.Message; }
            finally { Cursor = Cursors.Default; }
        }

        // ---------- анализ .assets ----------
        private void ExtractKey(string path)
        {
            var am = new AssetsManager();
            var inst = am.LoadAssetsFile(path, true);   // ← вторым аргументом включаем TypeTree
            int hits = 0;

            foreach (var info in inst.file.Metadata.AssetInfos)
            {
                var field = am.GetBaseField(inst, info);
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
        private static bool FindKey(AssetTypeValueField n,
                                    out string key,
                                    out string monoName)
        {
            monoName = n.Children.FirstOrDefault(c => c.FieldName == "m_Name")
                          ?.AsString ?? "<unnamed>";

            if (n.FieldName is "encryptionKey" or "encryptionPassword" &&
                n.Value?.ValueType == AssetValueType.String)
            {
                key = n.AsString;
                return true;
            }
            foreach (var c in n.Children)
                if (FindKey(c, out key, out monoName))
                    return true;

            key = null!;
            return false;
        }
    }
}
