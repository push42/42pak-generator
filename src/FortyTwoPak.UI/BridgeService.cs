using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using FortyTwoPak.Core.Compression;
using FortyTwoPak.Core.Legacy;
using FortyTwoPak.Core.VpkFormat;

namespace FortyTwoPak.UI;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class BridgeService
{
    private readonly Form _owner;
    private VpkArchive? _currentArchive;
    private string? _currentArchivePath;

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "42pak-generator");
    private static readonly string RecentFilePath = Path.Combine(SettingsDir, "recent.json");

    public BridgeService(Form owner) => _owner = owner;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public string BuildVpk(string sourceDir, string outputPath, string optionsJson)
    {
        try
        {
            var options = JsonSerializer.Deserialize<VpkBuildOptions>(optionsJson, _jsonOpts)
                ?? new VpkBuildOptions();

            if (!string.IsNullOrEmpty(options.Passphrase))
                options.EnableEncryption = true;

            var archive = new VpkArchive();
            archive.Build(sourceDir, outputPath, options);
            _currentArchive = archive;

            return JsonSerializer.Serialize(new
            {
                success = true,
                entryCount = archive.Entries.Count,
                message = $"VPK created with {archive.Entries.Count} files."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string OpenVpk(string filePath, string passphrase)
    {
        try
        {
            _currentArchive = VpkArchive.Open(filePath,
                string.IsNullOrEmpty(passphrase) ? null : passphrase);
            _currentArchivePath = filePath;
            AddRecentFile(filePath);

            var entries = _currentArchive.Entries.Select(e => new
            {
                e.FileName,
                e.OriginalSize,
                e.StoredSize,
                e.IsCompressed,
                e.IsEncrypted,
                ratio = e.OriginalSize > 0
                    ? Math.Round((1.0 - (double)e.StoredSize / e.OriginalSize) * 100, 1)
                    : 0
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    _currentArchive.Header.Version,
                    _currentArchive.Header.EntryCount,
                    _currentArchive.Header.IsEncrypted,
                    _currentArchive.Header.CompressionLevel,
                    CompressionAlgorithm = _currentArchive.Header.CompressionAlgorithm.ToString(),
                    _currentArchive.Header.FileNamesMangled,
                    _currentArchive.Header.Author,
                    _currentArchive.Header.Comment,
                    CreatedAt = new DateTime(_currentArchive.Header.CreatedAtUtcTicks, DateTimeKind.Utc).ToString("o")
                },
                entries
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string ExtractAll(string outputDir, string passphrase)
    {
        try
        {
            if (_currentArchive == null)
                return JsonSerializer.Serialize(new { success = false, message = "No archive is open." });

            _currentArchive.ExtractAll(outputDir,
                string.IsNullOrEmpty(passphrase) ? null : passphrase);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Extracted {_currentArchive.Entries.Count} files to {outputDir}"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string ValidateVpk(string passphrase)
    {
        try
        {
            if (_currentArchive == null)
                return JsonSerializer.Serialize(new { success = false, message = "No archive is open." });

            var result = _currentArchive.Validate(
                string.IsNullOrEmpty(passphrase) ? null : passphrase);

            return JsonSerializer.Serialize(new
            {
                success = true,
                isValid = result.IsValid,
                validFiles = result.ValidFiles,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string ConvertEixEpk(string eixPath, string vpkOutputPath, string optionsJson)
    {
        try
        {
            var options = JsonSerializer.Deserialize<VpkBuildOptions>(optionsJson, _jsonOpts)
                ?? new VpkBuildOptions();

            if (!string.IsNullOrEmpty(options.Passphrase))
                options.EnableEncryption = true;

            var converter = new EixEpkConverter();
            var result = converter.Convert(eixPath, vpkOutputPath, options);

            return JsonSerializer.Serialize(new
            {
                success = true,
                convertedFiles = result.ConvertedFiles,
                totalEntries = result.TotalEntries,
                skippedFiles = result.SkippedFiles,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string PickFolder()
    {
        string result = "";
        _owner.Invoke(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select folder",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(_owner) == DialogResult.OK)
                result = dialog.SelectedPath;
        });
        return result;
    }

    public string PickFile(string filter)
    {
        string result = "";
        _owner.Invoke(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = filter,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(_owner) == DialogResult.OK)
                result = dialog.FileName;
        });
        return result;
    }

    public string PickSaveFile(string filter, string defaultExt)
    {
        string result = "";
        _owner.Invoke(() =>
        {
            using var dialog = new SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(_owner) == DialogResult.OK)
                result = dialog.FileName;
        });
        return result;
    }

    public string ReadEixListing(string eixPath)
    {
        try
        {
            var reader = new EixEpkReader();
            reader.Open(eixPath);

            var entries = reader.Entries.Select(e => new
            {
                e.FileName,
                e.DataSize,
                e.RealDataSize,
                e.CompressedTypeName,
                canExtract = e.CompressedType <= 1
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = reader.Entries.Count,
                entries
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string ExtractSingleFile(string fileName, string outputDir, string passphrase)
    {
        try
        {
            if (_currentArchive == null)
                return JsonSerializer.Serialize(new { success = false, message = "No archive is open." });

            var data = _currentArchive.ExtractFile(fileName,
                string.IsNullOrEmpty(passphrase) ? null : passphrase);
            var outPath = Path.Combine(outputDir, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllBytes(outPath, data);

            return JsonSerializer.Serialize(new { success = true, message = $"Extracted {fileName} to {outputDir}" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string GetArchiveStats()
    {
        try
        {
            if (_currentArchive == null)
                return JsonSerializer.Serialize(new { success = false, message = "No archive is open." });

            long totalOriginal = 0, totalStored = 0;
            foreach (var e in _currentArchive.Entries)
            {
                totalOriginal += e.OriginalSize;
                totalStored += e.StoredSize;
            }

            double overallRatio = totalOriginal > 0
                ? Math.Round((1.0 - (double)totalStored / totalOriginal) * 100, 1)
                : 0;

            long archiveSize = _currentArchivePath != null && File.Exists(_currentArchivePath)
                ? new FileInfo(_currentArchivePath).Length
                : 0;

            return JsonSerializer.Serialize(new
            {
                success = true,
                totalFiles = _currentArchive.Entries.Count,
                totalOriginal,
                totalStored,
                overallRatio,
                archiveSize
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string GetRecentFiles()
    {
        try
        {
            if (!File.Exists(RecentFilePath))
                return JsonSerializer.Serialize(new { success = true, files = Array.Empty<string>() });

            var files = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentFilePath)) ?? [];
            files = files.Where(File.Exists).ToList();
            return JsonSerializer.Serialize(new { success = true, files });
        }
        catch
        {
            return JsonSerializer.Serialize(new { success = true, files = Array.Empty<string>() });
        }
    }

    private void AddRecentFile(string filePath)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            List<string> files = [];
            if (File.Exists(RecentFilePath))
                files = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentFilePath)) ?? [];

            files.Remove(filePath);
            files.Insert(0, filePath);
            if (files.Count > 10) files = files.Take(10).ToList();

            File.WriteAllText(RecentFilePath, JsonSerializer.Serialize(files));
        }
        catch { /* best effort */ }
    }

    public string ClearRecentFiles()
    {
        try
        {
            if (File.Exists(RecentFilePath))
                File.Delete(RecentFilePath);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}
