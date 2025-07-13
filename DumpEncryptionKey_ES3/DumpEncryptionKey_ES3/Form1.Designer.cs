using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Microsoft.Win32;

namespace KeyFinder
{
    public sealed class Form1 : Form
    {
        public Form1() => BuildUi();

        // ────────────────── UI ──────────────────
        private readonly Button _btnSelectFolder = new();
        private readonly TextBox _txtOut = new();

        private void BuildUi()
        {
            Text = "ES3 encryptionKey finder";
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.Gainsboro;
            MinimumSize = new Size(850, 500);
            StartPosition = FormStartPosition.CenterScreen;

            _btnSelectFolder.Text = "Выбрать папку игры…";
            _btnSelectFolder.AutoSize = true;
            _btnSelectFolder.Padding = new Padding(12, 6, 12, 6);
            _btnSelectFolder.FlatStyle = FlatStyle.Flat;
            _btnSelectFolder.BackColor = Color.FromArgb(55, 55, 55);
            _btnSelectFolder.Click += (_, _) => PickFolder();

            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(38, 38, 38),
                Padding = new Padding(10)
            };
            top.Controls.Add(_btnSelectFolder);

            _txtOut.Multiline = true;
            _txtOut.ReadOnly = true;
            _txtOut.BackColor = Color.FromArgb(24, 24, 24);
            _txtOut.ForeColor = Color.Gainsboro;
            _txtOut.ScrollBars = ScrollBars.Vertical;
            _txtOut.Dock = DockStyle.Fill;

            Controls.Add(_txtOut);
            Controls.Add(top);
        }

        private void Log(string msg = "") => _txtOut.AppendText(msg + Environment.NewLine);

        // ────────────────── Выбор папки ──────────────────
        private void PickFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Укажи папку, где лежит Tiny Aquarium.exe"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _txtOut.Clear();
            Cursor = Cursors.WaitCursor;
            try
            {
                ScanGameFolder(dlg.SelectedPath);
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

        // ────────────────── ГЛАВНЫЙ ПАЙПЛАЙН ──────────────────
        private void ScanGameFolder(string root)
        {
            Log($"📂 Папка игры: {root}");
            string dataDir = Directory.GetDirectories(root, "*_Data").FirstOrDefault()
                             ?? throw new DirectoryNotFoundException("Не найден *_Data каталог Unity.");

            // 1) — обход всех .assets
            var assetFiles = Directory.EnumerateFiles(dataDir, "*.*", SearchOption.TopDirectoryOnly)
                                      .Where(f => Regex.IsMatch(Path.GetFileName(f),
                                                @"^(globalgamemanagers|level\d+|sharedassets\d+|resources)\.assets?$",
                                                RegexOptions.IgnoreCase))
                                      .ToList();

            if (assetFiles.Count == 0)
            {
                Log("Не нашёл ни одного *.assets — пропускаю фазу #1.");
            }
            else
            {
                Log($"🔎 Проверяю {assetFiles.Count} assets-файл(ов)…");
                foreach (string file in assetFiles)
                    TryProcessAssetsFile(file);
            }

            // 2) — PlayerPrefs (реестр)
            Log();
            Log("🔎 Проверяю PlayerPrefs (реестр) …");
            CheckRegistryForKey();

            // 3) — ASCII-поиск в GameAssembly.dll
            string asmPath = Path.Combine(root, "GameAssembly.dll");
            if (File.Exists(asmPath))
            {
                Log();
                Log("🔎 Ищу строку \"encryptionKey\" в GameAssembly.dll …");
                ScanAssemblyForLiteral(asmPath);
            }
            else
            {
                Log("GameAssembly.dll не найден — пропускаю фазу #3.");
            }

            Log();
            Log("✅ Готово.");
        }

        // ────────────────── Чтение assets-файла ──────────────────
        private void TryProcessAssetsFile(string path)
        {
            var am = new AssetsManager();
            AssetsFileInstance inst;

            try
            {
                inst = am.LoadAssetsFile(path, true); // пытаемся с type-tree
            }
            catch
            {
                try { inst = am.LoadAssetsFile(path, false); } // повтор без type-tree
                catch (Exception ex)
                {
                    Log($"⚠️  {Path.GetFileName(path)} → не удалось открыть ({ex.GetType().Name})");
                    return;
                }
            }

            bool foundAny = false;
            foreach (var info in inst.file.Metadata.AssetInfos)
            {
                AssetTypeValueField? field;
                try { field = am.GetBaseField(inst, info); }
                catch { continue; }

                if (field?.TypeName != "MonoBehaviour") continue;

                if (FindKey(field, out var key, out var monoName))
                {
                    Log($"🗝  {Path.GetFileName(path)}  ▶  [{monoName}]  =  \"{key}\"");
                    foundAny = true;
                }
            }

            if (!foundAny)
                Log($"— {Path.GetFileName(path)}: ключ не найден.");
        }

        // ────────────────── рекурсивный поиск поля ──────────────────
        private static bool FindKey(AssetTypeValueField node,
                                    out string key,
                                    out string monoName)
        {
            monoName = node.Children?.FirstOrDefault(c => c.FieldName == "m_Name")
                          ?.AsString ?? "<unnamed>";

            if (node.FieldName is "encryptionKey" or "encryptionPassword"
                && node.Value?.ValueType == AssetValueType.String)
            {
                key = node.AsString;
                return true;
            }

            if (node.Children != null)
                foreach (var child in node.Children)
                    if (FindKey(child, out key, out monoName))
                        return true;

            key = null!;
            return false;
        }

        // ────────────────── PlayerPrefs ──────────────────
        private void CheckRegistryForKey()
        {
            using var hkcu = Registry.CurrentUser;
            var companies = hkcu.OpenSubKey(@"Software");
            if (companies == null) { Log("HKCU\\Software недоступен."); return; }

            foreach (string companyName in companies.GetSubKeyNames())
            {
                using var companyKey = companies.OpenSubKey(companyName);
                if (companyKey == null) continue;
                foreach (string productName in companyKey.GetSubKeyNames())
                {
                    using var productKey = companyKey.OpenSubKey(productName);
                    if (productKey == null) continue;

                    foreach (string valName in productKey.GetValueNames())
                    {
                        if (!valName.ToLower().Contains("key")) continue;

                        var value = productKey.GetValue(valName) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            Log($"🗝  HKCU\\Software\\{companyName}\\{productName} → {valName} = \"{value}\"");
                        }
                    }
                }
            }
        }

        // ────────────────── Поиск ASCII в GameAssembly.dll ──────────────────
        private void ScanAssemblyForLiteral(string asmPath)
        {
            const int MIN_LEN = 16;
            const int MAX_LEN = 32;

            var bytes = File.ReadAllBytes(asmPath);
            string asmText = System.Text.Encoding.ASCII.GetString(bytes);

            var matches = Regex.Matches(asmText, @"encryptionKey.{0,40}?([A-Za-z0-9\+\-/=]{16,32})",
                                        RegexOptions.IgnoreCase);

            if (matches.Count == 0)
            {
                Log("— в GameAssembly.dll строка encryptionKey не найдена.");
                return;
            }

            foreach (Match m in matches)
            {
                var keyCandidate = m.Groups[1].Value;
                int len = keyCandidate.Length;
                if (len is >= MIN_LEN and <= MAX_LEN)
                    Log($"🗝  GameAssembly.dll → найден потенциальный ключ: \"{keyCandidate}\"");
            }
        }
    }
}
