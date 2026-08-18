---

# 🍬 CandyDataEditor

**CandyDataEditor** is a desktop-grade SQLite database management application built with **C# .NET Blazor** and **Bootstrap 5**.
Designed specifically for game development workflows, it pairs relational data navigation with dynamic rich-text editing—featuring font icon previewing and spellcheck dictionaries.

---

## 🚀 Features

* **Database Dashboard**: Track active database path status, view recent database file history with persistent limit management, and monitor spellcheck dictionary health.
* **Interactive Data Sidebar**: Deep inspection of SQLite tables and views with real-time filtering, primary key mapping, and accordion record navigation.
* **Rich Text & Spellcheck Editor**: Custom TipTap JS integration providing inline game-icon font ligatures (e.g., `[place]`, `[defense]`) and spellcheck validation.
* **Multi-Format Export Engine**: Multi-table data export support for TSV, CSV, XML, JSON, and standalone Interactive HTML Cards.
* **UI Light/Dark Mode Support**: Seamless switching between light and dark themes for improved usability in different lighting conditions.

---

## 🛠️ Project Structure

```text
CandyDataEditor.sln
│
├── CandyDataEditor/               # Main C# Blazor Project
│   ├── Components/
│   │   ├── RibbonMenuBar.razor    # Top navigation, file picker & app settings
│   │   └── SpellcheckInitializer.razor
│   ├── Pages/
│   │   └── Home.razor             # Primary workspace dashboard & recent DBs
│   ├── Services/
│   │   ├── SqliteDataService.cs   # SQLite engine & database state manager
│   │   └── GameDictionaryService.cs # Custom wordlists & dictionary loader
│   ├── Shared/
│   │   └── MainLayout.razor       # App shell, sidebar accordions & routing
│   └── wwwroot/
│       └── js/
│           └── tiptap.bundle.js   # Compiled esbuild output
│
└── TipTap/                        # Standalone JS Bundle Project
    ├── package.json               # TipTap & esbuild dependency manifest
    ├── tiptap-entry.js            # Node source entry file
    └── node_modules/              # Managed via npm

```

---

## ⚙️ Getting Started

### Prerequisites

* **.NET 8.0 SDK**
* **Node.js** (v18+) & **npm** (installed globally for `esbuild` bundling)
* **Visual Studio 2022**

### Setup & Run

1. **Clone the Repository**
```bash
git clone https://github.com/YourOrg/CandyDataEditor.git
cd CandyDataEditor

```


2. **Open in Visual Studio**
* Open `CandyDataEditor.sln`.
* Set **`CandyDataEditor`** as the Startup Project (*Right-click project $\rightarrow$ Set as Startup Project*).


3. **Build & Launch**
* Press **`F5`** or **`Ctrl + F5`**.
* The MSBuild system automatically runs `npm install` inside `/TipTap` on first build, bundles `tiptap-entry.js` via `esbuild`, and writes the compiled output to `wwwroot/js/tiptap.bundle.js`.



## 📖 Custom Dictionaries

Spellcheck dictionaries reside in the application's bundled dictionaries directory. You can manage wordlists directly within the app:

1. Open **File** $\rightarrow$ **Settings** $\rightarrow$ **Dictionaries**.
2. Add or remove custom terms (saved to `custom_user_words.txt`).
3. Click **Open Folder in Explorer** to add external `.txt` vocabulary files directly.

