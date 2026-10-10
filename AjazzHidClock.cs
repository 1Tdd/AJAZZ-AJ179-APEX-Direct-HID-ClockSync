using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class AjazzHidClock
{
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
    private const uint GENERIC_READ  = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ  = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const int ERROR_NO_MORE_ITEMS = 259;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public UIntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    public sealed class HidInfo
    {
        public string Path = "";
        public ushort Vid;
        public ushort Pid;
        public ushort Version;
        public ushort UsagePage;
        public ushort Usage;
        public ushort InputLen;
        public ushort OutputLen;
        public ushort FeatureLen;
        public int MetadataOpenError;
        public int WriteOpenError;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid HidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetAttributes(
        SafeFileHandle HidDeviceObject,
        ref HIDD_ATTRIBUTES Attributes);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetPreparsedData(
        SafeFileHandle HidDeviceObject,
        out IntPtr PreparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(
        IntPtr PreparsedData,
        out HIDP_CAPS Capabilities);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_SetFeature(
        SafeFileHandle HidDeviceObject,
        byte[] ReportBuffer,
        int ReportBufferLength);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        IntPtr Enumerator,
        IntPtr hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr DeviceInfoSet,
        IntPtr DeviceInfoData,
        ref Guid InterfaceClassGuid,
        uint MemberIndex,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        uint DeviceInterfaceDetailDataSize,
        out uint RequiredSize,
        IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    private static string GetDevicePath(
        IntPtr infoSet,
        ref SP_DEVICE_INTERFACE_DATA iface)
    {
        uint required = 0;
        SetupDiGetDeviceInterfaceDetail(
            infoSet, ref iface, IntPtr.Zero, 0, out required, IntPtr.Zero);

        if (required == 0)
            return null;

        IntPtr detail = Marshal.AllocHGlobal((int)required);
        try
        {
            // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize:
            // 8 on 64-bit Windows, 6 on 32-bit Windows.
            Marshal.WriteInt32(detail, (IntPtr.Size == 8) ? 8 : 6);

            uint required2;
            if (!SetupDiGetDeviceInterfaceDetail(
                infoSet, ref iface, detail, required, out required2, IntPtr.Zero))
                return null;

            // DevicePath begins at offset 4 in the variable-length structure.
            IntPtr pathPtr = IntPtr.Add(detail, 4);
            return Marshal.PtrToStringUni(pathPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(detail);
        }
    }

    public static List<HidInfo> EnumerateAllHid()
    {
        var list = new List<HidInfo>();
        Guid hidGuid;
        HidD_GetHidGuid(out hidGuid);

        IntPtr infoSet = SetupDiGetClassDevs(
            ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

        if (infoSet == INVALID_HANDLE_VALUE)
            return list;

        try
        {
            uint index = 0;
            while (true)
            {
                var iface = new SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

                if (!SetupDiEnumDeviceInterfaces(
                    infoSet, IntPtr.Zero, ref hidGuid, index, ref iface))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == ERROR_NO_MORE_ITEMS)
                        break;
                    index++;
                    continue;
                }

                index++;
                string path = GetDevicePath(infoSet, ref iface);
                if (String.IsNullOrEmpty(path))
                    continue;

                var info = new HidInfo();
                info.Path = path;

                // IMPORTANT: metadata probe uses desiredAccess = 0.
                // This avoids false negatives on HID collections that Windows
                // refuses to open GENERIC_READ|GENERIC_WRITE.
                using (SafeFileHandle h = CreateFile(
                    path,
                    0,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero))
                {
                    if (h == null || h.IsInvalid)
                    {
                        info.MetadataOpenError = Marshal.GetLastWin32Error();
                        list.Add(info);
                        continue;
                    }

                    var attr = new HIDD_ATTRIBUTES();
                    attr.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));

                    if (HidD_GetAttributes(h, ref attr))
                    {
                        info.Vid = attr.VendorID;
                        info.Pid = attr.ProductID;
                        info.Version = attr.VersionNumber;
                    }

                    IntPtr preparsed = IntPtr.Zero;
                    try
                    {
                        if (HidD_GetPreparsedData(h, out preparsed) &&
                            preparsed != IntPtr.Zero)
                        {
                            HIDP_CAPS caps;
                            int status = HidP_GetCaps(preparsed, out caps);
                            if (status == 0x00110000) // HIDP_STATUS_SUCCESS
                            {
                                info.UsagePage = caps.UsagePage;
                                info.Usage = caps.Usage;
                                info.InputLen = caps.InputReportByteLength;
                                info.OutputLen = caps.OutputReportByteLength;
                                info.FeatureLen = caps.FeatureReportByteLength;
                            }
                        }
                    }
                    finally
                    {
                        if (preparsed != IntPtr.Zero)
                            HidD_FreePreparsedData(preparsed);
                    }
                }

                list.Add(info);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(infoSet);
        }

        return list;
    }

    private static SafeFileHandle OpenForFeatureWrite(string path, out string mode, out int error)
    {
        // Try the least-privileged write mode first.
        SafeFileHandle h = CreateFile(
            path,
            GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (h != null && !h.IsInvalid)
        {
            mode = "GENERIC_WRITE";
            error = 0;
            return h;
        }

        int e1 = Marshal.GetLastWin32Error();
        if (h != null) h.Dispose();

        h = CreateFile(
            path,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (h != null && !h.IsInvalid)
        {
            mode = "GENERIC_READ|GENERIC_WRITE";
            error = 0;
            return h;
        }

        error = Marshal.GetLastWin32Error();
        mode = "failed (write err " + e1 + ", rw err " + error + ")";
        return h;
    }

    public static string SyncClock(
        ushort vid,
        ushort pid,
        DateTime localTime)
    {
        var all = EnumerateAllHid();

        // Conservative safety gate:
        // exact device VID/PID + exact vendor control TLC used by AJAZZ RTC/status.
        var candidates = all.Where(x =>
            x.Vid == vid &&
            x.Pid == pid &&
            x.UsagePage == 0xFFFF &&
            x.Usage == 0x0002 &&
            x.FeatureLen >= 16)
            .ToList();

        if (candidates.Count == 0)
        {
            var sameDevice = all.Where(x => x.Vid == vid && x.Pid == pid).ToList();

            if (sameDevice.Count == 0)
                return String.Format(
                    "ERR|No HID interface found for VID=0x{0:X4} PID=0x{1:X4}",
                    vid, pid);

            string seen = String.Join("; ", sameDevice.Select(x =>
                String.Format(
                    "UP=0x{0:X4}/U=0x{1:X4}/F={2}",
                    x.UsagePage, x.Usage, x.FeatureLen)));

            return "ERR|Device found, but exact control collection " +
                   "UsagePage=0xFFFF Usage=0x0002 was not found. Seen: " + seen;
        }

        foreach (var c in candidates)
        {
            string openMode;
            int openErr;
            using (SafeFileHandle h = OpenForFeatureWrite(c.Path, out openMode, out openErr))
            {
                if (h == null || h.IsInvalid)
                    continue;

                // Windows HidD_SetFeature expects ReportBufferLength to match
                // the collection's feature report length. Keep at least 65 bytes
                // for AJ-series devices whose descriptor reports 65.
                int reportLen = Math.Max((int)c.FeatureLen, 65);
                byte[] report = new byte[reportLen];

                // RTC report captured/documented for AJAZZ dock:
                // [0] report id = 0
                // [1] 0x28 SET_OLEDCLOCK (vendor command opcode name)
                // [2..7] zero
                // [8] 0xD7 marker
                // [9..10] year, big-endian
                // [11] month
                // [12] day
                // [13] hour
                // [14] minute
                // [15] second
                int year = localTime.Year;
                report[0] = 0x00;
                report[1] = 0x28;
                report[8] = 0xD7;
                report[9] = (byte)((year >> 8) & 0xFF);
                report[10] = (byte)(year & 0xFF);
                report[11] = (byte)localTime.Month;
                report[12] = (byte)localTime.Day;
                report[13] = (byte)localTime.Hour;
                report[14] = (byte)localTime.Minute;
                report[15] = (byte)localTime.Second;

                bool ok = HidD_SetFeature(h, report, report.Length);
                if (ok)
                {
                    return String.Format(
                        "OK|RTC sent to VID=0x{0:X4} PID=0x{1:X4} " +
                        "UP=0x{2:X4} U=0x{3:X4} FeatureLen={4} Open={5}",
                        c.Vid, c.Pid, c.UsagePage, c.Usage, c.FeatureLen, openMode);
                }
            }
        }

        return "ERR|Exact AJAZZ RTC control collection found, but Windows would not accept the feature-report write. Close AJAZZ/iot_driver and retry as Administrator.";
    }
}
