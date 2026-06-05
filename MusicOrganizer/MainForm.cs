using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicOrganizer
{
    public class MainForm : Form
    {
        // UI Controls
        private Panel topPanel = null!;
        private Label lblFolder = null!;
        private TextBox txtFolderPath = null!;
        private Button btnBrowse = null!;
        private Button btnStart = null!;
        private ProgressBar progressBar = null!;
        private SplitContainer mainSplit = null!;
        private RichTextBox txtLog = null!;
        private DataGridView dgvSongs = null!;

        private TabControl tabControl = null!;
        private TabPage tabProcessing = null!;
        private TabPage tabKanban = null!;
        private Dictionary<string, ListBox> moodListBoxes = new Dictionary<string, ListBox>(StringComparer.OrdinalIgnoreCase);

        // Configuration and State
        private string selectedFolder = @"d:\Mus";
        private static readonly HttpClient client = new HttpClient();
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".m4a" };
        private Dictionary<string, OverrideEntry> overridesMap = new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase);
        private string? lastFmApiKey = null;
        private ConcurrentBag<PlaylistEntry> playlistEntries = new ConcurrentBag<PlaylistEntry>();

        private static int processedCount = 0;
        private static int lyricsEmbeddedCount = 0;
        private static int genreUpdatedCount = 0;
        private static int totalFilesCount = 0;

        private static readonly string[] Moods = { "Chill", "Gaming_Mood", "Happy_Vibes", "SAD" };

        public MainForm()
        {
            InitializeComponent();
            
            // Set UserAgent for HTTP requests
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MusicOrganizer/1.0 (https://github.com/google-deepmind/antigravity)");
        }

        private void InitializeComponent()
        {
            this.Text = "Music Organizer & Mood Classifier (GUI)";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Top Panel for folder selection
            topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            
            lblFolder = new Label { Text = "مجلد الموسيقى:", Location = new Point(10, 20), Width = 100 };
            txtFolderPath = new TextBox { Text = selectedFolder, Location = new Point(110, 17), Width = 500 };
            btnBrowse = new Button { Text = "اختر المجلد", Location = new Point(620, 15), Width = 100 };
            btnStart = new Button { Text = "ابدأ التصنيف", Location = new Point(730, 15), Width = 120, BackColor = Color.LightGreen };
            
            topPanel.Controls.Add(lblFolder);
            topPanel.Controls.Add(txtFolderPath);
            topPanel.Controls.Add(btnBrowse);
            topPanel.Controls.Add(btnStart);

            // Progress Bar
            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 20 };

            // Tab Control
            tabControl = new TabControl { Dock = DockStyle.Fill };
            tabProcessing = new TabPage { Text = "المعالجة وتفاصيل الأغاني" };
            tabKanban = new TabPage { Text = "منظم القوائم (Kanban Board)" };

            // Split Container
            mainSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 220 };

            // RichTextBox Log
            txtLog = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.LightGreen, Font = new Font("Consolas", 10) };
            mainSplit.Panel1.Controls.Add(txtLog);

            // DataGridView dgvSongs
            dgvSongs = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White
            };

            dgvSongs.Columns.Add("FileName", "اسم الملف");
            dgvSongs.Columns["FileName"]!.ReadOnly = true;
            
            dgvSongs.Columns.Add("Artist", "الفنان");
            dgvSongs.Columns["Artist"]!.ReadOnly = true;
            
            dgvSongs.Columns.Add("Title", "العنوان");
            dgvSongs.Columns["Title"]!.ReadOnly = true;
            
            dgvSongs.Columns.Add("Genre", "النوع (Genre)");
            dgvSongs.Columns["Genre"]!.ReadOnly = true;

            var moodCol = new DataGridViewComboBoxColumn
            {
                Name = "ManualMood",
                HeaderText = "المود (Grouping)",
                DataSource = Moods,
                FlatStyle = FlatStyle.Flat
            };
            dgvSongs.Columns.Add(moodCol);

            mainSplit.Panel2.Controls.Add(dgvSongs);
            
            tabProcessing.Controls.Add(mainSplit);

            // Initialize Kanban Board Tab
            TableLayoutPanel kanbanLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(240, 242, 245)
            };

            for (int i = 0; i < 4; i++)
            {
                kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }
            kanbanLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            string[] moods = { "Chill", "SAD", "Happy_Vibes", "Gaming_Mood" };
            string[] moodArabicNames = { "الهدوء (Chill)", "الحزن (SAD)", "البهجة (Happy Vibes)", "الحماس (Gaming Mood)" };
            Color[] moodColors = {
                Color.FromArgb(42, 157, 143),  // Teal
                Color.FromArgb(69, 123, 157), // Slate Blue
                Color.FromArgb(244, 162, 97), // Warm Orange
                Color.FromArgb(230, 57, 70)   // Crimson Red
            };

            for (int i = 0; i < moods.Length; i++)
            {
                string moodName = moods[i];
                
                Panel colPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(5),
                    BackColor = Color.White,
                    Padding = new Padding(2)
                };
                
                Label lblHeader = new Label
                {
                    Text = moodArabicNames[i],
                    Dock = DockStyle.Top,
                    Height = 40,
                    BackColor = moodColors[i],
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                
                ListBox lb = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.None,
                    Font = new Font("Segoe UI", 10F),
                    SelectionMode = SelectionMode.One,
                    AllowDrop = true,
                    Tag = moodName
                };
                
                lb.MouseDown += ListBox_MouseDown;
                lb.DragEnter += ListBox_DragEnter;
                lb.DragOver += ListBox_DragOver;
                lb.DragDrop += ListBox_DragDrop;
                
                colPanel.Controls.Add(lb);
                colPanel.Controls.Add(lblHeader);
                
                moodListBoxes[moodName] = lb;
                kanbanLayout.Controls.Add(colPanel, i, 0);
            }

            tabKanban.Controls.Add(kanbanLayout);

            tabControl.TabPages.Add(tabProcessing);
            tabControl.TabPages.Add(tabKanban);

            this.Controls.Add(tabControl);
            this.Controls.Add(progressBar);
            this.Controls.Add(topPanel);

            // Wire events
            btnBrowse.Click += BtnBrowse_Click;
            btnStart.Click += BtnStart_Click;
            dgvSongs.CellValueChanged += DgvSongs_CellValueChanged;
            this.Load += MainForm_Load;
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            Log("==================================================");
            Log("مرحباً بك في واجهة تنظيم الموسيقى وتصنيف المود");
            Log("==================================================");
            
            // Run Pre-flight check
            RunPreFlightCheck();
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = txtFolderPath.Text;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtFolderPath.Text = dialog.SelectedPath;
                    selectedFolder = dialog.SelectedPath;
                }
            }
        }

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            selectedFolder = txtFolderPath.Text;
            if (!Directory.Exists(selectedFolder))
            {
                MessageBox.Show($"المجلد '{selectedFolder}' غير موجود. يرجى اختيار مجلد صالح.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Disable buttons to avoid concurrent runs
            btnStart.Enabled = false;
            btnBrowse.Enabled = false;
            txtFolderPath.Enabled = false;

            txtLog.Clear();
            dgvSongs.Rows.Clear();
            foreach (var pair in moodListBoxes)
            {
                pair.Value.Items.Clear();
            }
            playlistEntries = new ConcurrentBag<PlaylistEntry>();
            progressBar.Value = 0;
            
            processedCount = 0;
            lyricsEmbeddedCount = 0;
            genreUpdatedCount = 0;

            Log("==================================================");
            Log($"بدء معالجة الموسيقى في المجلد: {selectedFolder}");
            Log("==================================================");

            await Task.Run(async () =>
            {
                try
                {
                    // Directories to exclude
                    var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "12", "New folder", "gaming", "MusicOrganizer", "MusicOrganization" };

                    // Load overrides map
                    string overridesPath = Path.Combine(selectedFolder, "metadata_overrides.json");
                    overridesMap = new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase);
                    if (File.Exists(overridesPath))
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(overridesPath);
                            overridesMap = JsonSerializer.Deserialize<Dictionary<string, OverrideEntry>>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                ?? new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase);
                            Log($"[+] تم تحميل {overridesMap.Count} من تفضيلات الميتاداتا.");
                        }
                        catch (Exception ex)
                        {
                            Log($"[!] تحذير: فشل تحميل metadata_overrides.json: {ex.Message}");
                        }
                    }

                    // Load Last.fm key
                    string lastFmKeyPath = Path.Combine(selectedFolder, "lastfm_key.txt");
                    lastFmApiKey = null;
                    if (File.Exists(lastFmKeyPath))
                    {
                        try
                        {
                            lastFmApiKey = File.ReadAllText(lastFmKeyPath).Trim();
                            if (!string.IsNullOrWhiteSpace(lastFmApiKey))
                            {
                                Log("[+] تم تحميل مفتاح Last.fm بنجاح.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[!] تحذير: فشل قراءة مفتاح Last.fm: {ex.Message}");
                        }
                    }

                    if (string.IsNullOrWhiteSpace(lastFmApiKey))
                    {
                        Log("[!] تحذير: مفتاح Last.fm غير موجود. سيتم تخطي الاستعلام من Last.fm.");
                    }

                    // Scan files recursively with non-music pre-filtering
                    var skipKeywords = new[] { "سورة", "قرآن", "قران", "الشيخ", "تلاوة" };
                    var files = Directory.GetFiles(selectedFolder, "*.*", SearchOption.AllDirectories)
                        .Where(f => AllowedExtensions.Contains(Path.GetExtension(f)))
                        .Where(f =>
                        {
                            string fileName = Path.GetFileName(f);
                            return !skipKeywords.Any(k => fileName.Contains(k, StringComparison.OrdinalIgnoreCase));
                        })
                        .Where(f =>
                        {
                            var relativePath = Path.GetRelativePath(selectedFolder, f);
                            var parts = relativePath.Split(Path.DirectorySeparatorChar);
                            if (parts.Length > 1)
                            {
                                var parentParts = parts.Take(parts.Length - 1);
                                return !parentParts.Any(p => excludedDirs.Contains(p));
                            }
                            return true;
                        })
                        .ToList();

                    totalFilesCount = files.Count;
                    Log($"[+] تم العثور على {totalFilesCount} ملف صوتي صالح للمزامنة.");

                    using (var semaphore = new SemaphoreSlim(4))
                    {
                        var tasks = files.Select(async (filePath) =>
                        {
                            await semaphore.WaitAsync();
                            var fileLog = new List<string>();
                            string currentFilePath = filePath;
                            string fileName = Path.GetFileName(currentFilePath);

                            try
                            {
                                int currentNum = Interlocked.Increment(ref processedCount);
                                fileLog.Add($"[{currentNum}/{totalFilesCount}] جاري المعالجة: {fileName}");

                                // Apply overrides if exists
                                if (overridesMap.TryGetValue(fileName, out var overrideEntry))
                                {
                                    fileLog.Add($"  -> تم العثور على ميتاداتا مخصصة: \"{overrideEntry.Artist} - {overrideEntry.Title}\"");
                                    try
                                    {
                                        using (var audioFile = TagLib.File.Create(currentFilePath))
                                        {
                                            audioFile.Tag.Performers = new[] { overrideEntry.Artist };
                                            audioFile.Tag.Title = overrideEntry.Title;
                                            audioFile.Save();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        fileLog.Add($"  [!] خطأ في كتابة التعديل: {ex.Message}");
                                    }

                                    // Rename SMI
                                    string parentDir = Path.GetDirectoryName(currentFilePath) ?? selectedFolder;
                                    string cleanBase = SanitizeFolderName($"{overrideEntry.Artist} - {overrideEntry.Title}");
                                    string oldSmi = Path.ChangeExtension(currentFilePath, ".smi");
                                    string newSmi = Path.Combine(parentDir, cleanBase + ".smi");
                                    if (File.Exists(oldSmi))
                                    {
                                        try
                                        {
                                            if (!string.Equals(Path.GetFullPath(oldSmi), Path.GetFullPath(newSmi), StringComparison.OrdinalIgnoreCase))
                                            {
                                                if (File.Exists(newSmi)) File.Delete(newSmi);
                                                File.Move(oldSmi, newSmi);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            fileLog.Add($"  [!] خطأ في إعادة تسمية SMI: {ex.Message}");
                                        }
                                    }

                                    // Rename Audio File
                                    try
                                    {
                                        string ext = Path.GetExtension(currentFilePath);
                                        string newAudio = Path.Combine(parentDir, cleanBase + ext);
                                        if (!string.Equals(Path.GetFullPath(currentFilePath), Path.GetFullPath(newAudio), StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (File.Exists(newAudio)) File.Delete(newAudio);
                                            File.Move(currentFilePath, newAudio);
                                            currentFilePath = newAudio;
                                            fileName = Path.GetFileName(currentFilePath);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        fileLog.Add($"  [!] خطأ في إعادة تسمية الملف الصوتي: {ex.Message}");
                                    }
                                }

                                string artist = "";
                                string title = "";
                                string album = "";
                                string genre = "";
                                string existingLyrics = "";
                                string existingGrouping = "";
                                int duration = 0;
                                bool lyricsLoaded = false;
                                string? lyrics = null;

                                bool hasValidTags = false;
                                try
                                {
                                    using (var audioFile = TagLib.File.Create(currentFilePath))
                                    {
                                        artist = audioFile.Tag.FirstPerformer ?? audioFile.Tag.FirstAlbumArtist ?? "";
                                        title = audioFile.Tag.Title ?? "";
                                        album = audioFile.Tag.Album ?? "";
                                        genre = audioFile.Tag.FirstGenre ?? "";
                                        existingLyrics = audioFile.Tag.Lyrics ?? "";
                                        existingGrouping = audioFile.Tag.Grouping ?? "";
                                        duration = (int)audioFile.Properties.Duration.TotalSeconds;
                                        hasValidTags = true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    fileLog.Add($"  [!] خطأ في قراءة الأوسمة: {ex.Message}");
                                }

                                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                                {
                                    ParseArtistTitleFromFileName(Path.GetFileNameWithoutExtension(fileName), ref artist, ref title);
                                }

                                string cleanArtist = CleanArtistName(artist);
                                string cleanTitle = CleanSongTitle(title);

                                // iTunes Genre Lookup
                                bool genreUpdated = false;
                                bool needsGenre = string.IsNullOrWhiteSpace(genre) || string.Equals(genre, "Unknown", StringComparison.OrdinalIgnoreCase);
                                if (hasValidTags && !string.IsNullOrWhiteSpace(cleanTitle) && needsGenre)
                                {
                                    fileLog.Add($"  -> جاري استعلام iTunes عن نوع الأغنية: \"{cleanArtist} - {cleanTitle}\"");
                                    string? fetchedGenre = await FetchGenreFromiTunes(cleanArtist, cleanTitle);
                                    if (!string.IsNullOrWhiteSpace(fetchedGenre))
                                    {
                                        fileLog.Add($"  -> تم العثور على النوع: \"{fetchedGenre}\"");
                                        genre = fetchedGenre;
                                        genreUpdated = true;
                                        Interlocked.Increment(ref genreUpdatedCount);
                                    }
                                }

                                // Lyrics Lookup
                                bool needsLyrics = string.IsNullOrWhiteSpace(existingLyrics);
                                if (needsLyrics)
                                {
                                    string smi = Path.ChangeExtension(currentFilePath, ".smi");
                                    if (File.Exists(smi))
                                    {
                                        lyrics = ReadLyricsFromSmi(smi);
                                        if (!string.IsNullOrWhiteSpace(lyrics))
                                        {
                                            fileLog.Add("  -> تم قراءة الكلمات من ملف .smi محلي.");
                                            lyricsLoaded = true;
                                        }
                                    }
                                }

                                if (needsLyrics && !lyricsLoaded && !string.IsNullOrWhiteSpace(cleanTitle) && !string.IsNullOrWhiteSpace(cleanArtist))
                                {
                                    lyrics = await FetchLyricsFromApi(cleanArtist, cleanTitle, album, duration);
                                    if (!string.IsNullOrWhiteSpace(lyrics))
                                    {
                                        fileLog.Add("  -> تم تحميل الكلمات من LRCLib API.");
                                        lyricsLoaded = true;
                                    }
                                }

                                // Save Tags
                                if (hasValidTags && (lyricsLoaded || genreUpdated))
                                {
                                    try
                                    {
                                        using (var audioFile = TagLib.File.Create(currentFilePath))
                                        {
                                            bool mod = false;
                                            if (lyricsLoaded && !string.IsNullOrWhiteSpace(lyrics))
                                            {
                                                audioFile.Tag.Lyrics = lyrics;
                                                mod = true;
                                                Interlocked.Increment(ref lyricsEmbeddedCount);
                                            }
                                            if (genreUpdated && !string.IsNullOrWhiteSpace(genre))
                                            {
                                                audioFile.Tag.Genres = new[] { genre };
                                                mod = true;
                                            }
                                            if (mod) audioFile.Save();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        fileLog.Add($"  [!] خطأ في حفظ الأوسمة المحدثة: {ex.Message}");
                                    }
                                }

                                // Organize to genre folder
                                string cleanGenre = SanitizeFolderName(genre);
                                string genreFolder = Path.Combine(selectedFolder, cleanGenre);
                                string finalAudioPath = currentFilePath;
                                try
                                {
                                    finalAudioPath = MoveFileSafely(currentFilePath, genreFolder);
                                    
                                    // Move SMI if exists
                                    string smi = Path.ChangeExtension(currentFilePath, ".smi");
                                    if (File.Exists(smi))
                                    {
                                        MoveFileSafely(smi, genreFolder);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    fileLog.Add($"  [!] خطأ في نقل الملف: {ex.Message}");
                                }

                                // Resolve Mood (Reversed Hybrid Pipeline)
                                string? songMood = null;
                                var validMoods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Gaming_Mood", "Happy_Vibes", "SAD", "Chill" };

                                bool forceReevaluate = true; // GUI force re-evaluate flag active for fresh classifications

                                // If metadata_overrides contains a mood override, use it first
                                if (overridesMap.TryGetValue(fileName, out var currentOverride) && !string.IsNullOrEmpty(currentOverride.Mood))
                                {
                                    songMood = currentOverride.Mood;
                                    fileLog.Add($"  -> استخدام المود المحدد يدوياً: {songMood}");
                                }
                                else if (!forceReevaluate && validMoods.Contains(existingGrouping))
                                {
                                    songMood = existingGrouping;
                                    fileLog.Add($"  -> استخدام المود المحفوظ سابقاً: {songMood}");
                                }
                                else
                                {
                                    // Step 1: Query Last.fm
                                    if (!string.IsNullOrWhiteSpace(lastFmApiKey) && !string.IsNullOrWhiteSpace(cleanTitle))
                                    {
                                        fileLog.Add($"  -> جاري استعلام Last.fm عن وسوم الأغنية...");
                                        songMood = await FetchMoodFromLastFm(cleanArtist, cleanTitle, lastFmApiKey);
                                    }

                                    if (songMood != null && validMoods.Contains(songMood))
                                    {
                                        fileLog.Add($"  -> تم تحديد المود عبر Last.fm: {songMood}");
                                    }
                                    else
                                    {
                                        fileLog.Add("  -> فشل الاستعلام من Last.fm أو لم يعثر على وسوم. التحويل للمحلل الصوتي المحلي.");
                                        
                                        // Step 2: Local Python audio analyzer
                                        var pythonResult = GetMoodFromPython(finalAudioPath);
                                        if (pythonResult != null && pythonResult.Value.mood != null && pythonResult.Value.confidence >= 0.70 && validMoods.Contains(pythonResult.Value.mood))
                                        {
                                            songMood = pythonResult.Value.mood;
                                            fileLog.Add($"  -> تم تحديد المود بالتحليل الصوتي: {songMood} (الثقة: {pythonResult.Value.confidence:F2})");
                                        }
                                        else
                                        {
                                            if (pythonResult != null && pythonResult.Value.mood != null)
                                            {
                                                fileLog.Add($"  -> التحليل الصوتي أعطى نسبة ثقة منخفضة: {pythonResult.Value.mood} ({pythonResult.Value.confidence:F2}). التحويل لمطابقة الكلمات.");
                                            }
                                            else
                                            {
                                                fileLog.Add("  -> فشل تشغيل المحلل الصوتي المحلي. التحويل لمطابقة الكلمات.");
                                            }

                                            // Step 3: Keyword matching
                                            songMood = GetMood(cleanTitle, cleanArtist, finalAudioPath);
                                            fileLog.Add($"  -> تم تحديد المود عبر مطابقة الكلمات: {songMood}");
                                        }
                                    }

                                    // Cache in Grouping
                                    if (songMood != null && validMoods.Contains(songMood))
                                    {
                                        try
                                        {
                                            using (var audioFile = TagLib.File.Create(finalAudioPath))
                                            {
                                                audioFile.Tag.Grouping = songMood;
                                                audioFile.Save();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            fileLog.Add($"  [!] خطأ في حفظ تصنيف المود في الملف: {ex.Message}");
                                        }
                                    }
                                }

                                playlistEntries.Add(new PlaylistEntry
                                {
                                    RelativePath = Path.GetRelativePath(selectedFolder, finalAudioPath),
                                    Duration = duration > 0 ? duration : -1,
                                    Title = !string.IsNullOrWhiteSpace(title) ? title : Path.GetFileNameWithoutExtension(finalAudioPath),
                                    Artist = artist,
                                    Mood = songMood
                                });

                                // Add to GUI DataGridView & Kanban
                                AddOrUpdateGridRow(Path.GetFileName(finalAudioPath), artist, title, genre, songMood ?? "Chill");
                                AddOrUpdateKanbanSong(finalAudioPath, artist, title, songMood ?? "Chill");
                            }
                            catch (Exception ex)
                            {
                                fileLog.Add($"  [!] خطأ غير متوقع: {ex.Message}");
                            }
                            finally
                            {
                                foreach (var line in fileLog) Log(line);
                                Log(""); // blank line separating files
                                
                                UpdateProgress(processedCount, totalFilesCount);
                                semaphore.Release();
                            }
                        });

                        await Task.WhenAll(tasks);
                    }

                    // Write Playlists
                    Log("==================================================");
                    Log("جاري توليد قوائم التشغيل...");
                    RegeneratePlaylists();
                    
                    Log("==================================================");
                    Log("ملخص عملية التشغيل:");
                    Log($"  - إجمالي الملفات المعالجة: {processedCount}");
                    Log($"  - إجمالي الأنواع (Genres) المحدثة: {genreUpdatedCount}");
                    Log($"  - إجمالي الملفات المدمجة بالكلمات: {lyricsEmbeddedCount}");
                    Log("==================================================");
                }
                catch (Exception ex)
                {
                    Log($"[!] خطأ فادح في معالجة المجلد: {ex.Message}");
                }
            });

            // Re-enable controls
            btnStart.Enabled = true;
            btnBrowse.Enabled = true;
            txtFolderPath.Enabled = true;
        }

        private void Log(string text)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => Log(text)));
            }
            else
            {
                txtLog.AppendText(text + Environment.NewLine);
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.ScrollToCaret();
            }
        }

        private void UpdateProgress(int val, int max)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() => UpdateProgress(val, max)));
            }
            else
            {
                progressBar.Maximum = max;
                progressBar.Value = val;
            }
        }

        private void AddOrUpdateGridRow(string fileName, string artist, string title, string genre, string mood)
        {
            if (dgvSongs.InvokeRequired)
            {
                dgvSongs.Invoke(new Action(() => AddOrUpdateGridRow(fileName, artist, title, genre, mood)));
            }
            else
            {
                // Temporarily detach handler to avoid trigger loop
                dgvSongs.CellValueChanged -= DgvSongs_CellValueChanged;

                DataGridViewRow? targetRow = null;
                foreach (DataGridViewRow row in dgvSongs.Rows)
                {
                    if (row.Cells["FileName"].Value?.ToString() == fileName)
                    {
                        targetRow = row;
                        break;
                    }
                }

                if (targetRow != null)
                {
                    targetRow.Cells["Artist"].Value = artist;
                    targetRow.Cells["Title"].Value = title;
                    targetRow.Cells["Genre"].Value = genre;
                    targetRow.Cells["ManualMood"].Value = mood;
                }
                else
                {
                    dgvSongs.Rows.Add(fileName, artist, title, genre, mood);
                }

                dgvSongs.CellValueChanged += DgvSongs_CellValueChanged;
            }
        }

        private void DgvSongs_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (dgvSongs.Columns[e.ColumnIndex].Name == "ManualMood")
            {
                string? fileName = dgvSongs.Rows[e.RowIndex].Cells["FileName"].Value?.ToString();
                string? newMood = dgvSongs.Rows[e.RowIndex].Cells["ManualMood"].Value?.ToString();

                if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(newMood)) return;

                UpdateSongMoodOverride(fileName, newMood);
            }
        }

        private void UpdateSongMoodOverride(string fileName, string newMood)
        {
            try
            {
                var files = Directory.GetFiles(selectedFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => AllowedExtensions.Contains(Path.GetExtension(f)))
                    .ToList();

                string? filePath = files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                if (filePath == null)
                {
                    Log($"[!] لم يتم العثور على الملف {fileName} لتعديل وسام المود.");
                    return;
                }

                // 1. Update tag
                using (var audioFile = TagLib.File.Create(filePath))
                {
                    audioFile.Tag.Grouping = newMood;
                    audioFile.Save();
                }
                Log($"[+] تم تعديل المود يدوياً إلى {newMood} في ملف: {fileName}");

                // 2. Update overrides config
                string overridesPath = Path.Combine(selectedFolder, "metadata_overrides.json");
                var localOverrides = new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(overridesPath))
                {
                    try
                    {
                        string json = File.ReadAllText(overridesPath);
                        localOverrides = JsonSerializer.Deserialize<Dictionary<string, OverrideEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase);
                    }
                    catch { }
                }

                string artist = "";
                string title = "";
                using (var audioFile = TagLib.File.Create(filePath))
                {
                    artist = audioFile.Tag.FirstPerformer ?? "";
                    title = audioFile.Tag.Title ?? "";
                }
                if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
                {
                    ParseArtistTitleFromFileName(Path.GetFileNameWithoutExtension(fileName), ref artist, ref title);
                }

                if (localOverrides.TryGetValue(fileName, out var entry))
                {
                    entry.Mood = newMood;
                }
                else
                {
                    localOverrides[fileName] = new OverrideEntry
                    {
                        Artist = artist,
                        Title = title,
                        Mood = newMood
                    };
                }

                var serializeOptions = new JsonSerializerOptions { WriteIndented = true };
                string newJson = JsonSerializer.Serialize(localOverrides, serializeOptions);
                File.WriteAllText(overridesPath, newJson);

                // 3. Update active entries list and playlists
                var activeEntry = playlistEntries.FirstOrDefault(ent => string.Equals(Path.GetFileName(ent.RelativePath), fileName, StringComparison.OrdinalIgnoreCase));
                if (activeEntry != null)
                {
                    activeEntry.Mood = newMood;
                }
                
                // Re-write M3Us
                RegeneratePlaylists();

                // 4. Synchronize with other UI controls
                UpdateGridRowMood(fileName, newMood);
                SyncGridChangeToKanban(fileName, newMood);
            }
            catch (Exception ex)
            {
                Log($"[!] خطأ في تعديل المود يدوياً للملف: {ex.Message}");
            }
        }

        private void RegeneratePlaylists()
        {
            try
            {
                string playlistPath = Path.Combine(selectedFolder, "playlist.m3u");
                var orderedPlaylist = playlistEntries
                    .OrderBy(e => e.Artist)
                    .ThenBy(e => e.Title)
                    .ToList();

                using (var writer = new StreamWriter(playlistPath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("#EXTM3U");
                    foreach (var entry in orderedPlaylist)
                    {
                        string infoLine = entry.Duration > 0
                            ? $"#EXTINF:{entry.Duration},{(string.IsNullOrEmpty(entry.Artist) ? "" : entry.Artist + " - ")}{entry.Title}"
                            : $"#EXTINF:-1,{(string.IsNullOrEmpty(entry.Artist) ? "" : entry.Artist + " - ")}{entry.Title}";
                        writer.WriteLine(infoLine);
                        writer.WriteLine(entry.RelativePath);
                    }
                }

                var moodPlaylists = new Dictionary<string, string>
                {
                    { "Gaming_Mood", Path.Combine(selectedFolder, "Gaming_Mood.m3u") },
                    { "Happy_Vibes", Path.Combine(selectedFolder, "Happy_Vibes.m3u") },
                    { "SAD", Path.Combine(selectedFolder, "SAD.m3u") },
                    { "Chill", Path.Combine(selectedFolder, "Chill.m3u") }
                };

                foreach (var mood in moodPlaylists)
                {
                    var moodEntries = orderedPlaylist
                        .Where(e => string.Equals(e.Mood, mood.Key, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    using (var writer = new StreamWriter(mood.Value, false, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine("#EXTM3U");
                        foreach (var entry in moodEntries)
                        {
                            string infoLine = entry.Duration > 0
                                ? $"#EXTINF:{entry.Duration},{(string.IsNullOrEmpty(entry.Artist) ? "" : entry.Artist + " - ")}{entry.Title}"
                                : $"#EXTINF:-1,{(string.IsNullOrEmpty(entry.Artist) ? "" : entry.Artist + " - ")}{entry.Title}";
                            writer.WriteLine(infoLine);
                            writer.WriteLine(entry.RelativePath);
                        }
                    }
                }
                Log("[+] تم تحديث ملفات قوائم التشغيل (.m3u) بنجاح.");
            }
            catch (Exception ex)
            {
                Log($"[!] خطأ في تحديث ملفات قوائم التشغيل: {ex.Message}");
            }
        }

        private void RunPreFlightCheck()
        {
            Log("[*] جاري فحص البيئة والاعتمادات قبل التشغيل...");
            string pythonPath = ResolvePythonPath();

            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "-c \"import librosa\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        ShowPreFlightWarning();
                        return;
                    }
                    
                    bool exited = process.WaitForExit(3000);
                    if (exited && process.ExitCode == 0)
                    {
                        Log("[+] فحص البيئة ناجح: بايثون ومكتبة librosa متوفرتان للتحليل الصوتي.");
                    }
                    else
                    {
                        ShowPreFlightWarning();
                    }
                }
            }
            catch
            {
                ShowPreFlightWarning();
            }
        }

        private void ShowPreFlightWarning()
        {
            Log("[!] تحذير: لم يتم العثور على مكتبة librosa أو بايثون.");
            MessageBox.Show(
                "لم يتم العثور على مكتبة librosa أو بايثون. يرجى تثبيت بايثون وتشغيل الأمر التالي في الطرفية لتفعيل ميزات التحليل الصوتي:\n\npip install librosa numpy soundfile", 
                "تنبيه بيئة التشغيل", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Warning
            );
        }

        // ==========================================
        // Helper Methods from Program.cs
        // ==========================================

        private static string ReadLyricsFromSmi(string smiPath)
        {
            try
            {
                string content = File.ReadAllText(smiPath);
                var matches = Regex.Matches(content, @"<SYNC[^>]*>\s*<P[^>]*>(.*?)(?=<SYNC|</body|</sami|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var lines = new List<string>();

                foreach (Match m in matches)
                {
                    string lyricLine = m.Groups[1].Value;
                    lyricLine = Regex.Replace(lyricLine, @"<[^>]+>", "");
                    lyricLine = System.Net.WebUtility.HtmlDecode(lyricLine);
                    lyricLine = lyricLine.Replace("\u00a0", " ").Replace("&nbsp;", " ").Trim();
                    lyricLine = lyricLine.Replace("♪", "").Trim();

                    if (!string.IsNullOrWhiteSpace(lyricLine))
                    {
                        lines.Add(lyricLine);
                    }
                }

                if (lines.Count > 0)
                {
                    return string.Join(Environment.NewLine, lines);
                }
            }
            catch { }
            return "";
        }

        private static string CleanSongTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            string cleaned = title;
            cleaned = Regex.Replace(cleaned, @"\s*[\(\[][^\]\)]*(official|video|audio|lyrics|hd|cbr|kbps|dir\.\s*by|prod\.\s*by|exclusive|remix|edit|ft\.?|feat\.?)[^\]\)]*[\)\]]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*\b(256k|128kbps|cbr|mc|hd)\b.*", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"[_\s]+(256k|128k|mc|cbr)$", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*\b(ft\.?|feat\.?)\b.*", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private static string CleanArtistName(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return "";
            string cleaned = artist;
            cleaned = Regex.Replace(cleaned, @"\s*[\(\[][^\]\)]*(official|video|audio|lyrics|hd|cbr|kbps|dir\.\s*by|prod\.\s*by|exclusive|remix|edit|ft\.?|feat\.?)[^\]\)]*[\)\]]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*\b(ft\.?|feat\.?)\b.*", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private static async Task<string?> FetchGenreFromiTunes(string artist, string title)
        {
            if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(title))
                return null;

            try
            {
                string query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
                string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media=music&limit=1";
                
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array && resultsElement.GetArrayLength() > 0)
                        {
                            var firstResult = resultsElement[0];
                            if (firstResult.TryGetProperty("primaryGenreName", out var genreElement) && genreElement.ValueKind == JsonValueKind.String)
                            {
                                return genreElement.GetString();
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static async Task<string?> FetchLyricsFromApi(string artist, string title, string album, int duration)
        {
            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(album) && duration > 0)
            {
                try
                {
                    string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}&album_name={Uri.EscapeDataString(album)}&duration={duration}";
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("plainLyrics", out var plainElement) && plainElement.ValueKind == JsonValueKind.String)
                            {
                                string lyrics = plainElement.GetString() ?? "";
                                if (!string.IsNullOrWhiteSpace(lyrics))
                                    return lyrics;
                            }
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
            {
                try
                {
                    string query = $"{artist} {title}";
                    string url = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in doc.RootElement.EnumerateArray())
                                {
                                    string resTitle = item.TryGetProperty("trackName", out var t) ? t.GetString() ?? "" : "";
                                    string resArtist = item.TryGetProperty("artistName", out var a) ? a.GetString() ?? "" : "";

                                    if (IsMatch(title, artist, resTitle, resArtist))
                                    {
                                        if (item.TryGetProperty("plainLyrics", out var plainElement) && plainElement.ValueKind == JsonValueKind.String)
                                        {
                                            string lyrics = plainElement.GetString() ?? "";
                                            if (!string.IsNullOrWhiteSpace(lyrics))
                                                return lyrics;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private static string? cachedPythonPath = null;
        private static string ResolvePythonPath()
        {
            if (cachedPythonPath != null) return cachedPythonPath;

            if (IsPythonAvailable("python"))
            {
                cachedPythonPath = "python";
                return cachedPythonPath;
            }

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string pythonProgramsDir = Path.Combine(localAppData, @"Programs\Python");
                if (Directory.Exists(pythonProgramsDir))
                {
                    var pyDirs = Directory.GetDirectories(pythonProgramsDir, "Python*");
                    foreach (var pyDir in pyDirs.OrderByDescending(d => d))
                    {
                        string exePath = Path.Combine(pyDir, "python.exe");
                        if (File.Exists(exePath) && IsPythonAvailable(exePath))
                        {
                            cachedPythonPath = exePath;
                            return cachedPythonPath;
                        }
                    }
                }
            }
            catch { }

            cachedPythonPath = "python";
            return cachedPythonPath;
        }

        private static bool IsPythonAvailable(string exePath)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-c \"print('OK')\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null) return false;
                    bool exited = process.WaitForExit(2000);
                    return exited && process.ExitCode == 0 && process.StandardOutput.ReadToEnd().Trim() == "OK";
                }
            }
            catch
            {
                return false;
            }
        }

        private (string? mood, double confidence)? GetMoodFromPython(string filePath)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = Path.Combine(exeDir, "analyzer.py");

                if (!File.Exists(scriptPath))
                {
                    Log($"  [!] لم يتم العثور على سكربت المحلل الصوتي في: {scriptPath}");
                    return null;
                }

                string pythonPath = ResolvePythonPath();

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null) return null;

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        using (var doc = JsonDocument.Parse(output))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("mood", out var moodProp))
                            {
                                string? mood = moodProp.GetString();
                                double confidence = 0.0;
                                if (root.TryGetProperty("confidence", out var confProp))
                                {
                                    confidence = confProp.GetDouble();
                                }
                                return (mood, confidence);
                            }
                            else if (root.TryGetProperty("error", out var errorProp))
                            {
                                Log($"  [!] تحذير التحليل الصوتي لـ '{Path.GetFileName(filePath)}': {errorProp.GetString()}");
                            }
                        }
                    }
                    else
                    {
                        Log($"  [!] فشل عملية بايثون لـ '{Path.GetFileName(filePath)}' (كود الخروج: {process.ExitCode}). الخطأ: {error.Trim()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"  [!] خطأ أثناء استدعاء المحلل الصوتي: {ex.Message}");
            }
            return null;
        }

        private async Task<string?> FetchMoodFromLastFm(string artist, string title, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            try
            {
                string url = $"http://ws.audioscrobbler.com/2.0/?method=track.gettoptags&artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(title)}&api_key={Uri.EscapeDataString(apiKey)}&format=json";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("toptags", out var toptags) && toptags.TryGetProperty("tag", out var tagList) && tagList.ValueKind == JsonValueKind.Array)
                        {
                            var tags = new List<string>();
                            foreach (var tag in tagList.EnumerateArray())
                            {
                                if (tag.TryGetProperty("name", out var nameProp))
                                {
                                    string? tagName = nameProp.GetString();
                                    if (!string.IsNullOrEmpty(tagName))
                                    {
                                        tags.Add(tagName);
                                    }
                                }
                            }
                            
                            if (tags.Count > 0)
                            {
                                return ClassifyMoodFromLastFmTags(tags);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"  [!] خطأ Last.fm API: {ex.Message}");
            }
            return null;
        }

        private static string? ClassifyMoodFromLastFmTags(List<string> tags)
        {
            int gamingScore = 0;
            int happyScore = 0;
            int sadScore = 0;
            int chillScore = 0;

            var gamingWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gaming", "hype", "epic", "energetic", "phonk", "workout", "upbeat", "power", "aggressive" };
            var happyWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "happy", "cheerful", "fun", "summer", "feel good", "pop", "dance", "joy" };
            var sadWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sad", "melancholy", "depression", "emotional", "slow", "crying", "heartbreak", "mournful" };
            var chillWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chill", "chillout", "lofi", "ambient", "relaxed", "relaxing", "calm", "smooth", "acoustic", "mellow" };

            foreach (var tag in tags)
            {
                string t = tag.ToLowerInvariant();
                if (gamingWords.Any(w => t.Contains(w))) gamingScore++;
                if (happyWords.Any(w => t.Contains(w))) happyScore++;
                if (sadWords.Any(w => t.Contains(w))) sadScore++;
                if (chillWords.Any(w => t.Contains(w))) chillScore++;
            }

            if (gamingScore == 0 && happyScore == 0 && sadScore == 0 && chillScore == 0)
                return null;

            int max = Math.Max(Math.Max(gamingScore, happyScore), Math.Max(sadScore, chillScore));
            if (max == gamingScore) return "Gaming_Mood";
            if (max == happyScore) return "Happy_Vibes";
            if (max == sadScore) return "SAD";
            return "Chill";
        }

        private static bool IsMatch(string songTitle, string songArtist, string resultTitle, string resultArtist)
        {
            if (string.IsNullOrWhiteSpace(songTitle) || string.IsNullOrWhiteSpace(resultTitle))
                return false;

            string Normalize(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            string normSongTitle = Normalize(songTitle);
            string normResultTitle = Normalize(resultTitle);
            string normSongArtist = Normalize(songArtist ?? "");
            string normResultArtist = Normalize(resultArtist ?? "");

            bool titleMatch = normResultTitle.Contains(normSongTitle) || normSongTitle.Contains(normResultTitle);
            
            bool artistMatch = true;
            if (!string.IsNullOrEmpty(normSongArtist) && !string.IsNullOrEmpty(normResultArtist))
            {
                artistMatch = normResultArtist.Contains(normSongArtist) || normSongArtist.Contains(normResultArtist);
            }

            return titleMatch && artistMatch;
        }

        private static void ParseArtistTitleFromFileName(string fileNameWithoutExt, ref string artist, ref string title)
        {
            string[] delimiters = { " - ", " _ ", "  " };
            foreach (var delimiter in delimiters)
            {
                int index = fileNameWithoutExt.IndexOf(delimiter);
                if (index > 0)
                {
                    artist = fileNameWithoutExt.Substring(0, index).Trim();
                    title = fileNameWithoutExt.Substring(index + delimiter.Length).Trim();
                    title = Regex.Replace(title, @"(_\d+k|\(Lyrics\)|\(Official Music Video\)|_mc)$", "", RegexOptions.IgnoreCase).Trim();
                    return;
                }
            }
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unknown";

            char[] splitters = { '/', ';', ',' };
            foreach (var spl in splitters)
            {
                if (name.Contains(spl))
                {
                    name = name.Split(spl)[0].Trim();
                }
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string clean = name;
            foreach (char c in invalidChars)
            {
                clean = clean.Replace(c, ' ');
            }

            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            return string.IsNullOrEmpty(clean) ? "Unknown" : clean;
        }

        private static string MoveFileSafely(string sourcePath, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(targetDir, fileName);

            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            {
                return destPath;
            }

            if (File.Exists(destPath))
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                int counter = 1;
                do
                {
                    destPath = Path.Combine(targetDir, $"{nameWithoutExt}_{counter}{ext}");
                    counter++;
                } while (File.Exists(destPath));
            }

            File.Move(sourcePath, destPath);
            return destPath;
        }

        private static string GetMood(string title, string artist, string relativePath)
        {
            string fileName = Path.GetFileName(relativePath);
            string combined = $"{artist} {title} {fileName}".ToLowerInvariant();

            bool isSadFolder = relativePath.Split(Path.DirectorySeparatorChar)
                .Any(p => string.Equals(p, "SAD", StringComparison.OrdinalIgnoreCase));

            var sadKeywords = new[] {
                "505", "as it was", "another love", "pastlives", "let the world burn", "lovely", "daylight",
                "yomein wa leila", "yomein wa layla", "يومين وليلة", "عشق البنات", "يا لاللى", "منحوس", "man7os",
                "كان نفسي", "kan nefsy", "لا تذهب", "لوكا", "الأغنية التي أبكت الملايين", "ستاموني", "satamoni",
                "3al 3mom", "يا زميلي", "yazmeely", "ستامونى", "حكمة الأيام", "على باب السيما", "fady shwaya", "فضى شوية",
                "mockingbird", "darkness"
            };

            if (isSadFolder || sadKeywords.Any(k => combined.Contains(k)))
            {
                return "SAD";
            }

            var gamingKeywords = new[] { 
                "gaming", "hype", "phonk", "aggressive", "collaps", "enemy", "industry", "scary", "balkan", "unknown", 
                "funk", "soccer", "football", "هدف", "مباراة", "برشلونة", "دوري ابطال", "ريال", "مودريتش", "كفارادونا", 
                "سستم", "تفجير", "عنبه", "زوكش", "اسياد", "العو", "فكك", "جوارديولا", "كيفي كده", "دولي", "العبد", 
                "بطل عالم", "الأسفلت", "دورك جاي", "هصلا", "سكرتي", "صاصا", "مروان موسى", "أبو الأنوار", "الجوكر مع هرم",
                "search", "gangsta", "lovely bastards", "dancin"
            };
            if (gamingKeywords.Any(k => combined.Contains(k)))
            {
                return "Gaming_Mood";
            }

            var happyKeywords = new[] { 
                "happy", "vibes", "joy", "summer", "waka", "la la la", "jagger", "dance monkey", "بالمصري", 
                "بالبنط العريض", "سالمونيلا", "حلمي", "الحركه دي", "مكسرات", "أحمد سعد", "مكي", "الكبير", "بايلا",
                "baila", "fairytale", "habeeby da", "heseeny", "تووليت", "تملي معاك", "tamally", "dancin", "stereo hearts",
                "الغسالة", "ghasala"
            };
            if (happyKeywords.Any(k => combined.Contains(k)))
            {
                return "Happy_Vibes";
            }

            return "Chill";
        }

        // ==========================================
        // Kanban Drag & Drop and Sync Helpers
        // ==========================================

        private void ListBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && sender is ListBox lb)
            {
                int index = lb.IndexFromPoint(e.Location);
                if (index >= 0 && index < lb.Items.Count)
                {
                    lb.SelectedIndex = index;
                    var item = lb.Items[index] as KanbanSongItem;
                    if (item != null)
                    {
                        lb.DoDragDrop(item, DragDropEffects.Move);
                    }
                }
            }
        }

        private void ListBox_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(typeof(KanbanSongItem)))
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void ListBox_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(typeof(KanbanSongItem)))
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void ListBox_DragDrop(object? sender, DragEventArgs e)
        {
            if (sender is ListBox targetLb && e.Data != null && e.Data.GetDataPresent(typeof(KanbanSongItem)))
            {
                var item = e.Data.GetData(typeof(KanbanSongItem)) as KanbanSongItem;
                if (item != null)
                {
                    string targetMood = targetLb.Tag?.ToString() ?? "Chill";
                    ListBox? sourceLb = FindSourceListBox(item.FileName);
                    if (sourceLb != null)
                    {
                        string sourceMood = sourceLb.Tag?.ToString() ?? "Chill";
                        if (string.Equals(sourceMood, targetMood, StringComparison.OrdinalIgnoreCase))
                        {
                            return; // Same mood, do nothing
                        }

                        // Move visually
                        sourceLb.Items.Remove(item);
                        targetLb.Items.Add(item);

                        Log($"[Kanban] نقل الأغنية \"{item.ToString()}\" بسحبها وإفلاتها من {sourceMood} إلى {targetMood}.");

                        // Update metadata & file tags
                        UpdateSongMoodOverride(item.FileName, targetMood);
                    }
                }
            }
        }

        private ListBox? FindSourceListBox(string fileName)
        {
            foreach (var pair in moodListBoxes)
            {
                foreach (var obj in pair.Value.Items)
                {
                    if (obj is KanbanSongItem item && string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
            }
            return null;
        }

        private void UpdateGridRowMood(string fileName, string newMood)
        {
            if (dgvSongs.InvokeRequired)
            {
                dgvSongs.Invoke(new Action(() => UpdateGridRowMood(fileName, newMood)));
                return;
            }

            dgvSongs.CellValueChanged -= DgvSongs_CellValueChanged;
            foreach (DataGridViewRow row in dgvSongs.Rows)
            {
                if (row.Cells["FileName"].Value?.ToString() == fileName)
                {
                    row.Cells["ManualMood"].Value = newMood;
                    break;
                }
            }
            dgvSongs.CellValueChanged += DgvSongs_CellValueChanged;
        }

        private void SyncGridChangeToKanban(string fileName, string newMood)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SyncGridChangeToKanban(fileName, newMood)));
                return;
            }

            KanbanSongItem? foundItem = null;
            ListBox? sourceLb = null;

            foreach (var pair in moodListBoxes)
            {
                foreach (var obj in pair.Value.Items)
                {
                    if (obj is KanbanSongItem item && string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundItem = item;
                        sourceLb = pair.Value;
                        break;
                    }
                }
                if (foundItem != null) break;
            }

            if (foundItem != null && sourceLb != null)
            {
                string currentMood = sourceLb.Tag?.ToString() ?? "Chill";
                if (!string.Equals(currentMood, newMood, StringComparison.OrdinalIgnoreCase))
                {
                    sourceLb.Items.Remove(foundItem);
                    if (moodListBoxes.TryGetValue(newMood, out var targetLb))
                    {
                        targetLb.Items.Add(foundItem);
                    }
                }
            }
        }

        private void AddOrUpdateKanbanSong(string filePath, string artist, string title, string mood)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AddOrUpdateKanbanSong(filePath, artist, title, mood)));
                return;
            }

            string fileName = Path.GetFileName(filePath);
            
            // Remove existing if any
            KanbanSongItem? existingItem = null;
            ListBox? existingLb = null;
            foreach (var pair in moodListBoxes)
            {
                foreach (var obj in pair.Value.Items)
                {
                    if (obj is KanbanSongItem item && string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        existingItem = item;
                        existingLb = pair.Value;
                        break;
                    }
                }
                if (existingItem != null) break;
            }

            if (existingItem != null && existingLb != null)
            {
                existingLb.Items.Remove(existingItem);
            }

            var newItem = new KanbanSongItem
            {
                FileName = fileName,
                Artist = artist,
                Title = title
            };

            if (moodListBoxes.TryGetValue(mood, out var lb))
            {
                lb.Items.Add(newItem);
            }
            else if (moodListBoxes.TryGetValue("Chill", out var chillLb))
            {
                chillLb.Items.Add(newItem);
            }
        }
    }
}
