using System.IO;
using Platform.IO;

namespace Platform.VirtualFileSystem.Providers.Zip
{
	internal class ZipFileStream
		: StreamWrapper
	{
		private readonly ZipFile zipFile;

		public static ZipFileStream CreateInputStream(ZipFile zipFile)
		{
			if (!zipFile.ParentDirectory.Exists)
			{
				if (!zipFile.ParentDirectory.Refresh().Exists)
				{
					throw new DirectoryNodeNotFoundException(zipFile.ParentDirectory.Address);
				}
			}

			zipFile.Refresh();

			var zipEntry = ((IZipNode)zipFile).ZipEntry;

			if (zipEntry == null)
			{
				throw new FileNotFoundException(zipFile.Address.Uri);
			}

			var zipFileInfo = ((ZipFileSystem)zipFile.FileSystem).GetZipFileInfo(zipFile.Address.AbsolutePath);
			var shadowFile = zipFileInfo.ShadowFile;

			if (shadowFile != null)
			{
				return new ZipFileStream(zipFile, shadowFile.GetContent().GetInputStream());
			}
			else
			{
				return new ZipFileStream(zipFile, ((ZipFileSystem)zipFile.FileSystem).GetInputStream(zipEntry));
			}
		}

		public static ZipFileStream CreateOutputStream(ZipFile zipFile)
		{
			if (!zipFile.ParentDirectory.Exists)
			{
				if (!zipFile.ParentDirectory.Refresh().Exists)
				{
					throw new DirectoryNodeNotFoundException(zipFile.ParentDirectory.Address);
				}
			}

			var zipFileInfo = ((ZipFileSystem)zipFile.FileSystem).GetZipFileInfo(zipFile.Address.AbsolutePath);

			var shadowFile = zipFileInfo.GetShadowFile(true);

			return new ZipFileStream(zipFile, shadowFile.GetContent().GetOutputStream());
		}

		private ZipFileStream(ZipFile zipFile, Stream stream)
			: base(stream)
		{
			this.zipFile = zipFile;
		}
	}
}