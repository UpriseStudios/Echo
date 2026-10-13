using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Echo.Editor
{
    /// <summary>
    /// Synchronously unpacks a .unitypackage (gzipped tar of guid/asset, guid/asset.meta, guid/pathname)
    /// into the project. AssetDatabase.ImportPackage is asynchronous, so batch-mode builds can't use it.
    /// </summary>
    public static class UnityPackageExtractor
    {
        const int Block = 512;

        public static int Extract(string packagePath, string projectRoot)
        {
            var assets = new Dictionary<string, byte[]>();
            var metas = new Dictionary<string, byte[]>();
            var paths = new Dictionary<string, string>();

            using (var file = File.OpenRead(packagePath))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                var header = new byte[Block];
                while (ReadExactly(gzip, header, Block))
                {
                    string name = ReadString(header, 0, 100);
                    if (name.Length == 0) break;
                    string prefix = ReadString(header, 345, 155);
                    if (prefix.Length > 0) name = prefix + "/" + name;
                    long size = Convert.ToInt64(ReadString(header, 124, 12).Trim().PadLeft(1, '0'), 8);
                    char type = (char)header[156];

                    var data = new byte[size];
                    if (size > 0 && !ReadExactly(gzip, data, (int)size)) throw new EndOfStreamException(packagePath);
                    int padding = (int)((Block - size % Block) % Block);
                    if (padding > 0) ReadExactly(gzip, new byte[padding], padding);
                    if (type != '0' && type != '\0') continue;

                    string[] parts = name.TrimStart('.', '/').Split('/');
                    if (parts.Length != 2) continue;
                    switch (parts[1])
                    {
                        case "asset": assets[parts[0]] = data; break;
                        case "asset.meta": metas[parts[0]] = data; break;
                        case "pathname": paths[parts[0]] = Encoding.UTF8.GetString(data).Split('\n')[0].Trim(); break;
                    }
                }
            }

            int written = 0;
            foreach (var kv in paths)
            {
                string target = Path.Combine(projectRoot, kv.Value);
                if (assets.TryGetValue(kv.Key, out byte[] asset))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, asset);
                    written++;
                }
                else
                {
                    Directory.CreateDirectory(target);
                }
                if (metas.TryGetValue(kv.Key, out byte[] meta)) File.WriteAllBytes(target + ".meta", meta);
            }
            return written;
        }

        static string ReadString(byte[] buffer, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && buffer[end] != 0) end++;
            return Encoding.UTF8.GetString(buffer, offset, end - offset);
        }

        static bool ReadExactly(Stream s, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buffer, read, count - read);
                if (n == 0) return false;
                read += n;
            }
            return true;
        }
    }
}
