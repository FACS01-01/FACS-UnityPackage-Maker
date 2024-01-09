using System.IO.Compression;
using System.Formats.Tar;
using System.IO.Hashing;

namespace FACS01.FUPM
{
    public class Program
    {
        // Error Codes:  https://learn.microsoft.com/windows/win32/debug/system-error-codes--0-499-
        private const int Success       = 0x00;
        private const int FileNotFound  = 0x02;
        private const int PathNotFound  = 0x03;
        private const int NoFiles       = 0x12;
        private const int CannotMake    = 0x52;
        private const int BadArgs       = 0xA0;
        private const int BadPath       = 0xA1;
        private const int AlreadyExists = 0xB7;

        private static bool noConsole = false;

        static int Main(string[] args) //args:[ pack/unpack ; source ; destination ; noConsole ]
        {
            if (args.Length == 0) { PrintHeader(); PrintArgs(); PAKTE(); return Success; }
            if (args.Length == 4 && args[3] == "noConsole") { noConsole = true; args = [args[0], args[1], args[2]]; }
            if (args.Length != 3) { PrintHeader(); PrintArgs(); PAKTE(); return BadArgs; }

            if (!noConsole) PrintHeader();

            switch (args[0].ToUpper())
            {
                case "P":
                    return Pack(args[1], args[2], null);
                case "PA":
                    return Pack(args[1], args[2], "Assets/");
                case "PP":
                    return Pack(args[1], args[2], "Packages/");
                case "U":
                    return Unpack(args[1], args[2], false);
                case "UK":
                    return Unpack(args[1], args[2], true);
                default:
                    if (!noConsole) { PrintHeader(); PrintArgs(); PAKTE(); }
                    return BadArgs;
            }
        }

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n#########################");
            Console.WriteLine("#FACS UnityPackage Maker#");
            Console.WriteLine("#########################\n\n");
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void PrintArgs()
        {
            Console.WriteLine("Usage: FUPM.exe [work] [source] [destination] {optional \"noConsole\"}\n");
            Console.WriteLine("  [work]:   \"P\"       Create UnityPackage. Source should be a Unity project's \"Assets\" or \"Packages\" folder");
            Console.WriteLine("            \"PA\"      Create UnityPackage with root \"Assets\". To be unpacked to \"Assets/Source\"");
            Console.WriteLine("            \"PP\"      Create UnityPackage with root \"Packages\". To be unpacked to \"Packages/Source\"");
            Console.WriteLine("            \"U\"       Unpack UnityPackage removing root (\"Assets\"|\"Packages\"|...)");
            Console.WriteLine("            \"UK\"      Unpack UnityPackage keeping root");
            Console.WriteLine("  [source]:           Path to the folder to Pack,   or to the UnityPackage file to Unpack");
            Console.WriteLine("  [destination]:      Path to the new UnityPackage, or to the new|empty folder to Unpack\n\n");
        }

        private static void PAKTE()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Press any key to exit...");
            Console.ForegroundColor = ConsoleColor.White;
            Console.ReadKey();
            Console.Write("\n");
        }

        private static int Pack(string source, string destination, string? root)
        {
            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);
            if (!Directory.Exists(source))
            {
                if (!noConsole) { PrintError_PathNotFound(source, false); PAKTE(); }
                return PathNotFound;
            }
            if (File.Exists(destination))
            {
                if (!noConsole) { PrintError_PathAlreadyExists(destination, true); PAKTE(); }
                return AlreadyExists;
            }
            if (IsDirectoryEmpty(source))
            {
                if (!noConsole)
                {
                    PrintError($"No files found to create UnityPackage, at: \"{source}\"\n\n");
                    PAKTE();
                }
                return FileNotFound;
            }

            var sourceCont = Path.GetDirectoryName(source);
            if (string.IsNullOrEmpty(sourceCont))
            {
                if (!noConsole)
                {
                    PrintError($"Can't get parent folder of source: \"{source}\"\n\n");
                    PAKTE();
                }
                return BadPath;
            }

            CollectFromDir(source, root, sourceCont);
            AssetForTar.ProcessLastAssets(root, sourceCont);
            if (AssetForTar.assetForTars.Count == 0)
            {
                if (!noConsole)
                {
                    PrintError($"No files found to create UnityPackage, at: \"{source}\"\n\n");
                    PAKTE();
                }
                return FileNotFound;
            }

            var tempFolder = GetTemporaryDirectory();
            var tempTar = Path.GetFullPath(Path.Combine(tempFolder, "archtemp.tar"));
            var FileMode = (UnixFileMode)511;

            try
            {
                using (var fw = File.OpenWrite(tempTar))
                using (var writer = new TarWriter(fw, TarEntryFormat.Ustar, false))
                {
                    foreach (var file in AssetForTar.assetForTars)
                    {
                        file.guid += '/';
                        var dirEntry = new UstarTarEntry(TarEntryType.Directory, file.guid) { Mode = FileMode };
                        writer.WriteEntry(dirEntry);
                        if (!string.IsNullOrEmpty(file.assetPath))
                        {
                            using var assetStream = File.OpenRead(file.assetPath);
                            var assetEntry = new UstarTarEntry(TarEntryType.RegularFile, file.guid + "asset")
                            { DataStream = assetStream, Mode = FileMode };
                            writer.WriteEntry(assetEntry);
                        }
                        if (!string.IsNullOrEmpty(file.metaPath))
                        {
                            using var metaStream = File.OpenRead(file.metaPath);
                            var metaEntry = new UstarTarEntry(TarEntryType.RegularFile, file.guid + "asset.meta")
                            { DataStream = metaStream, Mode = FileMode };
                            writer.WriteEntry(metaEntry);
                        }
                        using var pathnameStream = new MemoryStream();
                        pathnameStream.Write(System.Text.Encoding.UTF8.GetBytes(file.relative));
                        pathnameStream.Position = 0;
                        var pathnameEntry = new UstarTarEntry(TarEntryType.RegularFile, file.guid + "pathname")
                        { DataStream = pathnameStream, Mode = FileMode };
                        writer.WriteEntry(pathnameEntry);
                    }
                }
            }
            catch (Exception e)
            {
                Directory.Delete(tempFolder, true);
                if (!noConsole)
                {
                    PrintError($"{e}\n\n");
                    PAKTE();
                    return CannotMake;
                }
                throw;
            }

            try
            {
                using (var TarStream = File.OpenRead(tempTar))
                using (var TGZStream = File.OpenWrite(destination))
                {
                    WriteTGZHeader(TGZStream, "archtemp.tar");
                    using (var gz = new DeflateStream(TGZStream, CompressionMode.Compress, true))
                    { TarStream.CopyTo(gz); }
                    TarStream.Position = 0;
                    var crc32 = new Crc32(); crc32.Append(TarStream);
                    WriteTGZFooter(TGZStream, crc32.GetCurrentHashAsUInt32(), TarStream.Length);
                }
            }
            catch (Exception e)
            {
                Directory.Delete(tempFolder, true);
                if (File.Exists(destination)) File.Delete(destination);
                if (!noConsole)
                {
                    PrintError($"{e}\n\n");
                    PAKTE();
                    return CannotMake;
                }
                throw;
            }
            
            Directory.Delete(tempFolder, true);
            if (!noConsole)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"New UnityPackage created at: \"{destination}\"\n\n");
                Console.ForegroundColor = ConsoleColor.White;
                PAKTE();
            }

            return Success;
        }

        private static int Unpack(string source, string destination, bool keepRoot)
        {
            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);
            if (!File.Exists(source))
            {
                if (!noConsole) { PrintError_PathNotFound(source, true); PAKTE(); }
                return FileNotFound;
            }
            var destinationAlreadyExists = Directory.Exists(destination);
            if (destinationAlreadyExists && !IsDirectoryEmpty(destination))
            {

                if (!noConsole) { PrintError_PathAlreadyExists(destination, false); PAKTE(); }
                return AlreadyExists;
            }

            var tempFolder = GetTemporaryDirectory();
            var tempTar = Path.GetFullPath(Path.Combine(tempFolder, "archtemp.tar"));
            try
            {
                using (var fw = File.OpenWrite(tempTar))
                using (var TGZFile = File.OpenRead(source))
                using (var TGZStream = new GZipStream(TGZFile, CompressionMode.Decompress))
                {
                    TGZStream.CopyTo(fw);
                }
            }
            catch (Exception e)
            {
                Directory.Delete(tempFolder, true);
                if (!noConsole)
                {
                    PrintError($"{e}\n\n");
                    PAKTE();
                    return CannotMake;
                }
                throw;
            }

            var tempFolder2 = GetTemporaryDirectory();
            var GUIDToPath = new Dictionary<string, string>();
            try
            {
                using (var TarFile = File.OpenRead(tempTar))
                using (var TarStream = new TarReader(TarFile))
                {
                    TarEntry? entry;
                    while ((entry = TarStream.GetNextEntry()) != null)
                    {
                        if (entry.EntryType != TarEntryType.RegularFile) continue;
                        var entryPath = entry.Name.Split('/');
                        var guid = entryPath[0]; var entryFile = entryPath[1];
                        if (entryFile == "pathname")
                        {
                            using (var sr = new StreamReader(entry.DataStream!))
                            {
                                var relative = sr.ReadToEnd();
                                if (!keepRoot) relative = relative[(relative.IndexOf('/')+1)..];
                                GUIDToPath.Add(guid, Path.GetFullPath(Path.Combine(destination, relative)));
                            }
                            continue;
                        }
                        if (entryFile != "asset" && entryFile != "asset.meta") continue;
                        var tempDir = Path.GetFullPath(Path.Combine(tempFolder2, guid));
                        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                        var tempFile = Path.GetFullPath(Path.Combine(tempDir, entryFile));
                        entry.ExtractToFile(tempFile, false);
                    }
                }

                foreach (var guidDir in Directory.GetDirectories(tempFolder2))
                {
                    var tempFiles = Directory.GetFiles(guidDir);
                    var guid = Path.GetFileName(guidDir);
                    if (!GUIDToPath.TryGetValue(guid, out var finalPath)) continue;
                    var parentPath = Path.GetDirectoryName(finalPath);
                    if (!Directory.Exists(parentPath)) Directory.CreateDirectory(parentPath!);

                    if (tempFiles[0].EndsWith("asset")) File.Move(tempFiles[0], finalPath);
                    else
                    {
                        if (!Directory.Exists(finalPath)) Directory.CreateDirectory(finalPath);
                        File.Move(tempFiles[0], finalPath + ".meta");
                    }
                    if (tempFiles.Length>1)
                    {
                        File.Move(tempFiles[1], finalPath + ".meta");
                    }
                }
            }
            catch (Exception e)
            {
                Directory.Delete(tempFolder, true);
                Directory.Delete(tempFolder2, true);
                if (!destinationAlreadyExists) Directory.Delete(destination, true);
                else
                {
                    foreach (var dir in Directory.GetDirectories(destination)) Directory.Delete(dir, true);
                    foreach (var file in Directory.GetFiles(destination)) File.Delete(file);
                }
                if (!noConsole)
                {
                    PrintError($"{e}\n\n");
                    PAKTE();
                    return CannotMake;
                }
                throw;
            }

            Directory.Delete(tempFolder, true);
            Directory.Delete(tempFolder2, true);
            if (!noConsole)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"UnityPackage unpacked at: \"{destination}\"\n\n");
                Console.ForegroundColor = ConsoleColor.White;
                PAKTE();
            }

            return Success;
        }

        private static void PrintError_PathNotFound(string path, bool shouldBeFile)
        {
            PrintError($"{(shouldBeFile ? "File" : "Directory")} not found: \"{path}\"\n\n");
        }

        private static void PrintError_PathAlreadyExists(string path, bool shouldBeFile)
        {
            PrintError($"{(shouldBeFile ? "File" : "Directory")} already exists: \"{path}\"\n\n");
        }

        private static void PrintError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            if (File.Exists(tempDirectory)) return GetTemporaryDirectory();
            else
            {
                Directory.CreateDirectory(tempDirectory);
                return tempDirectory;
            }
        }

        private static void WriteTGZHeader(Stream stream, string name)
        {
            byte[] header = [0x1f, 0x8b, 8, 8, 0, 0, 0, 0, 4, 0];
            stream.Write(header, 0, header.Length);

            byte[] data = System.Text.Encoding.UTF8.GetBytes(name);
            stream.Write(data, 0, data.Length);
            stream.WriteByte(0);
        }

        private static void WriteTGZFooter(Stream stream, uint crc, long size)
        {
            stream.WriteByte((byte)(crc & 0xff));
            stream.WriteByte((byte)((crc >> 8) & 0xff));
            stream.WriteByte((byte)((crc >> 16) & 0xff));
            stream.WriteByte((byte)((crc >> 24) & 0xff));
            
            stream.WriteByte((byte)(size & 0xff));
            stream.WriteByte((byte)((size >> 8) & 0xff));
            stream.WriteByte((byte)((size >> 16) & 0xff));
            stream.WriteByte((byte)((size >> 24) & 0xff));
        }

        private static void CollectFromDir(string dir, string? root, string sourceContainer)
        {
            if (Path.GetFileName(dir).StartsWith('.')) return;

            var metaDir = dir + ".meta";
            if (File.Exists(metaDir))
            {
                using var reader = new StreamReader(metaDir);
                reader.ReadLine();
                var guid = reader.ReadLine();
                if (!string.IsNullOrEmpty(guid) && guid.StartsWith("guid: "))
                {
                    var relPath = dir[(sourceContainer.Length+1)..].Replace('\\', '/');
                    if (root != null) relPath = root + relPath;
                    AssetForTar.AddDir(guid[6..], relPath, metaDir);
                }
            }

            foreach (var asset in Directory.GetFiles(dir))
            {
                if (asset.EndsWith(".meta") || Path.GetFileName(asset).StartsWith('.')) continue;
                var metaAsset = asset + ".meta";
                if (File.Exists(metaAsset))
                {
                    using var reader = new StreamReader(metaAsset);
                    reader.ReadLine();
                    var guid = reader.ReadLine();
                    if (!string.IsNullOrEmpty(guid) && guid.StartsWith("guid: "))
                    {
                        var relPath = asset[(sourceContainer.Length+1)..].Replace('\\', '/');
                        if (root != null) relPath = root + relPath;
                        AssetForTar.AddAsset(guid[6..], relPath, metaAsset, asset);
                    }
                }
                else
                {
                    AssetForTar.AddAssetLater(asset);
                }
            }

            foreach (var _dir in Directory.GetDirectories(dir))
            {
                CollectFromDir(_dir, root, sourceContainer);
            }
        }

        private static bool IsDirectoryEmpty(string path)
        {
            IEnumerable<string> items = Directory.EnumerateFileSystemEntries(path);
            using IEnumerator<string> en = items.GetEnumerator();
            return !en.MoveNext();
        }

        private class AssetForTar
        {
            public static HashSet<string> GUIDS = [];
            public static List<AssetForTar> assetForTars = [];
            public static List<string> AssignGUID = [];

            public string guid;
            public string relative;
            public string metaPath;
            public string assetPath = "";

            private AssetForTar(string GUID, string Relative, string MetaPath, string AssetPath = "")
            {
                if (!string.IsNullOrEmpty(AssetPath)) assetPath = AssetPath;
                guid = GUID; metaPath = MetaPath; relative = Relative;

                GUIDS.Add(GUID); assetForTars.Add(this);
            }

            public static void AddDir(string GUID, string Relative, string MetaPath)
            {
                _ = new AssetForTar(GUID, Relative, MetaPath);
            }

            public static void AddAsset(string GUID, string Relative, string MetaPath, string AssetPath)
            {
                _ = new AssetForTar(GUID, Relative, MetaPath, AssetPath);
            }

            public static void AddAssetLater(string AssetPath)
            {
                AssignGUID.Add(AssetPath);
            }

            public static void ProcessLastAssets(string? root, string sourceContainer)
            {
                foreach (var asset in AssignGUID)
                {
                    var guid = Guid.NewGuid().ToString().Replace("-","");
                    while (!GUIDS.Add(guid)) { guid = Guid.NewGuid().ToString().Replace("-", ""); }

                    var relPath = asset[(sourceContainer.Length+1)..].Replace('\\', '/');
                    if (root != null) relPath = root + relPath;
                    AddAsset(guid, relPath, "", asset);
                }
                assetForTars.Sort((x,y)=>string.Compare(x.guid,y.guid));
            }
        }
    }
}