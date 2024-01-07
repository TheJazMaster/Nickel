using Nickel.Common;
using System.Collections.Generic;

namespace Nickel;

internal sealed class AssemblyModManifest : IAssemblyModManifest
{
	public string UniqueName
		=> this.ModManifest.UniqueName;

	public SemanticVersion Version
		=> this.ModManifest.Version;

	public SemanticVersion RequiredApiVersion
		=> this.ModManifest.RequiredApiVersion;

	public IReadOnlySet<ModDependency> Dependencies
		=> this.ModManifest.Dependencies;

	public string? DisplayName
		=> this.ModManifest.DisplayName;

	public string? Author
		=> this.ModManifest.Author;

	public string ModType
		=> this.ModManifest.ModType;

	public ModLoadPhase LoadPhase
		=> this.ModManifest.LoadPhase;

	public IReadOnlyList<ISubmodEntry> Submods
		=> this.ModManifest.Submods;

	public IReadOnlyDictionary<string, object> ExtensionData
		=> this.ModManifest.ExtensionData;

	public string EntryPointAssembly { get; internal set; } = null!;

	public string? EntryPointType { get; internal set; }

	public IReadOnlyList<ModAssemblyReference> AssemblyReferences { get; internal set; } = [];

	private IModManifest ModManifest { get; }

	public AssemblyModManifest(IModManifest modManifest)
	{
		this.ModManifest = modManifest;
	}
}
