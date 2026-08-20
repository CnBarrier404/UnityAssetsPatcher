using System.Runtime.InteropServices;

namespace UnityAssetsPatcher.TUI.Framework;

internal static class WindowsNativeFilePicker
{
    private const int MaximumFileCharacters = 32768;
    private const int ExplorerStyle = 0x00080000;
    private const int FileMustExist = 0x00001000;
    private const int PathMustExist = 0x00000800;
    private const int NoChangeDirectory = 0x00000008;
    private const int HideReadOnly = 0x00000004;

    public static string? PickFile(string title, string fileTypeDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileTypeDescription);

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        nint filter = 0;
        nint fileBuffer = 0;
        nint dialogTitle = 0;

        try
        {
            filter = Marshal.StringToHGlobalUni(
                $"{fileTypeDescription} (*.zip)\0*.zip\0\0");
            fileBuffer = Marshal.AllocHGlobal(MaximumFileCharacters * sizeof(char));
            dialogTitle = Marshal.StringToHGlobalUni(title);

            byte[] emptyBuffer = new byte[MaximumFileCharacters * sizeof(char)];
            Marshal.Copy(emptyBuffer, 0, fileBuffer, emptyBuffer.Length);

            var openFileName = new OpenFileName
            {
                StructSize = Marshal.SizeOf<OpenFileName>(),
                Owner = GetConsoleWindow(),
                Filter = filter,
                FilterIndex = 1,
                File = fileBuffer,
                MaximumFile = MaximumFileCharacters,
                Title = dialogTitle,
                Flags = ExplorerStyle | FileMustExist | PathMustExist |
                        NoChangeDirectory | HideReadOnly
            };

            return GetOpenFileName(ref openFileName) == 0
                ? null
                : Marshal.PtrToStringUni(fileBuffer);
        }
        finally
        {
            if (dialogTitle != 0)
            {
                Marshal.FreeHGlobal(dialogTitle);
            }

            if (fileBuffer != 0)
            {
                Marshal.FreeHGlobal(fileBuffer);
            }

            if (filter != 0)
            {
                Marshal.FreeHGlobal(filter);
            }
        }
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", ExactSpelling = true, SetLastError = true)]
    private static extern int GetOpenFileName(ref OpenFileName openFileName);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetConsoleWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileName
    {
        public int StructSize;
        public nint Owner;
        public nint Instance;
        public nint Filter;
        public nint CustomFilter;
        public int MaximumCustomFilter;
        public int FilterIndex;
        public nint File;
        public int MaximumFile;
        public nint FileTitle;
        public int MaximumFileTitle;
        public nint InitialDirectory;
        public nint Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public nint DefaultExtension;
        public nint CustomData;
        public nint Hook;
        public nint TemplateName;
        public nint Reserved;
        public int Reserved2;
        public int ExtendedFlags;
    }
}
