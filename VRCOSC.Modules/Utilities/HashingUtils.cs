// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Security.Cryptography;
using System.Text;

namespace Bluscream;

/// <summary>
/// Hashing utility functions
/// </summary>
public static class HashingUtils
{
    /// <summary>
    /// Generate SHA256 hash of a string input
    /// </summary>
    public static string GenerateSha256Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        try
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Generate CRC32 hash of a string input and return as hexadecimal string
    /// CRC32 produces an 8-character hex string (32 bits = 4 bytes = 8 hex chars)
    /// </summary>
    public static string GenerateCrc32Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        try
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var crc = ComputeCrc32(bytes);
            return crc.ToString("x8"); // 8 hex characters (lowercase)
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Compute CRC32 checksum for a byte array
    /// </summary>
    public static uint ComputeCrc32(byte[] data)
    {
        const uint polynomial = 0xEDB88320; // CRC32 polynomial
        var crc = 0xFFFFFFFFu;
        
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? polynomial : 0);
            }
        }
        
        return crc ^ 0xFFFFFFFFu;
    }
}
