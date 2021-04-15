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
            FindFiles();
        }

        private void FindFiles()
        {
            Console.WriteLine("FindFiles() called.");

            string syncPath = Directory.GetCurrentDirectory() + "\\Files to Sync";
            //Console.WriteLine(test);

            if (Directory.Exists(syncPath))
            {
                Console.WriteLine("Files to Sync directory did exist.");
                Console.WriteLine("Creating list of directories.");
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
                }
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






        // This method accepts two strings that represent two files to compare.
        // A return value of 0 indicates that the contents of the files are the same.
        // A return value of any other value indicates that the files are not the same.
        private bool FileCompare(string file1, string file2)
        {
            int file1byte;
            int file2byte;
            FileStream fs1;
            FileStream fs2;

            // Determine if the same file was referenced two times.
            if (file1 == file2)
            {
                // Return true to indicate that the files are the same.
                return true;
            }

            // Open the two files.
            fs1 = new FileStream(file1, FileMode.Open);
            fs2 = new FileStream(file2, FileMode.Open);
            
            // Check the file sizes. If they are not the same, the files
            // are not the same.
            if (fs1.Length != fs2.Length)
            {
                // Close the file
                fs1.Close();
                fs2.Close();

                // Return false to indicate files are different
                return false;
            }

            // Read and compare a byte from each file until either a
            // non-matching set of bytes is found or until the end of
            // file1 is reached.
            do
            {
                // Read one byte from each file.
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));

            // Close the files.
            fs1.Close();
            fs2.Close();

            // Return the success of the comparison. "file1byte" is
            // equal to "file2byte" at this point only if the files are
            // the same.
            return ((file1byte - file2byte) == 0);
        }
    }
}