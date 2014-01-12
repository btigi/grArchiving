using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace grArchiving
{
    class Program
    {
        private class FileInfoEntry
        {
            public UInt32 Hash;
            public Int32 Offset;
            public Int32 CompressedSize;
            public Int32 UncompressedSize;
            public Int32 Encrypted;
            public string Filename;

            public FileInfoEntry(UInt32 hash, Int32 offset, Int32 compressedSize, Int32 uncompressedSize, Int32 encrypted, string filename)
            {
                this.CompressedSize = compressedSize;
                this.Hash = hash;
                this.Offset = offset;
                this.UncompressedSize = uncompressedSize;
                this.Encrypted = encrypted;
                this.Filename = filename;
            }
        }

        private class Arguments
        {
            public string InputFile;
            public string OutputFile;
            public string Filelist;
            public bool IsValid;
            public bool ArgumentCountError;
            public bool InputFileError;
            public bool OutputFileError;
            public bool FilelistError;
        }

        static void Main(string[] args)
        {
            var arguments = ParseArguments(args);
            if (arguments.IsValid)
            {
                var fileInfoEntries = new List<FileInfoEntry>();
                using (FileStream savFile = new FileStream(arguments.InputFile, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader datFileReader = new BinaryReader(savFile))
                    {
                        datFileReader.BaseStream.Seek(0, SeekOrigin.Begin);
                        var s = new MemoryStream();
                        var header = datFileReader.ReadChars(4); // GRA2
                        var fileCount = datFileReader.ReadInt32();

                        var filelist = File.ReadAllLines(arguments.Filelist);
                        for (int i = 0; i < fileCount; i++)
                        {
                            var hash = datFileReader.ReadUInt32();
                            var offset = datFileReader.ReadInt32();
                            var compressedSize = datFileReader.ReadInt32();
                            var uncompressedSize = datFileReader.ReadInt32();
                            var encrypted = datFileReader.ReadInt32();
                            var filename = GetFileName(filelist, hash);
                            fileInfoEntries.Add(new FileInfoEntry(hash, offset, compressedSize, uncompressedSize, encrypted, filename));
                        }

                        var fileIndex = 0;
                        foreach (var fileEntry in fileInfoEntries)
                        {
                            var validFile = true;
                            byte[] bytes;
                            datFileReader.BaseStream.Seek(fileEntry.Offset, SeekOrigin.Begin);
                            if (fileEntry.CompressedSize == 0)
                            {
                                bytes = datFileReader.ReadBytes(fileEntry.UncompressedSize);
                            }
                            else
                            {
                                var fileSize = datFileReader.ReadInt32();
                                bytes = datFileReader.ReadBytes(fileEntry.CompressedSize - 4); // The compressed size includes 4 bytes which were holding the fileSize
                                if (fileEntry.Encrypted == 1)
                                {
                                    bytes = Ionic.Zlib.ZlibStream.UncompressBuffer(bytes);
                                }
                                else
                                {
                                    validFile = false;
                                    Console.WriteLine(String.Format("Not saving file number {0} from offset 0x{1} (encryption code {2})", fileIndex, fileEntry.Offset.ToString("X"), fileEntry.Encrypted));
                                }
                            }

                            if (validFile)
                            {
                                var filename = String.Format(@"{0}{1}", arguments.OutputFile, fileEntry.Filename);

                                Directory.CreateDirectory(Path.GetDirectoryName(filename));
                                using (FileStream datFileWriter = new FileStream(filename, FileMode.Create))
                                {
                                    datFileWriter.Write(bytes, 0, bytes.Length);
                                    datFileWriter.Flush();
                                    datFileWriter.Close();
                                }
                            }
                            fileIndex++;
                        }
                    }
                }
            }
        }

        private static Arguments ParseArguments(string[] args)
        {
            var arguments = new Arguments();
            if (args.Length == 2 || args.Length == 3)
            {
                // We require the input file does exist
                if (File.Exists(args[0]))
                {
                    arguments.InputFile = args[0];
                }
                else
                {
                    arguments.InputFileError = true;
                }

                // We require the output file does not already exist
                if (!File.Exists(args[1]))
                {
                    arguments.OutputFile = args[1];
                }
                else
                {
                    arguments.OutputFileError = true;
                }

                arguments.Filelist = "filelist.txt";
                if (args.Length == 3)
                {
                    if (File.Exists(args[2]))
                    {
                        arguments.Filelist = args[2];
                    }
                    else
                    {
                        arguments.FilelistError = true;
                    }
                }
            }
            else
            {
                arguments.ArgumentCountError = true;
            }

            arguments.IsValid = !arguments.ArgumentCountError & !arguments.InputFileError & !arguments.OutputFileError & !arguments.FilelistError;

            if (!arguments.IsValid)
            {
                Console.WriteLine("Invalid usage, expected usage patterns:");
                Console.WriteLine("  grArchiving datFile outputDirectory");
                Console.WriteLine("  grArchiving datFile outputDirectory filelist");
            }

            return arguments;
        }

        private static string GetFileName(string[] filelist, UInt32 hash)
        {
            var filename = filelist.Where(fl => fl.StartsWith(hash.ToString("X") + "|")).FirstOrDefault();
            return (filename ?? '|' + hash.ToString() + ".dat").Split('|')[1];
        }

        // https://en.wikipedia.org/wiki/Fowler_Noll_Vo_hash
        // http://www.grimrock.net/forum/viewforum.php?f=14
        //e.g. var h = FNVHash(@"shaders/d3d9/blur_cube_map_blur_ps.d3d9_shader");
        //private static UInt32 FNVHash(string s)
        //{
        //    var v = 0x811c9dc5;

        //    foreach (var c in s)
        //    {
        //        v ^= (byte)c;
        //        v = (v * 0x1000193) & 0xffffffff;
        //    }

        //    return v;
        //}
    }
}