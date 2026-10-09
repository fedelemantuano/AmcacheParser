using System;
using Amcache.Classes;
using Amcache.Converters;
using CsvHelper;
using CsvHelper.Configuration;

namespace AmcacheParser;

// Shared maps keep CSV columns and the flat JSON schema in sync.
internal static class ExportMaps
{
    internal static ClassMap<FileEntryNew> NewUnassociatedFile(CsvContext context, string dt)
    {
        var foo = context.AutoMap<FileEntryNew>();

        foo.Map(m => m.ApplicationName).Index(0);

        foo.Map(m => m.ProgramId).Index(1);
        foo.Map(t => t.FileKeyLastWriteTimestamp).Convert(t =>
                t.Value.FileKeyLastWriteTimestamp.ToString(dt))
            .Index(2);
        foo.Map(m => m.SHA1).Index(3);
        foo.Map(m => m.IsOsComponent).Index(4);
        foo.Map(m => m.FullPath).Index(5);
        foo.Map(m => m.Name).Index(6);
        foo.Map(m => m.FileExtension).Index(7);

        foo.Map(t => t.LinkDate).Convert(
            t => t.Value.LinkDate == null ? string.Empty : t.Value.LinkDate?.ToString(dt)).Index(8);
        foo.Map(m => m.ProductName).TypeConverter<CustomNullTypeConverter<string>>().Index(9);

        foo.Map(m => m.Size).Index(10);

        foo.Map(m => m.Version).Index(11);
        foo.Map(m => m.ProductVersion).Index(12);

        foo.Map(m => m.LongPathHash).Index(13);

        foo.Map(m => m.BinaryType).Index(14);
        foo.Map(m => m.IsPeFile).Index(15);

        foo.Map(m => m.BinFileVersion).Index(16);
        foo.Map(m => m.BinProductVersion).Index(17);


        foo.Map(m => m.Language).Index(18);
        foo.Map(m => m.Publisher).Ignore();

        return foo;
    }

    internal static ClassMap<ProgramsEntryNew> NewProgram(CsvContext context, string dt)
    {
        var foo = context.AutoMap<ProgramsEntryNew>();

        foo.Map(m => m.ProgramId).Index(0);
        foo.Map(t => t.KeyLastWriteTimestamp).Convert(t =>
                t.Value.KeyLastWriteTimestamp.ToString(dt))
            .Index(1);
        foo.Map(m => m.Name).Index(2);
        foo.Map(m => m.Version).Index(3);
        foo.Map(m => m.Publisher).Index(4);

        foo.Map(t => t.InstallDate).Convert(row =>
        {
            if (row.Value.InstallDate.HasValue)
            {
                return row.Value.InstallDate.Value.ToString(
                    dt);
            }

            return "";
        }).Index(5);

        foo.Map(m => m.OSVersionAtInstallTime).Index(6);

        foo.Map(m => m.BundleManifestPath).Index(7);
        foo.Map(m => m.HiddenArp).Index(8);
        foo.Map(m => m.InboxModernApp).Index(9);
        foo.Map(m => m.Language).Index(10);
        foo.Map(m => m.ManifestPath).Index(11);
        foo.Map(m => m.MsiPackageCode).Index(12);
        foo.Map(m => m.MsiProductCode).Index(13);

        foo.Map(m => m.PackageFullName).Index(14);
        foo.Map(m => m.ProgramInstanceId).Index(15);
        foo.Map(m => m.RegistryKeyPath).Index(16);
        foo.Map(m => m.RootDirPath).Index(17);

        foo.Map(m => m.Type).Index(18);
        foo.Map(m => m.Source).Index(19);
        foo.Map(m => m.StoreAppType).Index(20);

        foo.Map(m => m.UninstallString).Index(21);

        return foo;
    }

    internal static ClassMap<FileEntryNew> NewAssociatedFile(CsvContext context, string dt)
    {
        var foo = context.AutoMap<FileEntryNew>();

        foo.Map(m => m.ApplicationName).Index(0);

        foo.Map(m => m.ProgramId).Index(1);
        foo.Map(t => t.FileKeyLastWriteTimestamp).Convert(t =>
                t.Value.FileKeyLastWriteTimestamp.ToString(dt))
            .Index(2);
        foo.Map(m => m.SHA1).Index(3);
        foo.Map(m => m.IsOsComponent).Index(4);
        foo.Map(m => m.FullPath).Index(5);
        foo.Map(m => m.Name).Index(6);
        foo.Map(m => m.FileExtension).Index(7);

        foo.Map(t => t.LinkDate).Convert(t =>
            t.Value.LinkDate?.ToString(dt)).Index(8);
        foo.Map(m => m.ProductName).Index(9);

        foo.Map(m => m.Size).Index(10);

        foo.Map(m => m.Version).Index(11);
        foo.Map(m => m.ProductVersion).Index(12);

        foo.Map(m => m.LongPathHash).Index(13);

        foo.Map(m => m.BinaryType).Index(14);
        foo.Map(m => m.IsPeFile).Index(15);

        foo.Map(m => m.BinFileVersion).Index(16);
        foo.Map(m => m.BinProductVersion).Index(17);

        foo.Map(m => m.Language).Index(18);
        foo.Map(m => m.Publisher).Ignore();

        return foo;
    }

    internal static ClassMap<Shortcut> Shortcut(CsvContext context, string dt)
    {
        var foo = context.AutoMap<Shortcut>();

        foo.Map(m => m.KeyName).Index(0);
        foo.Map(m => m.LnkName).Index(1);

        foo.Map(t => t.KeyLastWriteTimestamp).Convert(t =>
            t.Value.KeyLastWriteTimestamp.ToString(dt)).Index(2);

        return foo;
    }

    internal static ClassMap<DriverBinary> DriverBinary(CsvContext context, string dt)
    {
        var foo = context.AutoMap<DriverBinary>();

        foo.Map(m => m.KeyName).Index(0);
        foo.Map(t => t.KeyLastWriteTimestamp).Convert(t =>
            t.Value.KeyLastWriteTimestamp.ToString(dt)).Index(1);
        foo.Map(t => t.DriverTimeStamp).Convert(t =>
            t.Value.DriverTimeStamp?.ToString(dt)).Index(2);
        foo.Map(t => t.DriverLastWriteTime).Convert(t =>
            t.Value.DriverLastWriteTime?.ToString(dt)).Index(3);

        foo.Map(m => m.DriverName).Index(4);

        foo.Map(m => m.DriverInBox).Index(5);
        foo.Map(m => m.DriverIsKernelMode).Index(6);
        foo.Map(m => m.DriverSigned).Index(7);
        foo.Map(m => m.DriverCheckSum).Index(8);
        foo.Map(m => m.DriverCompany).Index(9);
        foo.Map(m => m.DriverId).Index(10);
        foo.Map(m => m.DriverPackageStrongName).Index(11);
        foo.Map(m => m.DriverType).Index(12);
        foo.Map(m => m.DriverVersion).Index(13);
        foo.Map(m => m.ImageSize).Index(14);
        foo.Map(m => m.Inf).Index(15);
        foo.Map(m => m.Product).Index(16);
        foo.Map(m => m.ProductVersion).Index(17);
        foo.Map(m => m.Service).Index(18);
        foo.Map(m => m.WdfVersion).Index(19);

        return foo;
    }

    internal static ClassMap<DeviceContainer> DeviceContainer(CsvContext context, string dt)
    {
        var foo = context.AutoMap<DeviceContainer>();

        foo.Map(m => m.KeyName).Index(0);
        foo.Map(t => t.KeyLastWriteTimestamp).Convert(t =>
            t.Value.KeyLastWriteTimestamp.ToString(dt)).Index(1);

        foo.Map(m => m.Categories).Index(2);

        foo.Map(m => m.DiscoveryMethod).Index(3);
        foo.Map(m => m.FriendlyName).Index(4);
        foo.Map(m => m.Icon).Index(5);
        foo.Map(m => m.IsActive).Index(6);
        foo.Map(m => m.IsConnected).Index(7);
        foo.Map(m => m.IsMachineContainer).Index(8);
        foo.Map(m => m.IsNetworked).Index(9);
        foo.Map(m => m.IsPaired).Index(10);
        foo.Map(m => m.Manufacturer).Index(11);
        foo.Map(m => m.ModelId).Index(12);
        foo.Map(m => m.ModelName).Index(13);
        foo.Map(m => m.ModelNumber).Index(14);
        foo.Map(m => m.PrimaryCategory).Index(15);
        foo.Map(m => m.State).Index(16);

        return foo;
    }

    internal static ClassMap<DriverPackage> DriverPackage(CsvContext context, string dt)
    {
        var foo = context.AutoMap<DriverPackage>();

        foo.Map(m => m.KeyName).Index(0);
        foo.Map(t => t.KeyLastWriteTimestamp).Convert(t =>
            t.Value.KeyLastWriteTimestamp.ToString(dt)).Index(1);
        foo.Map(t => t.Date)
            .Convert(t => t.Value.Date?.ToString(dt))
            .Index(2);

        foo.Map(m => m.Class).Index(3);

        foo.Map(m => m.Directory).Index(4);
        foo.Map(m => m.DriverInBox).Index(5);
        foo.Map(m => m.Hwids).Index(6);
        foo.Map(m => m.Inf).Index(7);
        foo.Map(m => m.Provider).Index(8);
        foo.Map(m => m.SubmissionId).Index(9);
        foo.Map(m => m.SYSFILE).Index(10);
        foo.Map(m => m.Version).Index(11);
        foo.Map(m => m.ClassGuid).Ignore();

        return foo;
    }

    internal static ClassMap<DevicePnp> DevicePnp(CsvContext context, string dt)
    {
        var foo = context.AutoMap<DevicePnp>();

        foo.Map(m => m.KeyName).Index(0);
        foo.Map(t => t.KeyLastWriteTimestamp).Convert(t =>
            t.Value.KeyLastWriteTimestamp.ToString(dt)).Index(1);

        foo.Map(m => m.BusReportedDescription).Index(2);

        foo.Map(m => m.Class).Index(3);
        foo.Map(m => m.ClassGuid).Index(4);
        foo.Map(m => m.Compid).Index(5);
        foo.Map(m => m.ContainerId).Index(6);
        foo.Map(m => m.Description).Index(7);
        foo.Map(m => m.DriverId).Index(8);
        foo.Map(m => m.DriverPackageStrongName).Index(9);
        foo.Map(m => m.DriverName).Index(10);
        foo.Map(m => m.DriverVerDate).Index(11);
        foo.Map(m => m.DriverVerVersion).Index(12);
        foo.Map(m => m.Enumerator).Index(13);
        foo.Map(m => m.HWID).Index(14);
        foo.Map(m => m.Inf).Index(15);
        foo.Map(m => m.InstallState).Index(16);
        foo.Map(m => m.Manufacturer).Index(17);
        foo.Map(m => m.MatchingId).Index(18);
        foo.Map(m => m.Model).Index(19);
        foo.Map(m => m.ParentId).Index(20);
        foo.Map(m => m.ProblemCode).Index(21);
        foo.Map(m => m.Provider).Index(22);
        foo.Map(m => m.Service).Index(23);
        foo.Map(m => m.Stackid).Index(24);
        foo.Map(m => m.DeviceState).Ignore();

        return foo;
    }

    internal static ClassMap<FileEntryOld> OldUnassociatedFile(CsvContext context, string dt)
    {
        var foo = context.AutoMap<FileEntryOld>();

        foo.Map(m => m.ProgramName).Index(0);
        foo.Map(m => m.ProgramID).Index(1);
        foo.Map(m => m.VolumeID).Index(2);
        foo.Map(t => t.VolumeIDLastWriteTimestamp).Convert(t =>
            t.Value.VolumeIDLastWriteTimestamp.ToString(dt)).Index(3);
        foo.Map(m => m.FileID).Index(4);
        foo.Map(t => t.FileIDLastWriteTimestamp).Convert(t =>
            t.Value.FileIDLastWriteTimestamp.ToString(dt)).Index(5);
        foo.Map(m => m.SHA1).Index(6);
        foo.Map(m => m.FullPath).Index(7);
        foo.Map(m => m.FileExtension).Index(8);
        foo.Map(m => m.MFTEntryNumber).Index(9);
        foo.Map(m => m.MFTSequenceNumber).Index(10);
        foo.Map(m => m.FileSize).TypeConverter<CustomNullTypeConverter<string>>().Index(11);
        foo.Map(m => m.FileVersionString).Index(12);
        foo.Map(m => m.FileVersionNumber).Index(13);
        foo.Map(m => m.FileDescription).Index(14);

        foo.Map(m => m.SizeOfImage).TypeConverter<CustomNullTypeConverter<string>>().Index(15);
        foo.Map(m => m.PEHeaderHash).Index(16);
        foo.Map(m => m.PEHeaderChecksum).TypeConverter<CustomNullTypeConverter<string>>().Index(17);

        foo.Map(m => m.BinProductVersion).Index(18);
        foo.Map(m => m.BinFileVersion).Index(19);
        foo.Map(m => m.LinkerVersion).Index(20);
        foo.Map(m => m.BinaryType).Index(21);
        foo.Map(m => m.IsLocal).Index(22);
        foo.Map(m => m.GuessProgramID).Index(23);

        foo.Map(t => t.Created)
            .Convert(t => t.Value.Created == null ? string.Empty : t.Value.Created?.ToString(dt))
            .Index(24);
        foo.Map(t => t.LastModified).Convert(t =>
            t.Value.LastModified == null ? string.Empty : t.Value.LastModified?.ToString(dt)).Index(25);
        foo.Map(t => t.LastModifiedStore).Convert(t =>
            t.Value.LastModifiedStore == null ? string.Empty : t.Value.LastModifiedStore?.ToString(dt)).Index(26);
        foo.Map(t => t.LinkDate)
            .Convert(t => t.Value.LinkDate == null ? string.Empty : t.Value.LinkDate?.ToString(dt))
            .Index(27);
        foo.Map(m => m.LanguageID).TypeConverter<CustomNullTypeConverter<string>>().Index(28);

        foo.Map(m => m.ProductName).TypeConverter<CustomNullTypeConverter<string>>().Index(29);
        foo.Map(m => m.CompanyName).TypeConverter<CustomNullTypeConverter<string>>().Index(30);
        foo.Map(m => m.SwitchBackContext).TypeConverter<CustomNullTypeConverter<string>>().Index(31);

        return foo;
    }

    internal static ClassMap<ProgramsEntryOld> OldProgram(CsvContext context, string dt)
    {
        var foo = context.AutoMap<ProgramsEntryOld>();

        foo.Map(m => m.ProgramID).Index(0);
        foo.Map(t => t.LastWriteTimestamp).Convert(t =>
            t.Value.LastWriteTimestamp.ToString(dt)).Index(1);
        foo.Map(m => m.ProgramName_0).Index(2);
        foo.Map(m => m.ProgramVersion_1).Index(3);
        foo.Map(m => m.VendorName_2).Index(4);

        foo.Map(t => t.InstallDateEpoch_a).Convert(t =>
            t.Value.InstallDateEpoch_a?.ToString(dt)).Index(5);
        foo.Map(t => t.InstallDateEpoch_b).Convert(t =>
            t.Value.InstallDateEpoch_b?.ToString(dt)).Index(6);

        foo.Map(m => m.LanguageCode_3).Index(7);
        foo.Map(m => m.InstallSource_6).Index(8);
        foo.Map(m => m.UninstallRegistryKey_7).Index(9);
        foo.Map(m => m.PathsList_d).Index(10);

        return foo;
    }

    internal static ClassMap<FileEntryOld> OldAssociatedFile(CsvContext context, string dt)
    {
        var foo = context.AutoMap<FileEntryOld>();

        foo.Map(m => m.ProgramName).Index(0);
        foo.Map(m => m.ProgramID).Index(1);
        foo.Map(m => m.VolumeID).Index(2);
        foo.Map(t => t.VolumeIDLastWriteTimestamp).Convert(t =>
                t.Value.VolumeIDLastWriteTimestamp.ToString(dt))
            .Index(3);
        foo.Map(m => m.FileID).Index(4);
        foo.Map(t => t.FileIDLastWriteTimestamp).Convert(t =>
                t.Value.FileIDLastWriteTimestamp.ToString(dt))
            .Index(5);
        foo.Map(m => m.SHA1).Index(6);
        foo.Map(m => m.FullPath).Index(7);
        foo.Map(m => m.FileExtension).Index(8);
        foo.Map(m => m.MFTEntryNumber).Index(9);
        foo.Map(m => m.MFTSequenceNumber).Index(10);
        foo.Map(m => m.FileSize).Index(11);
        foo.Map(m => m.FileVersionString).Index(12);
        foo.Map(m => m.FileVersionNumber).Index(13);
        foo.Map(m => m.FileDescription).Index(14);

        foo.Map(m => m.SizeOfImage).Index(15);
        foo.Map(m => m.PEHeaderHash).Index(16);
        foo.Map(m => m.PEHeaderChecksum).Index(17);

        foo.Map(m => m.BinProductVersion).Index(18);
        foo.Map(m => m.BinFileVersion).Index(19);
        foo.Map(m => m.LinkerVersion).Index(20);
        foo.Map(m => m.BinaryType).Index(21);
        foo.Map(m => m.IsLocal).Index(22);
        foo.Map(m => m.GuessProgramID).Index(23);

        foo.Map(t => t.Created)
            .Convert(t => t.Value.Created?.ToString(dt))
            .Index(24);
        foo.Map(t => t.LastModified).Convert(t =>
            t.Value.LastModified?.ToString(dt)).Index(25);
        foo.Map(t => t.LastModifiedStore).Convert(t =>
            t.Value.LastModifiedStore?.ToString(dt)).Index(26);
        foo.Map(t => t.LinkDate)
            .Convert(t => t.Value.LinkDate?.ToString(dt))
            .Index(27);
        foo.Map(m => m.LanguageID).Index(28);

        return foo;
    }
}
