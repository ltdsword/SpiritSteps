using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CorgiAR.EditorTools
{
    /// <summary>
    /// Builds one smooth base controller per pet family plus a per-pet
    /// AnimatorOverrideController. AnyState transitions keyed on the int
    /// <c>AnimationID</c> (0 Idle, 1 React, 2 Walk, 3 Run, 4 Sit/Rest, 5 Eat) with
    /// fixed ~0.2s crossfades and no self-transitions. The Dog-Kit base keeps the
    /// Sitting/Eating Start→Cycle→End chains; the Ultimate-Animated-Animals base
    /// uses single Rest / Eat clips.
    /// </summary>
    public static partial class DogARSetupGenerator
    {
        private const string AnimationDir = "Assets/CorgiAR/Animation";
        private const string DogKitBasePath = AnimationDir + "/PetLocomotion_DogKit.controller";
        private const string UaaBasePath = AnimationDir + "/PetLocomotion_UAA.controller";
        private const string DogModelsDir = "Assets/Bublisher/3D Stylized Animated Dogs Kit/Models/";

        private const string DogKitBaseModel = "corgi";      // clips: corgi_<suffix>
        private const string UaaClipPrefix = "AnimalArmature|"; // clips: AnimalArmature|<suffix>
        private const string UaaBaseModelPath = "Assets/CorgiAR/Animals/Fox.fbx";

        private struct AnimRole
        {
            public string State;
            public int AnimationId;   // -1 = chained state, not AnyState-driven
            public bool Loop;
            public string[] ClipCandidates;
        }

        private static readonly AnimRole[] DogKitRoles =
        {
            new() { State = "Breathing",    AnimationId = 0, Loop = true,  ClipCandidates = new[]{"Breathing"} },
            new() { State = "WigglingTail",  AnimationId = 1, Loop = true,  ClipCandidates = new[]{"WigglingTail"} },
            new() { State = "Walking",       AnimationId = 2, Loop = true,  ClipCandidates = new[]{"Walking01"} },
            new() { State = "Running",       AnimationId = 3, Loop = true,  ClipCandidates = new[]{"Running"} },
            new() { State = "SittingStart",  AnimationId = 4, Loop = false, ClipCandidates = new[]{"SittingStart"} },
            new() { State = "SittingCycle",  AnimationId = -1, Loop = true, ClipCandidates = new[]{"SittingCycle"} },
            new() { State = "EatingStart",   AnimationId = 5, Loop = false, ClipCandidates = new[]{"EatingStart"} },
            new() { State = "EatingCycle",   AnimationId = -1, Loop = true, ClipCandidates = new[]{"EatingCycle"} },
            new() { State = "EatingEnd",     AnimationId = -1, Loop = false,ClipCandidates = new[]{"EatingEnd"} },
        };

        private static readonly AnimRole[] UaaRoles =
        {
            new() { State = "Idle",   AnimationId = 0, Loop = true, ClipCandidates = new[]{"Idle"} },
            new() { State = "React",  AnimationId = 1, Loop = true, ClipCandidates = new[]{"Idle_2"} },
            new() { State = "Walk",   AnimationId = 2, Loop = true, ClipCandidates = new[]{"Walk"} },
            new() { State = "Gallop", AnimationId = 3, Loop = true, ClipCandidates = new[]{"Gallop"} },
            new() { State = "Rest",   AnimationId = 4, Loop = true, ClipCandidates = new[]{"Idle_Headlow","Idle_2_HeadLow","Idle_2"} },
            new() { State = "Eat",    AnimationId = 5, Loop = true, ClipCandidates = new[]{"Eating"} },
        };

        private static void BuildAnimationAssets()
        {
            EnsureFolder(AnimationDir);

            AnimatorController dogKitBase = BuildBase(PetFamily.DogKit, DogKitBasePath);
            AnimatorController uaaBase = BuildBase(PetFamily.UltimateAnimated, UaaBasePath);

            foreach (PetEntry pet in PetCatalog.Entries)
            {
                AnimatorController baseController = pet.Family == PetFamily.DogKit ? dogKitBase : uaaBase;
                BuildOverride(baseController, pet);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (PetEntry pet in PetCatalog.Entries)
                AssetDatabase.ImportAsset(pet.OverrideControllerPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        // ---- clip lookup ----

        private static AnimationClip[] LoadClips(string fbxOrPrefabPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxOrPrefabPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();
        }

        private static AnimationClip ResolveClip(PetFamily family, AnimationClip[] clips,
            string modelId, string[] candidates)
        {
            foreach (string suffix in candidates)
            {
                string wanted = family == PetFamily.DogKit
                    ? modelId + "_" + suffix
                    : UaaClipPrefix + suffix;
                AnimationClip hit = clips.FirstOrDefault(c => c.name == wanted);
                if (hit != null)
                    return hit;
            }
            return null;
        }

        // ---- base controller ----

        private static AnimatorController BuildBase(PetFamily family, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AnimRole[] roles = family == PetFamily.DogKit ? DogKitRoles : UaaRoles;
            string modelPath = family == PetFamily.DogKit
                ? DogModelsDir + DogKitBaseModel + ".fbx"
                : UaaBaseModelPath;
            string modelId = family == PetFamily.DogKit ? DogKitBaseModel : "";
            AnimationClip[] clips = LoadClips(modelPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("AnimationID", AnimatorControllerParameterType.Int);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            machine.states = Array.Empty<ChildAnimatorState>();

            var states = new Dictionary<string, AnimatorState>();
            float y = 40f;
            foreach (AnimRole role in roles)
            {
                AnimatorState s = machine.AddState(role.State, new Vector3(300f, y, 0f));
                s.motion = ResolveClip(family, clips, modelId, role.ClipCandidates)
                           ?? throw new MissingReferenceException(
                               $"{family} base: no clip for state {role.State} (tried {string.Join(",", role.ClipCandidates)})");
                states[role.State] = s;
                y += 70f;
            }
            machine.defaultState = states[roles[0].State];

            foreach (AnimRole role in roles)
                if (role.AnimationId >= 0)
                {
                    AnimatorStateTransition t = machine.AddAnyStateTransition(states[role.State]);
                    t.canTransitionToSelf = false;
                    Condition(t, AnimatorConditionMode.Equals, role.AnimationId, 0.2f);
                }

            if (family == PetFamily.DogKit)
            {
                TimedTransition(states["SittingStart"], states["SittingCycle"], 0.9f, 0.12f);
                Condition(states["SittingCycle"].AddTransition(states["Breathing"]),
                    AnimatorConditionMode.NotEqual, 6, 0.28f);
                TimedTransition(states["EatingStart"], states["EatingCycle"], 0.9f, 0.12f);
                Condition(states["EatingCycle"].AddTransition(states["EatingEnd"]),
                    AnimatorConditionMode.NotEqual, 5, 0.18f);
                TimedTransition(states["EatingEnd"], states["Breathing"], 0.92f, 0.18f);
            }

            EditorUtility.SetDirty(controller);
            Debug.Log($"BASE CONTROLLER built: {path}");
            return controller;
        }

        private static void Condition(AnimatorStateTransition t, AnimatorConditionMode mode, int threshold, float duration)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.AddCondition(mode, threshold, "AnimationID");
        }

        private static void TimedTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = duration;
        }

        // ---- per-pet override ----

        /// <summary>Where a pet's animation clips live (Dog-Kit clips are on the .fbx, not the .prefab).</summary>
        private static string ClipSourcePath(PetEntry pet) =>
            pet.Family == PetFamily.DogKit ? DogModelsDir + pet.Id + ".fbx" : pet.SourcePrefabPath;

        private static void BuildOverride(AnimatorController baseController, PetEntry pet)
        {
            AnimRole[] roles = pet.Family == PetFamily.DogKit ? DogKitRoles : UaaRoles;
            string modelId = pet.Family == PetFamily.DogKit ? pet.Id : "";
            AnimationClip[] clips = LoadClips(ClipSourcePath(pet));

            var aoc = new AnimatorOverrideController(baseController) { name = "Pet_" + pet.Id };
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(overrides);

            // base motion name -> role (base uses corgi_/Fox clips)
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip baseClip = overrides[i].Key;
                AnimRole role = MatchRole(roles, pet.Family, baseClip.name);
                AnimationClip replacement = role.ClipCandidates != null
                    ? ResolveClip(pet.Family, clips, modelId, role.ClipCandidates)
                    : null;
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(baseClip, replacement);
            }
            aoc.ApplyOverrides(overrides);

            AssetDatabase.DeleteAsset(pet.OverrideControllerPath);
            AssetDatabase.CreateAsset(aoc, pet.OverrideControllerPath);
        }

        private static AnimRole MatchRole(AnimRole[] roles, PetFamily family, string baseClipName)
        {
            // strip the base model prefix to a suffix
            string suffix = family == PetFamily.DogKit
                ? (baseClipName.StartsWith(DogKitBaseModel + "_")
                    ? baseClipName.Substring(DogKitBaseModel.Length + 1) : baseClipName)
                : (baseClipName.StartsWith(UaaClipPrefix)
                    ? baseClipName.Substring(UaaClipPrefix.Length) : baseClipName);

            foreach (AnimRole role in roles)
                if (role.ClipCandidates.Contains(suffix))
                    return role;
            return default;
        }
    }
}
