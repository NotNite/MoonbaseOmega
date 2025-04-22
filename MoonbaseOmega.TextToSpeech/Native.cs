using System.Reflection;
using System.Runtime.InteropServices;

namespace MoonbaseOmega.TextToSpeech;

// https://github.com/dectalk/dectalk/blob/374caf7307f233d3aad708c64edbf743575ef6f5/src/dapi/src/api/ttsapi.h
public unsafe partial class Native {
    public const int WaveMapper = -1;
    public const int StatusSpeaking = 1;

    // Placeholder for the import resolver (see below)
    private const string LibraryName = "DECtalk";

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial uint TextToSpeechStartupExFonix(
        ref nint handle,
        int deviceNumber,
        uint deviceOptions,
        nint callbackRoutine,
        int instanceParameter,
        string dictionary
    );

    [LibraryImport(LibraryName)]
    public static partial uint TextToSpeechSetVolume(nint handle, VolumeType type, int volume);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial uint TextToSpeechSpeak(nint handle, string text, SpeechFlags flags);

    [LibraryImport(LibraryName)]
    public static partial uint TextToSpeechShutdown(nint handle);

    [LibraryImport(LibraryName)]
    public static partial uint TextToSpeechGetStatus(
        nint handle, [In] uint[] identifiers, [Out] uint[] statuses, uint numStatuses
    );

    public static void SetupResolver(string dllPath) =>
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(),
            (name, assembly, path) => name == LibraryName
                ? NativeLibrary.Load(dllPath)
                : NativeLibrary.Load(name, assembly, path));

    public enum VolumeType : uint {
        Main = 1,
        Attenuation = 2
    }

    [Flags]
    public enum SpeechFlags : uint {
        Normal = 0,
        Force = 1
    }
}
