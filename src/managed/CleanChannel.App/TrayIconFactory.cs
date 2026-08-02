using System.Buffers.Binary;
using Avalonia.Controls;

namespace CleanChannel.App;

internal static class TrayIconFactory
{
    public static WindowIcon Create()
    {
        return new WindowIcon(new MemoryStream(CreateIcoBytes()));
    }

    private static byte[] CreateIcoBytes()
    {
        const int size = 16;
        const int headerSize = 6;
        const int directoryEntrySize = 16;
        const int dibHeaderSize = 40;
        const int xorBitmapSize = size * size * 4;
        const int andBitmapSize = size * 4;
        var bytes = new byte[headerSize + directoryEntrySize + dibHeaderSize + xorBitmapSize + andBitmapSize];

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, 2), 1);

        bytes[6] = size;
        bytes[7] = size;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12, 2), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14, 4), dibHeaderSize + xorBitmapSize + andBitmapSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(18, 4), headerSize + directoryEntrySize);

        var dib = bytes.AsSpan(headerSize + directoryEntrySize, dibHeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(dib[0..4], dibHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(dib[4..8], size);
        BinaryPrimitives.WriteInt32LittleEndian(dib[8..12], size * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(dib[12..14], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib[14..16], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(dib[20..24], xorBitmapSize);

        var pixels = bytes.AsSpan(headerSize + directoryEntrySize + dibHeaderSize, xorBitmapSize);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distanceX = x - 7.5;
                var distanceY = y - 7.5;
                var isDisc = distanceX * distanceX + distanceY * distanceY <= 52;
                var offset = ((size - 1 - y) * size + x) * 4;
                pixels[offset] = isDisc ? (byte)0x2D : (byte)0x18;
                pixels[offset + 1] = isDisc ? (byte)0xB8 : (byte)0x24;
                pixels[offset + 2] = isDisc ? (byte)0xFF : (byte)0x38;
                pixels[offset + 3] = isDisc ? byte.MaxValue : (byte)0;
            }
        }

        return bytes;
    }
}
