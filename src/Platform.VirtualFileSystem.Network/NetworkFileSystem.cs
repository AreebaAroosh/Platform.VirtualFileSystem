using System;
using System.Collections.Generic;
using System.Linq;
using Platform.Collections;
using Platform.VirtualFileSystem.Network.Client;
using Platform.VirtualFileSystem.Providers;
using NetworkFileSystemWeakReference = Platform.References.WeakReference<Platform.VirtualFileSystem.Network.NetworkFileSystem>;

namespace Platform.VirtualFileSystem.Network
{
	public class NetworkFileSystem
		: AbstractFileSystem
	{
		public override event FileSystemActivityEventHandler Activity;

		private static readonly IDictionary<string, IList<NetworkFileSystemWeakReference>> staticFileSystemsCache;

		static NetworkFileSystem()
		{
			staticFileSystemsCache = new Dictionary<string, IList<NetworkFileSystemWeakReference>>();
		}
				
		internal virtual bool ShouldSupportSynthesizedActivityEvents
		{
			get
			{
				return true;
			}
		}

		internal static void RaiseActivityEvent(NetworkFileSystem networkFileSystem, FileSystemActivityEventArgs eventArgs)
		{
			var uniqueId = networkFileSystem.GetUniqueId();

			lock (staticFileSystemsCache)
			{
				IList<NetworkFileSystemWeakReference> fileSystems;

				if (staticFileSystemsCache.TryGetValue(uniqueId, out fileSystems))
				{
					var node = networkFileSystem.Resolve(eventArgs.Path, eventArgs.NodeType);

					foreach (var reference in fileSystems)
					{
						var fileSystem = reference.Target;

						if (fileSystem != null)
						{
							var currentNode = fileSystem.Resolve(eventArgs.Path, eventArgs.NodeType);

							lock (currentNode.Attributes.SyncLock)
							{
								if (currentNode != node)
								{
									foreach (var attribute in node.Attributes)
									{
										((NetworkNodeAndFileAttributes)currentNode.Attributes).SetValue<object>(attribute.Key, attribute.Value, false);
									}
								}
							}

							fileSystem.OnActivity(eventArgs);
						}
					}
				}
			}
		}

		protected virtual void OnActivity(FileSystemActivityEventArgs eventArgs)
		{
			if (Activity != null)
			{
				var node = this.Resolve(eventArgs.Path, eventArgs.NodeType);

				if (this.SecurityManager.CurrentContext.HasAccess(new AccessVerificationContext(node, FileSystemSecuredOperation.View)))
				{
					Activity(this, eventArgs);
				}
			}
		}

		public new NetworkNodeAddress RootAddress
		{
			get
			{
				return (NetworkNodeAddress)base.RootAddress;
			}
		}

		public override bool SupportsActivityEvents
		{
			get
			{
				return true;
			}
		}

		public override bool SupportsSeeking
		{
			get
			{
				return true;
			}
		}

		public virtual TimeSpan Ping()
		{
			FreeClientContext context;

			using (context = GetFreeClientContext())
			{
				return context.NetworkFileSystemClient.Ping();
			}
		}

		private readonly TimedReferenceDictionary<INetworkFileSystemClient, INetworkFileSystemClient> freeClients;
		private readonly TimedReferenceDictionary<INetworkFileSystemClient, INetworkFileSystemClient> freeClientsForBinaryAccess;

		public class FreeClientContext
			: IDisposable
		{
			private readonly bool forBinary;
			private readonly NetworkFileSystem fileSystem;

			public virtual INetworkFileSystemClient NetworkFileSystemClient { get; private set; }

			protected internal FreeClientContext(NetworkFileSystem fileSystem, INetworkFileSystemClient client, bool forBinary)
			{
				this.forBinary = forBinary;
				this.fileSystem = fileSystem;
				this.NetworkFileSystemClient = client;				
			}

			private bool alreadyDisposed = false;

			public virtual void Dispose()
			{
				if (this.alreadyDisposed)
				{
					return;
				}

				this.fileSystem.ReturnFreeClient(this.NetworkFileSystemClient, this.forBinary);

				this.alreadyDisposed = true;
			}
		}

		protected internal virtual FreeClientContext GetFreeClientContext()
		{
			return GetFreeClientContext(false);
		}

		protected internal virtual FreeClientContext GetFreeClientContext(bool forBinary)
		{
			return new FreeClientContext(this, RetrieveFreeClient(forBinary), forBinary);
		}

		protected internal virtual INetworkFileSystemClient RetrieveFreeClient(bool forBinary)
		{
			TimedReferenceDictionary<INetworkFileSystemClient, INetworkFileSystemClient> cache;

			if (forBinary)
			{
				cache = this.freeClientsForBinaryAccess;
			}
			else
			{
				cache = this.freeClients;
			}

			lock (cache)
			{
				while (true)
				{
					if (cache.Count > 0)
					{
						foreach (var client in cache.Select(c => c.Value))
						{
							if (client.Connected)
							{
								cache.Remove(client);

								return client;
							}
							else
							{
								cache.Remove(client);

								break;
							}
						}

						continue;
					}
					else
					{
						var retval = this.CreateNewClient();

						retval.Connect();

						return retval;
					}
				}
			}
		}

		protected internal virtual void ReturnFreeClient(INetworkFileSystemClient client, bool forBinary)
		{
			TimedReferenceDictionary<INetworkFileSystemClient, INetworkFileSystemClient> cache;

			if (forBinary)
			{
				cache = this.freeClientsForBinaryAccess;
			}
			else
			{
				cache = this.freeClients;
			}

			if (!client.Connected)
			{				
				lock (cache)
				{
					cache.Clear();
				}

				return;
			}

			lock (cache)
			{
				if (client.Connected)
				{
					if (!cache.ContainsKey(client))
					{
						cache[client] = client;
					}
				}
			}
		}

		private INetworkFileSystemClient CreateNewClient()
		{
			var type = Type.GetType("Platform.VirtualFileSystem.Network.Text.TextNetworkFileSystemClient, Platform.VirtualFileSystem.Network.Text");

			var client = (INetworkFileSystemClient)Activator.CreateInstance(type, this.RootAddress.ServerName, this.RootAddress.Port ?? 6021);

			return client;
		}

		internal protected NetworkFileSystem(INodeAddress rootAddress, FileSystemOptions options)
			: base(rootAddress, null, options)
		{
			var comparer = ObjectReferenceIdentityEqualityComparer<INetworkFileSystemClient>.Default;

			this.freeClients = new TimedReferenceDictionary<INetworkFileSystemClient, INetworkFileSystemClient>(TimeSpan.FromMinutes(25), comparer);
			this.freeClientsForBinaryAccess = new TimedReferenceDictionary<INetworkFileSystemClient, INetworkFileSystemClient>(TimeSpan.FromMinutes(25), comparer);

			lock (staticFileSystemsCache)
			{
				IList<NetworkFileSystemWeakReference> fileSystems;
				var uniqueId = this.GetUniqueId();

				if (!staticFileSystemsCache.TryGetValue(uniqueId, out fileSystems))
				{
					fileSystems = new List<NetworkFileSystemWeakReference>();

					staticFileSystemsCache[GetUniqueId()] = fileSystems;
				}

				fileSystems.Add(new NetworkFileSystemWeakReference(this));				
			}
		}

		private string GetUniqueId()
		{
			string uniqueId;

			var address = (NetworkNodeAddress)this.RootAddress;

			if ((uniqueId = this.Options.Variables["server-scheme-uniqueid"]) == null)
			{
				uniqueId = address.RemoteUri;
			}

			return
				address.ServerName + ":::"
				+ uniqueId
				+ ":::" + address.UserName
				+ ":::" + address.Password
				+ ":::" + address.Port;
		}

		protected override INode CreateNode(INodeAddress address, NodeType nodeType)
		{
			if (nodeType == NodeType.Directory)
			{
				return new NetworkDirectory(this, address);
			}
			else if (nodeType == NodeType.File || nodeType == NodeType.Any)
			{
				return new NetworkFile(this, address);
			}
			else
			{
				throw new NodeTypeNotSupportedException(nodeType);
			}
		}
	}
}
