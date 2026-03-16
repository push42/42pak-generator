using FortyTwoPak.Core.Compression;
using FortyTwoPak.Core.Legacy;
using FortyTwoPak.Core.VpkFormat;

namespace FortyTwoPak.UI;

static class CliHandler
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var cmdArgs = args.Skip(1).ToArray();

        return command switch
        {
            "build" => Build(cmdArgs),
            "extract" => Extract(cmdArgs),
            "info" => Info(cmdArgs),
            "validate" => Validate(cmdArgs),
            "convert" => Convert(cmdArgs),
            "help" or "--help" or "-h" => ShowHelp(),
            _ => UnknownCommand(command)
        };
    }

    static int Build(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: FortyTwoPak build <sourceDir> <output.vpk> [options]"); return 1; }

        string sourceDir = args[0];
        string outputPath = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"Source directory not found: {sourceDir}");
            return 1;
        }

        var options = new VpkBuildOptions
        {
            Passphrase = flags.GetValueOrDefault("passphrase"),
            CompressionLevel = int.TryParse(flags.GetValueOrDefault("compression"), out var cl) ? cl : 3,
            CompressionAlgorithm = Enum.TryParse<CompressionAlgorithm>(flags.GetValueOrDefault("algorithm", "LZ4"), true, out var ca) ? ca : CompressionAlgorithm.LZ4,
            MangleFileNames = flags.ContainsKey("mangle"),
            Author = flags.GetValueOrDefault("author"),
            Comment = flags.GetValueOrDefault("comment")
        };

        try
        {
            Console.WriteLine($"Building VPK from: {sourceDir}");
            var archive = new VpkArchive();
            archive.Build(sourceDir, outputPath, options);
            Console.WriteLine($"Created {outputPath} with {archive.Entries.Count} files.");
            Console.WriteLine($"  Algorithm: {options.CompressionAlgorithm}, Level: {options.CompressionLevel}");
            Console.WriteLine($"  Encrypted: {(options.Passphrase != null ? "Yes" : "No")}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Build failed: {ex.Message}");
            return 1;
        }
    }

    static int Extract(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: FortyTwoPak extract <archive.vpk> <outputDir> [--passphrase X]"); return 1; }

        string archivePath = args[0];
        string outputDir = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"File not found: {archivePath}");
            return 1;
        }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            Console.WriteLine($"Opening: {archivePath}");
            var archive = VpkArchive.Open(archivePath, passphrase);
            Console.WriteLine($"Extracting {archive.Entries.Count} files to: {outputDir}");
            archive.ExtractAll(outputDir, passphrase);
            Console.WriteLine("Extraction complete.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Extraction failed: {ex.Message}");
            return 1;
        }
    }

    static int Info(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: FortyTwoPak info <archive.vpk> [--passphrase X]"); return 1; }

        string archivePath = args[0];
        var flags = ParseFlags(args.Skip(1));

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"File not found: {archivePath}");
            return 1;
        }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            var archive = VpkArchive.Open(archivePath, passphrase);
            var h = archive.Header;

            Console.WriteLine($"Archive: {Path.GetFileName(archivePath)}");
            Console.WriteLine($"  Version:     {h.Version}");
            Console.WriteLine($"  Files:       {h.EntryCount}");
            Console.WriteLine($"  Encrypted:   {(h.IsEncrypted ? "Yes" : "No")}");
            Console.WriteLine($"  Algorithm:   {h.CompressionAlgorithm}");
            Console.WriteLine($"  Level:       {h.CompressionLevel}");
            Console.WriteLine($"  Mangled:     {(h.FileNamesMangled ? "Yes" : "No")}");
            Console.WriteLine($"  Author:      {h.Author ?? "—"}");
            Console.WriteLine($"  Comment:     {h.Comment ?? "—"}");
            Console.WriteLine($"  Created:     {new DateTime(h.CreatedAtUtcTicks, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();

            long totalOriginal = 0, totalStored = 0;
            foreach (var e in archive.Entries)
            {
                totalOriginal += e.OriginalSize;
                totalStored += e.StoredSize;
            }

            Console.WriteLine($"  Total size:  {FormatSize(totalOriginal)} (original)");
            Console.WriteLine($"  Stored size: {FormatSize(totalStored)} (compressed)");
            if (totalOriginal > 0)
                Console.WriteLine($"  Ratio:       {(1.0 - (double)totalStored / totalOriginal) * 100:F1}%");

            Console.WriteLine();
            Console.WriteLine($"  {"File",-50} {"Original",10} {"Stored",10} {"Ratio",7}  Flags");
            Console.WriteLine($"  {new string('-', 50)} {new string('-', 10)} {new string('-', 10)} {new string('-', 7)}  {new string('-', 10)}");
            foreach (var e in archive.Entries)
            {
                double ratio = e.OriginalSize > 0 ? (1.0 - (double)e.StoredSize / e.OriginalSize) * 100 : 0;
                string flagStr = string.Join(" ", new[]
                {
                    e.IsEncrypted ? "ENC" : null,
                    e.IsCompressed ? "CMP" : null
                }.Where(f => f != null));
                Console.WriteLine($"  {e.FileName,-50} {FormatSize(e.OriginalSize),10} {FormatSize(e.StoredSize),10} {ratio,6:F1}%  {flagStr}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read archive: {ex.Message}");
            return 1;
        }
    }

    static int Validate(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: FortyTwoPak validate <archive.vpk> [--passphrase X]"); return 1; }

        string archivePath = args[0];
        var flags = ParseFlags(args.Skip(1));

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"File not found: {archivePath}");
            return 1;
        }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            var archive = VpkArchive.Open(archivePath, passphrase);
            var result = archive.Validate(passphrase);

            if (result.IsValid)
            {
                Console.WriteLine($"VALID — {result.ValidFiles} files verified OK.");
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"INVALID — {result.Errors.Count} error(s):");
                foreach (var err in result.Errors)
                    Console.Error.WriteLine($"  - {err}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }
    }

    static int Convert(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: FortyTwoPak convert <input.eix> <output.vpk> [options]"); return 1; }

        string eixPath = args[0];
        string outputPath = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!File.Exists(eixPath))
        {
            Console.Error.WriteLine($"File not found: {eixPath}");
            return 1;
        }

        var options = new VpkBuildOptions
        {
            Passphrase = flags.GetValueOrDefault("passphrase"),
            CompressionLevel = int.TryParse(flags.GetValueOrDefault("compression"), out var cl) ? cl : 3,
            CompressionAlgorithm = Enum.TryParse<CompressionAlgorithm>(flags.GetValueOrDefault("algorithm", "LZ4"), true, out var ca) ? ca : CompressionAlgorithm.LZ4
        };

        try
        {
            Console.WriteLine($"Converting: {eixPath}");
            var converter = new EixEpkConverter();
            var result = converter.Convert(eixPath, outputPath, options);
            Console.WriteLine($"Converted {result.ConvertedFiles}/{result.TotalEntries} files.");
            if (result.SkippedFiles.Count > 0)
                Console.WriteLine($"Skipped {result.SkippedFiles.Count} encrypted entries.");
            if (result.Errors.Count > 0)
            {
                Console.Error.WriteLine("Errors:");
                foreach (var err in result.Errors)
                    Console.Error.WriteLine($"  - {err}");
            }
            return result.Errors.Count > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Conversion failed: {ex.Message}");
            return 1;
        }
    }

    static int ShowHelp()
    {
        PrintUsage();
        return 0;
    }

    static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        PrintUsage();
        return 1;
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"42pak-generator — VPK archive tool for Metin2

Usage: FortyTwoPak <command> [arguments] [options]

Commands:
  build     <sourceDir> <output.vpk>   Create a VPK archive from a directory
  extract   <archive.vpk> <outputDir>  Extract all files from a VPK archive
  info      <archive.vpk>              Display archive details and file listing
  validate  <archive.vpk>              Verify archive integrity
  convert   <input.eix> <output.vpk>   Convert EIX/EPK to VPK format
  help                                 Show this help message

Options (where applicable):
  --passphrase <value>     Encryption passphrase
  --compression <0-12>     Compression level (default: 3)
  --algorithm <name>       LZ4, Zstandard, or Brotli (default: LZ4)
  --mangle                 Obfuscate file names with SHA256
  --author <name>          Author metadata
  --comment <text>         Comment metadata

Examples:
  FortyTwoPak build ./gamedata output.vpk --passphrase mysecret --algorithm Zstandard
  FortyTwoPak extract archive.vpk ./output --passphrase mysecret
  FortyTwoPak info archive.vpk
  FortyTwoPak validate archive.vpk --passphrase mysecret
  FortyTwoPak convert textures.eix textures.vpk --passphrase newsecret

Run without arguments to launch the GUI.");
    }

    static Dictionary<string, string> ParseFlags(IEnumerable<string> args)
    {
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].StartsWith("--"))
            {
                string key = list[i][2..];
                if (key == "mangle")
                {
                    flags[key] = "true";
                }
                else if (i + 1 < list.Count && !list[i + 1].StartsWith("--"))
                {
                    flags[key] = list[++i];
                }
            }
        }
        return flags;
    }

    static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB"];
        int i = Math.Min((int)Math.Floor(Math.Log(bytes, 1024)), units.Length - 1);
        return $"{bytes / Math.Pow(1024, i):F1} {units[i]}";
    }
}
