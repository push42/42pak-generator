using FortyTwoPak.Core.VpkFormat;

namespace FortyTwoPak.Core.Legacy;

public class EixEpkConverter
{
    public event Action<string>? OnLog;
    public event Action<Utils.ProgressInfo>? OnProgress;

    /// <summary>
    /// Controls how the source EPK data is decompressed.
    /// Auto (default) tries LZ4 first (FliegeV3), then falls back to LZO (40250).
    /// </summary>
    public EpkFormat SourceFormat { get; set; } = EpkFormat.Auto;

    public VpkConversionResult Convert(string eixPath, string vpkOutputPath, VpkBuildOptions options)
    {
        var result = new VpkConversionResult();
        var reader = new EixEpkReader { Format = SourceFormat };

        Log($"Opening EIX: {eixPath}");
        reader.Open(eixPath);

        Log($"Found {reader.Entries.Count} entries in EIX index.");

        // Create temporary directory for extracted files
        string tempDir = Path.Combine(Path.GetTempPath(), $"42pak-convert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            int extracted = 0;
            int skipped = 0;

            for (int i = 0; i < reader.Entries.Count; i++)
            {
                var entry = reader.Entries[i];

                try
                {
                    if (entry.CompressedType > 1)
                    {
                        Log($"  SKIP: {entry.FileName} (encrypted type {entry.CompressedTypeName})");
                        result.SkippedFiles.Add($"{entry.FileName} ({entry.CompressedTypeName})");
                        skipped++;
                        continue;
                    }

                    byte[] data = reader.ExtractFile(eixPath, entry);

                    string outputPath = Path.Combine(tempDir, entry.FileName.Replace('/', Path.DirectorySeparatorChar));
                    string? dir = Path.GetDirectoryName(outputPath);
                    if (dir != null) Directory.CreateDirectory(dir);

                    File.WriteAllBytes(outputPath, data);
                    extracted++;
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: {entry.FileName}: {ex.Message}");
                    result.Errors.Add($"{entry.FileName}: {ex.Message}");
                }

                OnProgress?.Invoke(new Utils.ProgressInfo
                {
                    CurrentFile = entry.FileName,
                    ProcessedFiles = i + 1,
                    TotalFiles = reader.Entries.Count
                });
            }

            Log($"Extracted {extracted} files, skipped {skipped}, errors {result.Errors.Count}.");

            if (extracted == 0)
            {
                result.Errors.Add("No files could be extracted from the EIX/EPK.");
                return result;
            }

            // Build VPK from extracted files
            Log("Building VPK archive...");
            var archive = new VpkArchive();
            archive.OnProgress += p => OnProgress?.Invoke(p);
            archive.Build(tempDir, vpkOutputPath, options);

            result.ConvertedFiles = extracted;
            result.TotalEntries = reader.Entries.Count;
            Log($"VPK created: {vpkOutputPath}");
        }
        finally
        {
            // Clean up temp directory
            try { Directory.Delete(tempDir, true); } catch { /* best effort */ }
        }

        return result;
    }

    private void Log(string message) => OnLog?.Invoke(message);
}

public class VpkConversionResult
{
    public int ConvertedFiles { get; set; }
    public int TotalEntries { get; set; }
    public List<string> SkippedFiles { get; } = new();
    public List<string> Errors { get; } = new();
    public bool HasErrors => Errors.Count > 0;
}
