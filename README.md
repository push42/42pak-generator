<p align="center">
  <img src="assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

<p align="center">
  <strong>🌐 Language / Język / Limbă / Dil / ภาษา / Idioma / Lingua / Idioma:</strong><br>
  <a href="README.md">🇬🇧 English</a> •
  <a href="README.pl.md">🇵🇱 Polski</a> •
  <a href="README.ro.md">🇷🇴 Română</a> •
  <a href="README.tr.md">🇹🇷 Türkçe</a> •
  <a href="README.th.md">🇹🇭 ไทย</a> •
  <a href="README.pt.md">🇧🇷 Português</a> •
  <a href="README.it.md">🇮🇹 Italiano</a> •
  <a href="README.es.md">🇪🇸 Español</a>
</p>

# 42pak-generator

A modern, open-source pak file manager for the Metin2 private server community. Replaces the legacy EIX/EPK archive format with the new **VPK** format featuring AES-256-GCM encryption, LZ4/Zstandard/Brotli compression, BLAKE3 hashing, and HMAC-SHA256 tamper detection.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)


> **Screenshots:** See [PREVIEW.md](PREVIEW.md) for a full gallery of the GUI in both dark and light themes.

---

## Features

- **Create VPK archives** - Pack directories into single `.vpk` files with optional encryption and compression
- **Manage existing archives** - Browse, search, extract, and validate VPK archives
- **Convert EIX/EPK to VPK** - One-click migration from legacy EterPack format (supports 40250, FliegeV3, and MartySama 5.8 variants)
- **AES-256-GCM encryption** - Per-file authenticated encryption with unique nonces
- **LZ4 / Zstandard / Brotli compression** - Choose the algorithm that fits your needs
- **BLAKE3 content hashing** - Cryptographic integrity verification for every file
- **HMAC-SHA256** - Archive-level tamper detection
- **Filename mangling** - Optional obfuscation of file paths within archives
- **Full CLI** - Standalone `42pak-cli` with pack, unpack, list, info, verify, diff, migrate, search, check-duplicates, and watch commands
- **Metin2 C++ integration** - Drop-in client and server loader code for 40250, FliegeV3, and MartySama 5.8 source trees

## VPK vs EIX/EPK Comparison

| Feature | EIX/EPK (Legacy) | VPK (42pak) |
|---------|:-:|:-:|
| Encryption | TEA / Panama / HybridCrypt | AES-256-GCM |
| Compression | LZO | LZ4 / Zstandard / Brotli |
| Integrity | CRC32 | BLAKE3 + HMAC-SHA256 |
| File format | Dual file (.eix + .epk) | Single file (.vpk) |
| Archive count | 512 max entries | Unlimited |
| Filename length | 160 bytes | 512 bytes (UTF-8) |
| Key derivation | Hardcoded keys | PBKDF2-SHA512 (200k iterations) |
| Tamper detection | None | HMAC-SHA256 whole-archive |

---

# Personal advertisement <3
### If you are operating your own server i happily invite you to list it on our newly launched platform!
- Free, Anti-Bot Secured, Free Vote-API, Dev Serverlisting-Integration for your platform & more -> [metin2-toplist.net](https://metin2-toplist.net/)

<href href="https://metin2-toplist.net/" align="center">
  <img src="https://github.com/push42/42pak-generator/blob/master/assets/Screenshot%202026-04-26%20024832.png" alt="metin2-toplist.net" width="100%" />
</href>


---

## Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 version 1809+ (for WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre-installed)

### Build

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### Run the GUI

```bash
dotnet run --project src/FortyTwoPak.UI
```

### Run the CLI

```bash
dotnet run --project src/FortyTwoPak.CLI -- <command> [options]
```

### Run Tests

```bash
dotnet test
```

### Publish (Portable / Installer)

```powershell
# CLI single-exe (~65 MB)
.\publish.ps1 -Target CLI

# GUI portable folder (~163 MB)
.\publish.ps1 -Target GUI

# Both + Inno Setup installer
.\publish.ps1 -Target All
```

---

## CLI Usage

The standalone CLI (`42pak-cli`) supports all operations:

```
42pak-cli pack <SOURCE_DIR> [--output <FILE>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <PASS>] [--threads <N>] [--dry-run]
42pak-cli unpack <ARCHIVE> <OUTPUT_DIR> [--passphrase <PASS>] [--filter <PATTERN>]
42pak-cli list <ARCHIVE> [--passphrase <PASS>] [--filter <PATTERN>] [--json]
42pak-cli info <ARCHIVE> [--passphrase <PASS>] [--json]
42pak-cli verify <ARCHIVE> [--passphrase <PASS>] [--filter <PATTERN>] [--json]
42pak-cli diff <ARCHIVE_A> <ARCHIVE_B> [--passphrase <PASS>] [--json]
42pak-cli migrate <LEGACY_ARCHIVE> [--output <FILE>] [--compression <TYPE>] [--passphrase <PASS>]
42pak-cli search <WORKSPACE_DIR> <FILENAME_OR_PATTERN>
42pak-cli check-duplicates <WORKSPACE_DIR> [--read-index]
42pak-cli watch <SOURCE_DIR> [--output <FILE>] [--debounce <MS>]
```

**Flags:** `-q` / `--quiet` suppresses all output except errors. `--json` outputs structured JSON for scriptable pipelines.

**Exit codes:** 0 = success, 1 = error, 2 = integrity failure, 3 = passphrase incorrect.

---

## Project Structure

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # Core library: VPK read/write, crypto, compression, legacy import
│   │   ├── VpkFormat/               # VPK header, entry, archive classes
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # LZ4 / Zstandard / Brotli compressors
│   │   ├── Legacy/                  # EIX/EPK reader and converter (40250 + FliegeV3 + MartySama 5.8)
│   │   ├── Cli/                     # Shared CLI handler (12 commands)
│   │   └── Utils/                   # Filename mangling, progress reporting
│   ├── FortyTwoPak.CLI/             # Standalone CLI tool (42pak-cli)
│   ├── FortyTwoPak.UI/              # WebView2 desktop application
│   │   ├── MainWindow.cs            # WinForms host with WebView2 control
│   │   ├── BridgeService.cs         # JavaScript <-> C# interop bridge
│   │   └── wwwroot/                 # HTML/CSS/JS frontend (6 tabs, dark/light theme)
│   └── FortyTwoPak.Tests/           # xUnit test suite (22 tests)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # C++ integration for 40250/ClientVS22 (HybridCrypt)
│   │   ├── MartySama58/             # C++ integration for MartySama 5.8 (Boost + HybridCrypt)
│   │   └── FliegeV3/                # C++ integration for FliegeV3 (XTEA/LZ4)
│   └── Server/                      # Shared server-side VPK handler
├── docs/
│   └── FORMAT_SPEC.md               # VPK binary format specification
├── publish.ps1                      # Publish script (CLI/GUI/Installer/All)
├── installer.iss                    # Inno Setup 6 installer script
├── assets/                          # Screenshots and banner images
└── build/                           # Build output
```

---

## VPK File Format

Single-file archive with this binary layout:

```
+-------------------------------------+
| VpkHeader (512 bytes, fixed)        |  Magic "42PK", version, entry count,
|                                     |  encryption flag, salt, author, etc.
+-------------------------------------+
| Data Block 0 (aligned to 4096)      |  File content (compressed + encrypted)
+-------------------------------------+
| Data Block 1 (aligned to 4096)      |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| Entry Table (variable size)         |  Array of VpkEntry records. If encrypted,
|                                     |  wrapped in AES-GCM (nonce + tag + data).
+-------------------------------------+
| HMAC-SHA256 (32 bytes)              |  Covers everything above. Zero if unsigned.
+-------------------------------------+
```

### Encryption Pipeline

For each file: `Original -> LZ4 Compress -> AES-256-GCM Encrypt -> Store`

On extraction: `Read -> AES-256-GCM Decrypt -> LZ4 Decompress -> BLAKE3 Verify`

Key derivation: `PBKDF2-SHA512("42PK-v1:" + passphrase, salt, 100000 iterations) -> 64 bytes`
- First 32 bytes: AES-256 key
- Last 32 bytes: HMAC-SHA256 key

For the full binary specification, see [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md).

---

## Usage

### Creating a VPK Archive (GUI)

1. Open 42pak-generator
2. Go to **Create Pak** tab
3. Select source directory containing files to pack
4. Choose output `.vpk` file path
5. Configure options:
   - **Encryption**: Toggle on and enter a passphrase
   - **Compression Level**: 0 (none) to 12 (maximum)
   - **Filename Mangling**: Obfuscate paths within the archive
   - **Author / Comment**: Optional metadata
6. Click **Build**

### Converting EIX/EPK to VPK

1. Go to **Convert** tab
2. Select the `.eix` file (the `.epk` must be in the same directory)
3. Choose output `.vpk` path
4. Optionally set encryption passphrase
5. Click **Convert**

### Metin2 Client Integration

Three integration profiles are provided for different source trees:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **MartySama 5.8** (Boost, HybridCrypt, LZO): [Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md](Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### Metin2 Server Integration

See [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) for server-side VPK reading.

---

## Technologies

| Component | Technology |
|-----------|------------|
| Runtime | .NET 8.0 (C#) |
| UI | WebView2 (WinForms host) |
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Encryption | AES-256-GCM via `System.Security.Cryptography` |
| Key Derivation | PBKDF2-SHA512 (200,000 iterations) |
| Hashing | BLAKE3 via [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| Compression | LZ4 via [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard via [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (built-in) |
| Tamper Detection | HMAC-SHA256 |
| C++ Crypto | OpenSSL 1.1+ |
| Testing | xUnit |

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Acknowledgments

- The Metin2 private server community for keeping the game alive
- The original YMIR Entertainment EterPack format for providing the foundation
- [BLAKE3 team](https://github.com/BLAKE3-team/BLAKE3) for the fast hash function
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) for the .NET LZ4 binding
- **[MartySama](https://github.com/martysama0134)** - for his excellent tutorials and clean codebase
- **Fliege** - for the inspiring server files and systems
- **[TMP4](https://metin2.dev/profile/27454-tmp4/)** - for the high-quality reference files and tools

Without their work, this project wouldn't have been possible. 🙏
