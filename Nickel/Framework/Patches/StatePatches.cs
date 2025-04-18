using HarmonyLib;
using Microsoft.Extensions.Logging;
using Nanoray.Shrike;
using Nanoray.Shrike.Harmony;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Nickel;

internal static class StatePatches
{
	internal static EventHandler<EnumerateAllArtifactsEventArgs>? OnEnumerateAllArtifactsBeforeAddingArtifacts;
	internal static EventHandler<EnumerateAllArtifactsEventArgs>? OnEnumerateAllArtifactsAfterAddingArtifacts;
	internal static RefEventHandler<ModifyPotentialExeCardsEventArgs>? OnModifyPotentialExeCards;
	internal static RefEventHandler<LoadEventArgs>? OnLoad;
	internal static EventHandler<State>? OnUpdate;

	internal static void Apply(Harmony harmony)
	{
		harmony.Patch(
			original: AccessTools.DeclaredMethod(typeof(State), nameof(State.EnumerateAllArtifacts))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(State)}.{nameof(State.EnumerateAllArtifacts)}`"),
			transpiler: new HarmonyMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(EnumerateAllArtifacts_Transpiler))
		);
		harmony.Patch(
			original: typeof(State).GetNestedTypes(AccessTools.all).SelectMany(t => t.GetMethods(AccessTools.all)).First(m => m.Name.StartsWith("<PopulateRun>") && m.ReturnType == typeof(Route))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(State)}.<compiler-generated-type>.<PopulateRun>`"),
			transpiler: new HarmonyMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(State_PopulateRun_Delegate_Transpiler))
		);
		harmony.Patch(
			original: AccessTools.DeclaredMethod(typeof(State), nameof(State.SaveIfRelease))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(State)}.{nameof(State.SaveIfRelease)}`"),
			postfix: new HarmonyMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(SaveIfRelease_Postfix))
		);
		harmony.Patch(
			original: AccessTools.DeclaredMethod(typeof(State), nameof(State.Load))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(State)}.{nameof(State.Load)}`"),
			postfix: new HarmonyMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(Load_Postfix))
		);
		harmony.Patch(
			original: AccessTools.DeclaredMethod(typeof(State), nameof(State.Update))
				?? throw new InvalidOperationException($"Could not patch game methods: missing method `{nameof(State)}.{nameof(State.Update)}`"),
			postfix: new HarmonyMethod(AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(Update_Postfix)), priority: Priority.Last)
		);
	}
	
	[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
	private static IEnumerable<CodeInstruction> EnumerateAllArtifacts_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod)
	{
		try
		{
			return new SequenceBlockMatcher<CodeInstruction>(instructions)
				.Find([
					ILMatches.Ldarg(0),
					ILMatches.Ldfld(nameof(State._artifactScratch)),
					ILMatches.Call("Clear"),
				])
				.Insert(SequenceMatcherPastBoundsDirection.After, SequenceMatcherInsertionResultingBounds.IncludingInsertion, [
					new CodeInstruction(OpCodes.Ldarg_0),
					new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(EnumerateAllArtifacts_Transpiler_BeforeAddingArtifacts))),
				])
				.Find(ILMatches.Instruction(OpCodes.Endfinally))
				.PointerMatcher(SequenceMatcherRelativeElement.AfterLast)
				.ExtractLabels(out var labels)
				.Insert(SequenceMatcherPastBoundsDirection.Before, SequenceMatcherInsertionResultingBounds.IncludingInsertion, [
					new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
					new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(MethodBase.GetCurrentMethod()!.DeclaringType!, nameof(EnumerateAllArtifacts_Transpiler_AfterAddingArtifacts))),
				])
				.AllElements();
		}
		catch (Exception ex)
		{
			Nickel.Instance.ModManager.Logger.LogCritical("Could not patch method {Method} - {ModLoaderName} probably won't work.\nReason: {Exception}", originalMethod, NickelConstants.Name, ex);
			return instructions;
		}
	}

	private static void EnumerateAllArtifacts_Transpiler_BeforeAddingArtifacts(State state)
	{
		var args = new EnumerateAllArtifactsEventArgs
		{
			State = state,
			Artifacts = state._artifactScratch,
		};
		OnEnumerateAllArtifactsBeforeAddingArtifacts?.Invoke(null, args);
	}

	private static void EnumerateAllArtifacts_Transpiler_AfterAddingArtifacts(State state)
	{
		var args = new EnumerateAllArtifactsEventArgs
		{
			State = state,
			Artifacts = state._artifactScratch,
		};
		OnEnumerateAllArtifactsAfterAddingArtifacts?.Invoke(null, args);
	}

	[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
	private static IEnumerable<CodeInstruction> State_PopulateRun_Delegate_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod)
	{
		try
		{
			return new SequenceBlockMatcher<CodeInstruction>(instructions)
				.Find([
					ILMatches.Ldarg(0),
					ILMatches.Ldfld("chars"),
					ILMatches.LdcI4((int)Deck.shard),
					ILMatches.Call("Contains"),
					ILMatches.Brtrue,
					ILMatches.Ldloc<List<Card>>(originalMethod).CreateLdlocaInstruction(out var ldlocaCards),
					ILMatches.Instruction(OpCodes.Newobj),
					ILMatches.Call("Add")
				])
				.PointerMatcher(SequenceMatcherRelativeElement.AfterLast)
				.ExtractLabels(out var labels)
				.Insert(SequenceMatcherPastBoundsDirection.Before, SequenceMatcherInsertionResultingBounds.IncludingInsertion, [
					new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
					new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(originalMethod.DeclaringType, "chars")),
					ldlocaCards,
					new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(StatePatches), nameof(State_PopulateRun_Delegate_Transpiler_ModifyPotentialExeCards)))
				])
				.AllElements();
		}
		catch (Exception ex)
		{
			Nickel.Instance.ModManager.Logger.LogCritical("Could not patch method {Method} - {ModLoaderName} probably won't work.\nReason: {Exception}", originalMethod, NickelConstants.Name, ex);
			return instructions;
		}
	}

	private static void State_PopulateRun_Delegate_Transpiler_ModifyPotentialExeCards(IEnumerable<Deck> chars, ref List<Card> cards)
	{
		var args = new ModifyPotentialExeCardsEventArgs
		{
			Characters = chars.ToHashSet(),
			ExeCards = cards,
		};
		OnModifyPotentialExeCards?.Invoke(null, ref args);
		cards = args.ExeCards;
	}

	private static void SaveIfRelease_Postfix(State __instance)
	{
		if (Nickel.Instance.DebugMode != DebugMode.EnabledWithSaving)
			return;
		if (FeatureFlags.Debug)
			__instance.Save();
	}

	private static void Load_Postfix(int slot, ref State.SaveSlot __result)
	{
		var args = new LoadEventArgs
		{
			Slot = slot,
			Data = __result,
		};
		OnLoad?.Invoke(null, ref args);
		__result = args.Data;
	}

	private static void Update_Postfix(State __instance)
		=> OnUpdate?.Invoke(null, __instance);

	internal struct EnumerateAllArtifactsEventArgs
	{
		public required State State { get; init; }
		public required List<Artifact> Artifacts { get; init; }
	}

	internal struct ModifyPotentialExeCardsEventArgs
	{
		public required HashSet<Deck> Characters { get; init; }
		public required List<Card> ExeCards;
	}

	internal struct LoadEventArgs
	{
		public required int Slot { get; init; }
		public required State.SaveSlot Data;
	}
}
