// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Shared helpers for the OpenXR module suite.

using Silk.NET.OpenXR;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VRCOSC.Modules.OpenXR;

/// <summary>Shared utility helpers for the OpenXR module suite.</summary>
internal static unsafe class OpenXRHelper
{
    // XR_MAKE_VERSION(1, 0, 0)
    public const ulong XrVersion10 = (ulong)1 << 48;

    // Total hand joints per XR_EXT_hand_tracking spec (indices 0-25)
    public const int HandJointCount = 26;

    // ── UTF-8 write into a fixed-size buffer pointer ──────────────
    public static void WriteUtf8(byte* dst, int maxLen, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        int len   = Math.Min(bytes.Length, maxLen - 1);
        for (int i = 0; i < len; i++) dst[i] = bytes[i];
        dst[len] = 0;
    }

    // ── Per-struct fill helpers ───────────────────────────────────
    public static void FillApplicationInfo(ref ApplicationInfo info, string appName)
    {
        fixed (ApplicationInfo* p = &info)
            WriteUtf8((byte*)p, 128, appName);       // ApplicationName is the first field
    }

    public static void FillActionSetCreateInfo(ref ActionSetCreateInfo info, string name, string localName)
    {
        fixed (ActionSetCreateInfo* p = &info)
        {
            byte* raw = (byte*)p;
            // Memory layout: StructureType(8) + Next*(8) + ActionSetName[64] + LocalizedActionSetName[128] + Priority(4)
            WriteUtf8(raw + 16, 64,  name);
            WriteUtf8(raw + 80, 128, localName);
        }
    }

    public static void FillActionCreateInfo(ref ActionCreateInfo info, string name, string localName)
    {
        fixed (ActionCreateInfo* p = &info)
        {
            byte* raw = (byte*)p;
            // Memory layout: StructureType(8) + Next*(8) + ActionType(4) + pad(4) + ActionName[64] + LocalizedActionName[128]
            WriteUtf8(raw + 24, 64,  name);
            WriteUtf8(raw + 88, 128, localName);
        }
    }

    // ── Extension string pointer helpers ─────────────────────────
    public static IntPtr[] AllocStringPointers(IEnumerable<string> strings)
    {
        var list = strings.ToList();
        var ptrs = new IntPtr[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(list[i] + '\0');
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            ptrs[i] = ptr;
        }
        return ptrs;
    }

    public static void FreeStringPointers(IntPtr[] ptrs)
    {
        foreach (var p in ptrs) Marshal.FreeHGlobal(p);
    }

    // ── Linux shell helper ────────────────────────────────────────
    public static string RunShell(string command)
    {
        using var proc = new System.Diagnostics.Process();
        proc.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "/bin/bash",
            Arguments              = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(3000);
        return output;
    }
}

/// <summary>Mutable device state, shared between Stats and Gesture modules.</summary>
internal sealed class OpenXRDeviceState
{
    public bool  IsConnected;
    public bool  IsPresent;
    public bool  IsCharging;
    public float BatteryPercent;
    public readonly float[] FingerCurl = new float[4]; // [Index, Middle, Ring, Pinky]

    public void Reset()
    {
        IsConnected = IsPresent = IsCharging = false;
        BatteryPercent = 0f;
        Array.Clear(FingerCurl, 0, 4);
    }
}
