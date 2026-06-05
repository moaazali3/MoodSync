# MoodSync 🎵

**MoodSync** is a smart, desktop-based Music Organizer and Mood Classifier application. It helps you keep your local music library clean, tagged, and organized by classifying songs into four core moods: **Chill**, **SAD**, **Happy Vibes**, and **Gaming Mood**.

Built with a **C# .NET 10 Windows Forms** frontend and a local **Python (Librosa + NumPy)** analysis backend, MoodSync integrates multiple metadata APIs to deliver a comprehensive organization tool.

---

## ✨ Features

- 🧠 **Hybrid Mood Classification Pipeline**:
  1. **Metadata-driven (Cloud)**: Queries the **Last.fm API** for community tags.
  2. **Acoustic Analysis (Local)**: Analyzes audio files locally using Python, extracting **Tempo (BPM)** and **RMS Energy** to classify moods with confidence scores.
  3. **Keyword Matching (Fallback)**: Falls back to rule-based keyword matching on song metadata and filenames.
- 🏷️ **Automated Tagging & Metadata**:
  - Syncs genre information automatically using the **iTunes Search API**.
  - Fetches and embeds lyrics directly into audio tags and localized `.smi` files using the **LRCLib API**.
- 📋 **Visual Kanban Board**: View your library categorized into mood columns. Drag-and-drop tracks between columns to manually adjust and override classifications.
- 🗂️ **Library Organization**: Automatically organizes files into genre folders on your disk.
- 🎶 **Playlist Generation**: Automatically generates and updates `.m3u` playlists for each mood category.

---

## 🛠️ Technology Stack

- **Frontend**: C# Windows Forms (.NET 10.0-windows)
- **Metadata Libraries**: [TagLib# (TagLibSharp)](https://github.com/mono/taglib-sharp)
- **Audio Processing**: Python 3 (using `librosa` and `numpy` for signal processing)
- **APIs**:
  - [iTunes Search API](https://performance-developer.apple.com/documentation/itunes_search_api) (Genres)
  - [LRCLib API](https://github.com/tranxuanthang/lrclib) (Lyrics)
  - [Last.fm API](https://www.last.fm/api) (Mood tags)

---

## 📂 Project Structure

```
├── MusicOrganizer/
│   ├── MainForm.cs           # Main GUI Layout and Logic
│   ├── Models.cs             # Data classes (PlaylistEntry, OverrideEntry, KanbanSongItem)
│   ├── Program.cs            # Entry point for the Windows Forms App
│   ├── MusicOrganizer.csproj # .NET 10 Project File
│   └── analyzer.py           # Python script for local acoustic features extraction
```

---

## 🚀 Getting Started

### 1. Prerequisites

- **.NET 10.0 SDK / Runtime**: Ensure you have the .NET 10 desktop runtime installed.
- **Python 3**: Install Python 3 and the required libraries for local audio analysis:
  ```bash
  pip install librosa numpy soundfile
  ```

### 2. Configuration

To enable the cloud-based mood tagging feature, configure the Last.fm API Key:
1. Create a file named `lastfm_key.txt` in your selected music folder.
2. Paste your **Last.fm API Key** into it as a single line.

*Note: If `lastfm_key.txt` is missing, the application will automatically bypass the cloud tag lookup and fallback to local Python analysis and keyword matching.*

### 3. Build & Run

To run the application locally:
1. Open a terminal and navigate to the project folder:
   ```bash
   cd MusicOrganizer
   ```
2. Run the application:
   ```bash
   dotnet run
   ```
   *(MSBuild will automatically copy the `analyzer.py` script to the output directory so the application can invoke it dynamically.)*

---

## 📖 How to Use & Workflow

1. **Select Music Folder**: Click on the **"اختر المجلد" (Browse Folder)** button and select the directory containing your music files.
2. **Start Classification**: Click on the green **"ابدأ التصنيف" (Start Classification)** button. The application will:
   - Perform a pre-flight check to verify Python and `librosa` are installed.
   - Scan all audio files recursively (ignoring specified non-music paths or religious files).
   - Sync and embed missing metadata, genres, and lyrics into the files.
   - Run the classification pipeline to determine the mood of each track.
   - Move the physical files on your disk into subdirectories named after their respective genres.
3. **Explore and Refine (Kanban Board)**:
   - Switch to the **"منظم القوائم (Kanban Board)"** tab.
   - You will see the tracks loaded into four columns: **Chill**, **SAD**, **Happy Vibes**, and **Gaming Mood**.
   - **Drag and Drop**: If a song is classified incorrectly, drag and drop it from one list to another. The application will instantly update the audio file's tag, save the choice in the local overrides file, and regenerate the `.m3u` playlists.
   - **Table Editor**: Alternatively, in the first tab, you can change the mood from the dropdown cell in the songs table, which triggers the same synchronization.

---

## 💾 Generated Files

- **`metadata_overrides.json`**: Created in the root of your music folder. This file caches manual corrections you make to the songs (mood, artist, or title overrides) so that running the organizer again won't reset your manual adjustments.
- **`.m3u` Playlists**: The application automatically creates a master `playlist.m3u` file containing all songs, plus individual mood playlists (`Chill.m3u`, `SAD.m3u`, `Happy_Vibes.m3u`, `Gaming_Mood.m3u`). These are saved directly in the selected music directory.

---

## 📄 License

This project is licensed under the MIT License.
