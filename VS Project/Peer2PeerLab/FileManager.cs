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
        // Constructor
        public FileManager()
        {
            //FindFiles();
        }

        private void FindFiles()
        {
            Console.WriteLine("FindFiles() called.");

            string syncPath = Directory.GetCurrentDirectory() + "\\Files to Sync";

            if (Directory.Exists(syncPath))
            {
                Console.WriteLine("Files to Sync directory did exist.");

                foreach (string s in EnumerateFilesRecursively(syncPath))
                {
                    Console.WriteLine(s);
                }

                /*Console.WriteLine("Creating list of directories.");
                DirectoryInfo[] directories = new DirectoryInfo(syncPath).GetDirectories();
                Console.WriteLine("Size of array: " + directories.Length);

                List<DirectoryInfo> directoryList = new List<DirectoryInfo>();
                directoryList.Add(new DirectoryInfo(syncPath));
                foreach (DirectoryInfo d in directories)
                    directoryList.Add(d);

                Dictionary<Byte[], string> fileHashes = new Dictionary<byte[], string>();
                foreach (DirectoryInfo dir in directoryList)
                {
                    FileInfo[] files = dir.GetFiles();
                    foreach (FileInfo file in files)
                    {
                        FileStream fileStream = new FileStream(file.FullName, FileMode.Open);
                        byte[] hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
                        fileStream.Close();

                        fileHashes.Add(hash, file.Name);
                    }
                }*/
            }
            else
            {
                Console.WriteLine("Files to Sync directory did not exist.");
                Console.WriteLine("Creating Files to Sync directory...");
                Directory.CreateDirectory(syncPath);
            }

            Console.WriteLine("FindFiles() returned.");

            Console.WriteLine("File 1:");
            FileStream testFileStream = new FileStream(syncPath + "\\TestDocument.txt", FileMode.Open);

            byte[] testByteArray = new MD5CryptoServiceProvider().ComputeHash(testFileStream);
            foreach (byte b in testByteArray)
                Console.WriteLine(b);

            testFileStream.Close();

            Console.WriteLine("File 2:");
            testFileStream = new FileStream(syncPath + "\\TestDocument2.txt", FileMode.Open);
                
            byte[] hashTest = new MD5CryptoServiceProvider().ComputeHash(testFileStream);
            foreach (byte b in hashTest)
                Console.WriteLine(b);

            testFileStream.Close();

            Console.WriteLine(HashCompare(testByteArray, hashTest));
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
    }
}