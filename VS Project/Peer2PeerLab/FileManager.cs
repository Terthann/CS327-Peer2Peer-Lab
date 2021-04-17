using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Peer2PeerLab
{
    class FileManager
    {
        public bool isSyncing;
        public string basePath;
        public string syncPath;
        public Dictionary<string, byte[]> localFiles;

        // Constructor.
        public FileManager()
        {
            isSyncing = false;
            basePath = Directory.GetCurrentDirectory();
            syncPath = basePath + "\\Files to Sync";
            localFiles = new Dictionary<string, byte[]>();

            if (Directory.Exists(syncPath))
            {
                foreach (string s in EnumerateFilesRecursively(syncPath))
                {
                    //Console.WriteLine(new FileInfo(s).Name);
                    //Console.WriteLine(new FileInfo(s).DirectoryName.Replace(basePath, ""));
                    //Console.WriteLine(new FileInfo(s).FullName.Replace(basePath, ""));

                    FileInfo file = new FileInfo(s);

                    FileStream fileStream = new FileStream(file.FullName, FileMode.Open);
                    byte[] hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
                    fileStream.Close();

                    localFiles.Add(file.FullName.Replace(basePath, ""), hash);
                }
            }
            else
            {
                Console.WriteLine("Files to Sync directory did not exist.");
                Console.WriteLine("Creating Files to Sync directory...");
                Directory.CreateDirectory(syncPath);
            }
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

        public bool HasFile(string path)
        {
            if (localFiles.ContainsKey(path))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool FileCompare(string path, byte[] hash)
        {
            return HashCompare(localFiles[path], hash);
        }

        private bool HashCompare(byte[] hash1, byte[] hash2)
        {
            bool isEqual = false;

            if (hash1.Length == hash2.Length)
            {
                int i = 0;
                while ((i < hash1.Length) && (hash1[i] == hash2[i]))
                {
                    i += 1;
                }
                if (i == hash1.Length)
                {
                    isEqual = true;
                }
            }

            return isEqual;
        }

        public DateTime GetTimeCreated(string path)
        {
            return new FileInfo(path).LastWriteTimeUtc;
        }

        public long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        public void CreateFile(string path, IEnumerable<byte[]> datas)
        {
            FileStream fileStream = File.Create(path);
            foreach (byte[] data in datas)
                fileStream.Write(data, 0, data.Length);
            fileStream.Close();

            FileInfo file = new FileInfo(path);

            fileStream = new FileStream(file.FullName, FileMode.Open);
            byte[] hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
            fileStream.Close();

            if (localFiles.ContainsKey(file.FullName.Replace(basePath, "")))
                localFiles[file.FullName.Replace(basePath, "")] = hash;
            else
                localFiles.Add(file.FullName.Replace(basePath, ""), hash);
        }
    }
}