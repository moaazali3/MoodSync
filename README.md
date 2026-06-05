# MoodSync 🎵

**MoodSync** is a smart, desktop-based Music Organizer and Mood Classifier application. It helps you keep your local music library clean, tagged, and organized by classifying songs into four core moods: **Chill**, **SAD**, **Happy Vibes**, and **Gaming Mood**.

Built with a **C# .NET 10 Windows Forms** frontend and a local **Python (Librosa + NumPy)** analysis backend, MoodSync integrates multiple metadata APIs to deliver a comprehensive organization tool.

---

## ✨ Features

- 🧠 **Hybrid Mood Classification**:
  1. **Metadata-driven**: Queries the **Last.fm API** for community tags.
  2. **Acoustic Analysis**: Analyzes audio files locally using Python, extracting **Tempo (BPM)** and **RMS Energy** to classify moods with confidence scores.
  3. **Keyword Matching**: Falls back to rule-based keyword matching on song metadata and filenames.
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

## 🚀 Getting Started

### Prerequisites

1. **.NET 10.0 SDK / Runtime**: Ensure you have the .NET 10 desktop runtime installed.
2. **Python 3**: Install Python 3 and the required packages:
   ```bash
   pip install librosa numpy soundfile
   ```

### Configuration

Create the following files in the root folder to activate external services:
- `lastfm_key.txt`: Place your Last.fm API Key here to enable cloud mood tagging.

---

## 📄 License

This project is licensed under the MIT License.
