/*
 * Daniel Willett
 * 08/08/2024
 */

using DanielWillett.SpeedBytes;
using DanielWillett.SpeedBytes.Unity;
using UnityEngine;

namespace UnturnedPreviewObjectDatFix;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.Write("Enter full file path to file that needs fixing: ");
        string? filePath = Console.ReadLine();

        if (filePath is { Length: > 2 } && filePath[0] == '"' && filePath[^1] == '"')
            filePath = filePath.Substring(1, filePath.Length - 2);

        while (filePath == null || !File.Exists(filePath))
        {
            Console.WriteLine("File doesn't exist. Hit Ctrl + C to exit.");
            Console.Write("Enter full file path to file that needs fixing: ");
            filePath = Console.ReadLine();

            if (filePath is { Length: > 2 } && filePath[0] == '"' && filePath[^1] == '"')
                filePath = filePath.Substring(1, filePath.Length - 2);
        }

        filePath = Path.GetFullPath(filePath);

        ByteReader reader = new ByteReader { ThrowOnError = true };

        string fixedPath = Path.Combine(Path.GetDirectoryName(filePath)!, "Fixed " + Path.GetFileName(filePath));
        Console.WriteLine($"The fixed file will be created at \"{fixedPath}\".");
        Console.Write("Continue? Type 'Y' and press [ENTER] :");

        if (!string.Equals(Console.ReadLine(), "y", StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }

        using FileStream output = new FileStream(fixedPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        ByteWriter writer = new ByteWriter
        {
            Stream = output
        };

        reader.LoadNew(File.ReadAllBytes(filePath));

        byte version = reader.ReadUInt8();
        Console.WriteLine($"Version: {version}");
        uint availableInstanceId = reader.ReadUInt32();
        Console.WriteLine($"Available instance id: {availableInstanceId}");

        if (version != 12)
        {
            Console.WriteLine("Only Object.dat files created in preview version need fixing, this file should be fine. Press [ENTER] to exit.");
            Console.ReadLine();
            return;
        }

        writer.Write((byte)11);
        writer.Write(availableInstanceId);

        int totalObjectCount = 0;
        int fixedObjectCount = 0;

        for (int x = 0; x < 64; ++x)
        {
            for (int y = 0; y < 64; ++y)
            {
                int ct = reader.ReadUInt16();

                writer.Write((ushort)ct);

                Console.WriteLine($"Region ({x}, {y}) - {ct} object(s).");
                for (int i = 0; i < ct; ++i)
                {
                    Vector3 pos = reader.ReadVector3();
                    Vector3 rotation = reader.ReadVector3();
                    Vector3 scale = reader.ReadVector3();

                    ushort id = reader.ReadUInt16();
                    Guid guid = new Guid(reader.ReadUInt8Array());
                    byte placementOrigin = reader.ReadUInt8();
                    uint instanceId = reader.ReadUInt32();
                    Guid palette = new Guid(reader.ReadUInt8Array());
                    int paletteIndex = reader.ReadInt32();

                    bool isMissingAsset = id == 0 && guid == Guid.Empty && instanceId == 0;

                    // the actual fix
                    bool isOwnedCullingVolumeAllowed = isMissingAsset || reader.ReadBool();
                    Console.WriteLine($"Object # {instanceId}: {pos}, {rotation}, {scale} - {id}, {guid:N}, {placementOrigin}, {palette:N}, {paletteIndex}, {isOwnedCullingVolumeAllowed}.");

                    writer.Write(pos);
                    writer.Write(rotation);
                    writer.Write(scale);
                    writer.Write(id);
                    writer.Write(guid.ToByteArray());
                    writer.Write(placementOrigin);
                    writer.Write(instanceId);
                    writer.Write(palette.ToByteArray());
                    writer.Write(paletteIndex);
                    //writer.Write(isOwnedCullingVolumeAllowed);

                    ++totalObjectCount;
                    if (isMissingAsset)
                    {
                        ++fixedObjectCount;
                    }
                }
            }
        }

        writer.Flush();
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"Saved to \"{fixedPath}\", load this ! IN THE LIVE BUILD ! unless Nelson has fixed the preview.");
        Console.WriteLine($"Done checking {totalObjectCount} object(s). Fixed {fixedObjectCount} object(s). Press [ENTER] to exit.");

        Console.ReadLine();
    }
}
