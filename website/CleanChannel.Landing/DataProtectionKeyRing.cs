using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CleanChannel.Landing;

public sealed class DataProtectionKeyRing
{
    private readonly string _path;

    private DataProtectionKeyRing(string path) => _path = path;

    public string Path => _path;

    public static DataProtectionKeyRing Initialize(string configuredPath)
    {
        var path = Environment.ExpandEnvironmentVariables(configuredPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Landing Data Protection key ring path must be configured.");
        }

        try
        {
            Directory.CreateDirectory(path);
            if (!Directory.Exists(path))
            {
                throw new IOException("The key ring path is not a directory.");
            }

            var keyRing = new DataProtectionKeyRing(System.IO.Path.GetFullPath(path));
            keyRing.AssertWritable();
            return keyRing;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new InvalidOperationException("Landing Data Protection key ring path cannot be created or written.", exception);
        }
    }

    public bool IsWritable()
    {
        if (!Directory.Exists(_path))
        {
            return false;
        }

        try
        {
            WriteProbe();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool IsCryptographicallyUsable(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!IsWritable())
        {
            return false;
        }

        try
        {
            var payload = RandomNumberGenerator.GetBytes(32);
            var protector = provider.CreateProtector("CleanChannel.Landing.DataProtectionHealth");
            var protectedPayload = protector.Protect(payload);
            var roundTripped = protector.Unprotect(protectedPayload);
            return CryptographicOperations.FixedTimeEquals(payload, roundTripped);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void AssertWritable()
    {
        if (!IsWritable())
        {
            throw new InvalidOperationException("Landing Data Protection key ring path cannot be written.");
        }
    }

    private void WriteProbe()
    {
        var probePath = System.IO.Path.Combine(_path, $".write-test-{Guid.NewGuid():N}");
        try
        {
            using (var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
            {
                stream.WriteByte(1);
                stream.Flush(true);
            }
        }
        finally
        {
            try { File.Delete(probePath); } catch (IOException) { }
        }
    }
}
