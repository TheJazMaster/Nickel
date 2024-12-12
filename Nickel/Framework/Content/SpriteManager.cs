using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Nickel;

internal sealed class SpriteManager
{
	private readonly Func<IModManifest, ILogger> LoggerProvider;
	private readonly EnumCasePool EnumCasePool;
	private readonly IModManifest VanillaModManifest;
	private readonly Dictionary<Spr, ISpriteEntry> SpriteToEntry = [];
	private readonly Dictionary<string, ISpriteEntry> UniqueNameToEntry = [];

	public SpriteManager(Func<IModManifest, ILogger> loggerProvider, EnumCasePool enumCasePool, IModManifest vanillaModManifest)
	{
		this.LoggerProvider = loggerProvider;
		this.EnumCasePool = enumCasePool;
		this.VanillaModManifest = vanillaModManifest;
		SpriteLoaderPatches.OnGetTexture += this.OnGetTexture;
	}
	
	private static string StandardizeUniqueName(string uniqueName)
		=> uniqueName.ToLower(CultureInfo.InvariantCulture);

	public ISpriteEntry RegisterSprite(IModManifest owner, string name, Func<Texture2D> textureProvider, bool isDynamic)
	{
		var uniqueName = $"{owner.UniqueName}::{name}";
		Spr sprite;
		
		if (this.UniqueNameToEntry.TryGetValue(StandardizeUniqueName(uniqueName), out var existing))
		{
			this.LoggerProvider(owner).LogWarning("A sprite with the unique name `{UniqueName}` is already registered; overriding it.", uniqueName);
			sprite = existing.Sprite;
		}
		else
		{
			sprite = this.EnumCasePool.ObtainEnumCase<Spr>();
		}

		if (isDynamic)
			return this.ActuallyRegisterSprite(new DynamicEntry(owner, uniqueName, name, sprite, textureProvider));
		return this.ActuallyRegisterSprite(new StaticEntry(owner, uniqueName, name, sprite, textureProvider));
	}

	public ISpriteEntry RegisterSprite(IModManifest owner, string name, Func<Stream> streamProvider)
	{
		var uniqueName = $"{owner.UniqueName}::{name}";
		Spr sprite;

		if (this.UniqueNameToEntry.TryGetValue(StandardizeUniqueName(uniqueName), out var existing))
		{
			this.LoggerProvider(owner).LogWarning("A sprite with the unique name `{UniqueName}` is already registered; overriding it.", uniqueName);
			sprite = existing.Sprite;
		}
		else
		{
			sprite = this.EnumCasePool.ObtainEnumCase<Spr>();
		}
			
		return this.ActuallyRegisterSprite(new StaticEntry(owner, uniqueName, name, sprite, streamProvider));
	}

	private T ActuallyRegisterSprite<T>(T entry) where T : ISpriteEntry
	{
		var standardizedUniqueName = StandardizeUniqueName(entry.UniqueName);
		this.SpriteToEntry[entry.Sprite] = entry;
		this.UniqueNameToEntry[standardizedUniqueName] = entry;

		var path = $"{NickelConstants.Name}-managed/{standardizedUniqueName}";
		SpriteMapping.mapping[entry.Sprite] = new SpritePath(path);
		SpriteMapping.strToId[path] = entry.Sprite;
		return entry;
	}

	public ISpriteEntry? LookupBySpr(Spr spr)
	{
		if (this.SpriteToEntry.TryGetValue(spr, out var entry))
			return entry;
		if (Enum.GetName(spr) is not { } vanillaName)
			return null;

		return new StaticEntry(
			modOwner: this.VanillaModManifest,
			uniqueName: vanillaName,
			localName: vanillaName,
			sprite: spr,
			textureProvider: () => SpriteLoader.Get(spr)!
		);
	}

	public ISpriteEntry? LookupByUniqueName(string uniqueName)
		=> this.UniqueNameToEntry.GetValueOrDefault(StandardizeUniqueName(uniqueName));

	private void OnGetTexture(object? _, SpriteLoaderPatches.GetTextureEventArgs e)
	{
		if (e.Texture is not null)
			return;
		if (!this.SpriteToEntry.TryGetValue(e.Sprite, out var entry))
			return;
		e.Texture = entry.ObtainTexture();
		e.IsDynamic = entry is DynamicEntry;
	}

	private sealed class StaticEntry
		: ISpriteEntry
	{
		public IModManifest ModOwner { get; }
		public string UniqueName { get; }
		public string LocalName { get; }
		public Spr Sprite { get; }

		private Func<Stream>? StreamProvider { get; set; }
		private Func<Texture2D>? TextureProvider { get; set; }
		private Texture2D? TextureStorage { get; set; }

		public StaticEntry(IModManifest modOwner, string uniqueName, string localName, Spr sprite, Func<Stream> streamProvider)
		{
			this.ModOwner = modOwner;
			this.UniqueName = uniqueName;
			this.LocalName = localName;
			this.Sprite = sprite;
			this.StreamProvider = streamProvider;
		}

		public StaticEntry(IModManifest modOwner, string uniqueName, string localName, Spr sprite, Func<Texture2D> textureProvider)
		{
			this.ModOwner = modOwner;
			this.UniqueName = uniqueName;
			this.LocalName = localName;
			this.Sprite = sprite;
			this.TextureProvider = textureProvider;
		}

		public Texture2D ObtainTexture()
		{
			if (this.TextureStorage is { } texture)
				return texture;

			if (this.TextureProvider is { } textureProvider)
			{
				texture = textureProvider();
				this.TextureProvider = null;
			}
			else if (this.StreamProvider is { } streamProvider)
			{
				using var stream = streamProvider();
				texture = Texture2D.FromStream(MG.inst.GraphicsDevice, stream);
				this.StreamProvider = null;
			}
			else
			{
				throw new InvalidOperationException();
			}

			this.TextureStorage = texture;
			this.StreamProvider = null;
			return texture;
		}
	}
	
	private sealed class DynamicEntry
		: ISpriteEntry
	{
		public IModManifest ModOwner { get; }
		public string UniqueName { get; }
		public string LocalName { get; }
		public Spr Sprite { get; }
		private Func<Texture2D> TextureProvider { get; }

		public DynamicEntry(IModManifest modOwner, string uniqueName, string localName, Spr sprite, Func<Texture2D> textureProvider)
		{
			this.ModOwner = modOwner;
			this.UniqueName = uniqueName;
			this.LocalName = localName;
			this.Sprite = sprite;
			this.TextureProvider = textureProvider;
		}

		public Texture2D ObtainTexture()
			=> this.TextureProvider();
	}
}
