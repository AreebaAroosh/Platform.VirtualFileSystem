using System;
using ZLib = ICSharpCode.SharpZipLib.Zip;

namespace Platform.VirtualFileSystem.Providers.Zip
{
	internal class ZipNodeAttributes
		: AbstractTypeBasedNodeAttributes
	{
		protected IZipNode zipNode;

		public ZipNodeAttributes(IZipNode zipNode)
			: base(zipNode)
		{
			this.zipNode = zipNode;
		}

		public override bool Exists
		{
			get
			{
				if (this.zipNode.NodeType.IsLikeDirectory)
				{
					return ((ZipFileSystem)this.zipNode.FileSystem).DirectoryExists(this.zipNode.Address.AbsolutePath);
				}
				else
				{
					return this.zipNode.ZipEntry != null
					       || ((ZipFileSystem)this.zipNode.FileSystem).GetTempFile((ZipFile)zipNode, false) != null;
				}
			}
		}

		protected virtual void VerifyZipEntry()
		{
			if (this.zipNode.ZipEntry == null)
			{
				throw new FileNodeNotFoundException(this.zipNode.Address);
			}
		}

		public override DateTime? CreationTime
		{
			get
			{
				lock (this)
				{
					VerifyZipEntry();

					return this.zipNode.ZipEntry.DateTime;
				}
			}
			set
			{
			}
		}

		public override DateTime? LastAccessTime
		{
			get
			{
				lock (this)
				{
					VerifyZipEntry();

					return this.zipNode.ZipEntry.DateTime;
				}
			}
			set
			{
			}
		}

		public override DateTime? LastWriteTime
		{
			get
			{
				lock (this)
				{
					VerifyZipEntry();

					return this.zipNode.ZipEntry.DateTime;
				}
			}
			set
			{
			}
		}

		public override INodeAttributes Refresh()
		{
			this.zipNode.SetZipEntry(((ZipFileSystem)this.zipNode.FileSystem).GetEntry(this.zipNode.ZipPath));

			return this;
		}
	}
}
