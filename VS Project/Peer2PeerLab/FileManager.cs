using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Peer2PeerLab
{
    // Manages the local files to sync.
    class FileManager
    {
        // Flag to check if files are currently being synced.
        public bool isSyncing;
        // The path to the .exe file.
        public string basePath;
        // The patht to the sync folder.
        public string syncPath;
        // Table to store files and their hashes.
        public Dictionary<string, byte[]> localFiles;
        // Watches for file changes.
        private FileSystemWatcher watcher;

        // Constructor.
        public FileManager()
        {
            // Initialize variables.
            isSyncing = false;
            basePath = Directory.GetCurrentDirectory();
            syncPath = basePath + "\\Files to Sync";
            localFiles = new Dictionary<string, byte[]>();

            // If the sync folder exists then add all files in it to the table, otherwise create the folder.
            if (Directory.Exists(syncPath))
            {
                Console.WriteLine("Preparing files for sync...");
                // For each file generate a hash and save the file and hash to the table.
                foreach (string s in EnumerateFilesRecursively(syncPath))
                {
                    // Open the file and generate its hash, then close the file.
                    FileInfo file = new FileInfo(s);
                    FileStream fileStream = new FileStream(file.FullName, FileMode.Open);
                    byte[] hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
                    fileStream.Close();

                    // Add the file and its hash to the table.
                    localFiles.Add(file.FullName.Replace(basePath, ""), hash);
                }
                Console.WriteLine("Files ready to sync.\n");
            }
            else
            {
                Console.WriteLine("Files to Sync directory did not exist.");
                Console.WriteLine("Creating Files to Sync directory...\n");
                Directory.CreateDirectory(syncPath);
            }

            // Create a file watcher to update the file table when a file is created, changed, or renamed.
            watcher = new FileSystemWatcher(syncPath);
            watcher.NotifyFilter = NotifyFilters.LastWrite
                | NotifyFilters.FileName;
            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Deleted += OnDeleted;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        // Called when a file is changed. (Based on last write)
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }

            // Ignore changes while files being synced.
            if (isSyncing)
                return;

            //Console.WriteLine($"Potential Change Detected: {e.FullPath}");

            // Open the file and generate its hash, then close the file.
            FileInfo file = new FileInfo(e.FullPath);
            FileStream fileStream = new FileStream(file.FullName, FileMode.Open);
            byte[] hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
            fileStream.Close();

            // If hashes are not equal then update the hash.
            if (!HashCompare(localFiles[file.FullName.Replace(basePath, "")], hash))
            {
                Console.WriteLine($"File Updated: {e.FullPath}\n");
                localFiles[file.FullName.Replace(basePath, "")] = hash;
            }
        }
        // Called when a file is renamed.
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            // Ignore changes while files being synced.
            if (isSyncing)
                return;

            Console.WriteLine($"Renamed:");
            Console.WriteLine($"    Old: {e.OldFullPath}");
            Console.WriteLine($"    New: {e.FullPath}\n");

            // Add new file path with old hash.
            localFiles.Add(e.FullPath.Replace(basePath, ""), localFiles[e.OldFullPath.Replace(basePath, "")]);

            // Remove old file path.
            localFiles.Remove(e.OldFullPath.Replace(basePath, ""));
        }
        // Called when a file is created.
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            // Ignore changes while files being synced.
            if (isSyncing)
                return;

            Console.WriteLine($"Created:");
            Console.WriteLine($"    New: {e.FullPath}\n");

            // Open the file and generate its hash, then close the file.
            FileInfo file = new FileInfo(e.FullPath);
            FileStream fileStream = new FileStream(file.FullName, FileMode.Open);
            byte[] hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
            fileStream.Close();

            // Add the file and its hash to the table.
            localFiles.Add(file.FullName.Replace(basePath, ""), hash);
        }
        // Called when a file is deleted.
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            // Ignore changes while files being synced.
            if (isSyncing)
                return;

            Console.WriteLine($"Deleted: {e.FullPath}");

            // Remove file path.
            localFiles.Remove(e.FullPath.Replace(basePath, ""));
        }

        // Enumerate all files in a given folder recursively. (Including entire sub-folder hierarchy)
        public IEnumerable<string> EnumerateFilesRecursively(string path)
        {
            // Check if there are any more subdirectories.
            if (Directory.EnumerateDirectories(path).Count() > 0)
            {
                // If there are, recursively call.
                foreach (string d in Directory.EnumerateDirectories(path))
                    foreach (string s in EnumerateFilesRecursively(d))
                        yield return s;
            }

            // For each file in this directory.
            foreach (string s in Directory.EnumerateFiles(path))
                yield return s;
        }

        // Check if the file exists locally.
        public bool HasFile(string path)
        {
            if (localFiles.ContainsKey(path))
                return true;
            else
                return false;
        }

        // Compare the local file hash to the given hash data.
        public bool FileCompare(string path, byte[] hash)
        {
            return HashCompare(localFiles[path], hash);
        }

        // Check if two hashes are equal.
        private bool HashCompare(byte[] hash1, byte[] hash2)
        {
            // Assume not equal.
            bool isEqual = false;

            // If not the same length, then not the same hash.
            if (hash1.Length == hash2.Length)
            {
                int i = 0;
                while ((i < hash1.Length) && (hash1[i] == hash2[i]))
                {
                    // Only increment if the bytes are equal.
                    i += 1;
                }
                if (i == hash1.Length)
                {
                    // If all bytes are equal then the hashes are equal.
                    isEqual = true;
                }
            }

            return isEqual;
        }

        // Get the time when the file was last written to.
        public DateTime GetLastWrite(string path)
        {
            return new FileInfo(path).LastWriteTimeUtc;
        }

        // Get the size of the file.
        public long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        // Create or write over a file at the given path using the given data.
        public void CreateFile(string path, IEnumerable<byte[]> datas)
        {
            // First create/overwrite the file.
            
            // Create/Open the file.
            FileStream fileStream = File.Create(path);
            // Write the next supplied block of data to the file. (Supplied by the server/client - EnumerateFileBlocks())
            foreach (byte[] data in datas)
                fileStream.Write(data, 0, data.Length);
            // Close the file.
            fileStream.Close();

            // Then save the file and its hash to the table.

            // Open the file and generate its hash, then close the file.
            FileInfo file = new FileInfo(path);
            fileStream = new FileStream(file.FullName, FileMode.Open);
            byte[] hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
            fileStream.Close();

            // If the file already exists in the table, replace the hash. Otherwise, add the file and hash.
            if (localFiles.ContainsKey(file.FullName.Replace(basePath, "")))
                localFiles[file.FullName.Replace(basePath, "")] = hash;
            else
                localFiles.Add(file.FullName.Replace(basePath, ""), hash);
        }
    }
}