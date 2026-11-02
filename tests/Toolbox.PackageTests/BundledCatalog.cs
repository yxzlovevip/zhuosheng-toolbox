using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.NET.HostModel.Bundle;

internal static class BundledCatalog
{
    // Read metadata only: never load or execute the installed application's code.
    public static byte[] Read(string executable)
    {
        if (!Bundler.IsBundle(executable, out long header)) throw new Exception("app is not a .NET bundle");
        using var input = File.OpenRead(executable);
        input.Position = header;
        using var reader = new BinaryReader(input);
        uint major = reader.ReadUInt32(), minor = reader.ReadUInt32();
        if (major != 6 || minor != 0) throw new Exception("unsupported bundle manifest version");
        int count = reader.ReadInt32();
        if (count < 1 || count > 10000) throw new Exception("invalid bundle file count");
        _ = reader.ReadString(); // Bundle ID.
        input.Position += 40; // deps/runtimeconfig offset and size, then flags.
        for (int i = 0; i < count; i++)
        {
            long offset = reader.ReadInt64(), size = reader.ReadInt64(), compressed = reader.ReadInt64();
            byte type = reader.ReadByte();
            string path = reader.ReadString();
            if (path != "CZToolbox.dll") continue;
            long stored = compressed == 0 ? size : compressed;
            if (type != (byte)FileType.Assembly || offset < 0 || size < 1 || size > 16777216 || stored < 1 || stored > 16777216 || offset > input.Length - stored)
                throw new Exception("invalid bundled app assembly");
            input.Position = offset;
            byte[] bytes = reader.ReadBytes(checked((int)stored));
            if (bytes.Length != stored) throw new Exception("truncated bundled assembly");
            if (compressed != 0)
            {
                using var packed = new MemoryStream(bytes);
                using var inflate = new DeflateStream(packed, CompressionMode.Decompress);
                byte[] expanded = new byte[checked((int)size)];
                inflate.ReadExactly(expanded);
                if (inflate.ReadByte() != -1) throw new Exception("unexpected assembly expansion");
                bytes = expanded;
            }
            using var assembly = new MemoryStream(bytes);
            using var pe = new PEReader(assembly);
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.ManifestResources)
            {
                var resource = metadata.GetManifestResource(handle);
                if (!resource.Implementation.IsNil || metadata.GetString(resource.Name) != "catalog.json") continue;
                var block = pe.GetSectionData(pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);
                int resourceOffset = checked((int)resource.Offset);
                var content = block.GetReader(resourceOffset, block.Length - resourceOffset);
                int length = content.ReadInt32();
                if (length < 1 || length > 4194304) throw new Exception("invalid embedded catalog length");
                return content.ReadBytes(length);
            }
            throw new Exception("bundled app has no catalog.json resource");
        }
        throw new Exception("bundle has no CZToolbox.dll");
    }
}
