using System.Reflection.PortableExecutable;

namespace Qx.Unity;

public static class UnityExecutableValidator
{
    public static void Validate(string assembly_path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assembly_path);
        try
        {
            using var stream = new FileStream(
                assembly_path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                131072,
                FileOptions.RandomAccess);
            if (stream.Length is <= 0 or > UnityBoundedFile.MaximumAssemblyBytes)
                throw new InvalidDataException($"Unity executable has an invalid size: {assembly_path}");

            using var image = new PEReader(stream, PEStreamOptions.LeaveOpen);
            PEHeaders headers = image.PEHeaders;
            PEHeader header = headers.PEHeader
                ?? throw new InvalidDataException($"Unity executable has no PE header: {assembly_path}");
            if (!SupportedArchitecture(header.Magic, headers.CoffHeader.Machine))
                throw new InvalidDataException($"Unity executable has an unsupported architecture: {assembly_path}");
            if ((headers.CoffHeader.Characteristics & Characteristics.ExecutableImage) == 0)
                throw new InvalidDataException($"Unity executable is not marked executable: {assembly_path}");
            if (header.SizeOfHeaders <= 0 || header.SizeOfHeaders > stream.Length || header.SizeOfImage <= 0)
                throw new InvalidDataException($"Unity executable has invalid image dimensions: {assembly_path}");

            bool executable_code = false;
            bool entry_point_valid = header.AddressOfEntryPoint == 0;
            foreach (SectionHeader section in headers.SectionHeaders)
            {
                ValidateSection(section, header, stream.Length, assembly_path);
                bool contains_code =
                    (section.SectionCharacteristics & SectionCharacteristics.ContainsCode) != 0 &&
                    (section.SectionCharacteristics & SectionCharacteristics.MemExecute) != 0 &&
                    section.SizeOfRawData > 0;
                if (!contains_code)
                    continue;

                executable_code = true;
                long start = section.VirtualAddress;
                long end = start + Math.Max(section.VirtualSize, section.SizeOfRawData);
                if (header.AddressOfEntryPoint >= start && header.AddressOfEntryPoint < end)
                    entry_point_valid = true;
            }

            if (!executable_code || !entry_point_valid)
                throw new InvalidDataException($"Unity executable has no valid executable code entry: {assembly_path}");
        }
        catch (BadImageFormatException error)
        {
            throw new InvalidDataException($"Unity executable is not a valid PE image: {assembly_path}", error);
        }
        catch (OverflowException error)
        {
            throw new InvalidDataException($"Unity executable contains an overflowing image range: {assembly_path}", error);
        }
    }

    static void ValidateSection(SectionHeader section, PEHeader header, long file_length, string assembly_path)
    {
        if (section.VirtualAddress < 0 || section.VirtualSize < 0 ||
            section.PointerToRawData < 0 || section.SizeOfRawData < 0)
            throw new InvalidDataException($"Unity executable contains a negative section range: {assembly_path}");

        long virtual_size = Math.Max(section.VirtualSize, section.SizeOfRawData);
        long virtual_end = checked((long)section.VirtualAddress + virtual_size);
        if (virtual_size > 0 && virtual_end > header.SizeOfImage)
            throw new InvalidDataException($"Unity executable section exceeds the virtual image: {assembly_path}");
        if (section.SizeOfRawData == 0)
            return;

        long raw_end = checked((long)section.PointerToRawData + section.SizeOfRawData);
        if (section.PointerToRawData < header.SizeOfHeaders || raw_end > file_length)
            throw new InvalidDataException($"Unity executable section exceeds the file image: {assembly_path}");
    }

    static bool SupportedArchitecture(PEMagic format, Machine machine) =>
        format == PEMagic.PE32 && machine == Machine.I386 ||
        format == PEMagic.PE32Plus && machine is Machine.Amd64 or Machine.Arm64;
}
