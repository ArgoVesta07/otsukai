# Unity 6.3 LTS — Breaking Changes

**Last verified:** 2026-02-13 (6.3 LTS content); 2026-11-24 (6.4–6.6 sections appended at the end)

This document tracks breaking API changes and behavioral differences between Unity 2022 LTS
(likely in model training) and Unity 6.3 LTS (current version). Organized by risk level.

## HIGH RISK — Will Break Existing Code

### Entities/DOTS API Complete Overhaul
**Versions:** Entities 1.0+ (Unity 6.0+)

```csharp
// ❌ OLD (pre-Unity 6, GameObjectEntity pattern)
public class HealthComponent : ComponentData {
    public float Value;
}

// ✅ NEW (Unity 6+, IComponentData)
public struct HealthComponent : IComponentData {
    public float Value;
}

// ❌ OLD: ComponentSystem
public class DamageSystem : ComponentSystem { }

// ✅ NEW: ISystem (unmanaged, Burst-compatible)
public partial struct DamageSystem : ISystem {
    public void OnCreate(ref SystemState state) { }
    public void OnUpdate(ref SystemState state) { }
}
```

**Migration:** Follow Unity's ECS migration guide. Major architectural changes required.

---

### Input System — Legacy Input Deprecated
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: Input class (deprecated)
if (Input.GetKeyDown(KeyCode.Space)) { }

// ✅ NEW: Input System package
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) { }
```

**Migration:** Install Input System package, replace all `Input.*` calls with new API.

---

### URP/HDRP Renderer Feature API Changes
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: ScriptableRenderPass.Execute signature
public override void Execute(ScriptableRenderContext context, ref RenderingData data)

// ✅ NEW: Uses RenderGraph API
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
```

**Migration:** Update custom render passes to use RenderGraph API.

---

## MEDIUM RISK — Behavioral Changes

### Addressables — Asset Loading Returns
**Versions:** Unity 6.2+

Asset loading failures now throw exceptions by default instead of returning null.
Add proper exception handling or use `TryLoad` variants.

```csharp
// ❌ OLD: Silent null on failure
var handle = Addressables.LoadAssetAsync<Sprite>("key");
var sprite = handle.Result; // null if failed

// ✅ NEW: Throws on failure, use try/catch or TryLoad
try {
    var handle = Addressables.LoadAssetAsync<Sprite>("key");
    var sprite = await handle.Task;
} catch (Exception e) {
    Debug.LogError($"Failed to load: {e}");
}
```

---

### Physics — Default Solver Iterations Changed
**Versions:** Unity 6.0+

Default solver iterations increased for better stability.
Check `Physics.defaultSolverIterations` if you rely on old behavior.

---

## LOW RISK — Deprecations (Still Functional)

### UGUI (Legacy UI)
**Status:** Deprecated but supported
**Replacement:** UI Toolkit

UGUI still works but UI Toolkit is recommended for new projects.

---

### Legacy Particle System
**Status:** Deprecated
**Replacement:** Visual Effect Graph (VFX Graph)

---

### Old Animation System
**Status:** Deprecated
**Replacement:** Animator Controller (Mecanim)

---

## Platform-Specific Breaking Changes

### WebGL
- **Unity 6.0+**: WebGPU is now the default (WebGL 2.0 fallback available)
- Update shaders for WebGPU compatibility

### Android
- **Unity 6.0+**: Minimum API level raised to 24 (Android 7.0)

### iOS
- **Unity 6.0+**: Minimum deployment target raised to iOS 13

---

## Migration Checklist

When upgrading from 2022 LTS to Unity 6.3 LTS:

- [ ] Audit all DOTS/ECS code (complete rewrite likely needed)
- [ ] Replace `Input` class with Input System package
- [ ] Update custom render passes to RenderGraph API
- [ ] Add exception handling to Addressables calls
- [ ] Test physics behavior (solver iterations changed)
- [ ] Consider migrating UGUI to UI Toolkit for new UI
- [ ] Update WebGL shaders for WebGPU
- [ ] Verify minimum platform versions (Android/iOS)

---

**Sources:**
- https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/upgrade-guide.html

---

## 6.3 → 6.4

**Last verified:** 2026-11-24 — source: https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity64

- **`Object.Destroy` now calls `OnDisable` on every descendant** (previously only the object and its direct children), matching `DestroyImmediate`. Remove any manual workaround that propagated `OnDisable` deeper — it now causes duplicate calls.
- **Narrower import artifact dependencies.** A dependency only triggers reimport when its import *result* changes. `ScriptedImporter`/`AssetPostprocessor` code that reads another asset's source file or an indirect artifact must declare it with `AssetImportContext.DependsOnSourceAsset` / `DependsOnArtifact`.
- **URP Compatibility Mode removed** for custom render passes, along with the `URP_COMPATIBILITY_MODE` define. Custom passes must use the Render Graph system.
- **SRP Core → Physics is now a soft dependency.** Disabling `com.unity.modules.physics` breaks local volumes. `IVolume` is removed; use `VolumeGizmoDrawer` for custom volume visualisation.
- **`LightShadowCasterMode` renamed:** `NonLightmappedOnly` → `ShadowMask`, `Everything` → `DistanceShadowMask` (auto-updated).
- **Project Auditor is built into the Editor;** `CodeModuleInstructionAnalyzer`, `InstructionAnalysisContext` and related types are now internal.
- Apple platforms: runtime libraries moved to `Frameworks/UnityRuntime.framework/UnityRuntime`. HoloLens 2 deprecated (use OpenXR).

## 6.4 → 6.5

**Last verified:** 2026-11-24 — source: https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65

- **`InstanceID` → `EntityId`.** Obsolete `InstanceID` APIs are compile errors. `int id = go.GetInstanceID();` becomes `EntityId id = go.GetEntityId();`. `EntityId` is **8 bytes** — never store it in an `int`, never round-trip it through `int.Parse`, never use `EntityId.GetHashCode()` as a dictionary key (use `EntityId` itself).
- **Obsolete `GameObject`/`Component` shortcut properties removed** (`.rigidbody2D`, `.collider2d`, `.renderer`, `.audio`, `.camera`, …, `AddComponent(string)`). Use `GetComponent<T>()` / `AddComponent<T>()`. Full table in `deprecated-apis.md`.
- **2D: `LowLevelPhysics2D` renamed to `PhysicsCore2D`**, namespace `UnityEngine.LowLevelPhysics2D` → `Unity.U2D.Physics`; the settings asset is now "Physics Core Settings 2D". Auto-migrated. The guide does not list any change to the classic **Physics 2D** module (`Rigidbody2D`, `Collider2D`) — an inference from absence, not a stated guarantee.
- **Editor.log is project-local by default:** `<Project>/Logs/Editor.log`. Use `Application.consoleLogPath` in scripts, or pass `-logFile` on the command line (this project's `commands.smoke` already does).
- **Built-In Render Pipeline deprecated** (supported through 6.7 LTS). This project is on URP.
- **Render Pipeline Core no longer depends on uGUI.** A project that got `com.unity.ugui` only transitively must add it (this project lists it directly in `Packages/manifest.json`).
- Legacy Render Graph compiler obsolete (custom SRPs only). Reflection Probe Shader Graph node deprecated.
- `ModelImporter`: obsolete APIs are now errors (table in `deprecated-apis.md`).
- `Entities.ForEach`, `Job.WithCode`, `IAspect` removed.
- BCL now ships `System.Text.Json`, `System.Collections.Immutable`, `System.Reflection.Metadata`, `System.Runtime.CompilerServices.Unsafe`; a user-supplied copy is dropped from the build.
- Android: min API 26, x86-64 removed, Gradle 9.1.0 / AGP 9.0.0. iOS `ReplayKit` obsolete. VR Module and Magic Leap removed.

## 6.5 → 6.6

**Last verified:** 2026-11-24 — source: https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66

- **`DEVELOPMENT_BUILD` and `UNITY_64` deprecated** (analyzer warnings `UAC0009` / `UAC0008`; errors under warnings-as-errors; fully removed in 6.8). Diagnostics now follow the per-platform **Managed Code Variant** Player setting (default **Release** = no diagnostic symbols, so a Development Build with defaults no longer includes SRP debug overlays etc.). Replacements: `UNITY_ENABLE_CHECKS` (checks/asserts), `UNITY_INCLUDE_INSTRUMENTATION` (profiling/logging), runtime `Debug.isDebugBuild` (anything that must not ship in a production Player), `IntPtr.Size` instead of `UNITY_64`. `BuildOptions.ForceEnableAssertions` is obsolete → `PlayerSettings.SetManagedCodeVariant(target, ManagedCodeVariant.Checked)`.
- **Dynamic batching removed.** Use SRP Batcher / GPU Resident Drawer / other draw-call methods.
- **UI Toolkit: UXML Factory/Traits removed** — use `[UxmlElement]` / `[UxmlAttribute]`. `DefaultEventSystem.LegacyInputProcessor` removed; delete calls to `UIToolkitInputConfiguration.SetRuntimeInputBackend`.
- **Core packages now part of the Editor:** Cinemachine 3 (Cinemachine 2 projects auto-upgrade to 3 — different API and data format), Performance Testing (`com.unity.test-framework.performance`). New in Unity 6.6 also lists Timeline and Animation Rigging as core packages.
- **YAML:** word wrapping disabled, "Reduce Version Control Noise" setting removed (`serializeInlineMappingsOnOneLine` setter is a no-op). Expect unrelated-looking YAML diffs; consider `AssetDatabase.ForceReserializeAssets`.
- **Render Pipeline Core no longer depends on Terrain.**
- Rendering Debugger legacy `DebugState*` / `DebugUIDrawer` APIs are compile errors — implement `ISerializedDebugDisplaySettings` and `DebugUI.Widget.Create()`.
- Surface shaders use the caching preprocessor. Progressive CPU Lightmapper deprecated.
- Android: Legacy/Round icons removed, GLES min 3.1 (`PlayerSettings.openGLRequireES31` deprecated), `-androidChainedSignalHandlerBehavior legacy` removed.
- **Mesh Read/Write** (source: https://docs.unity.com/en-us/engine/6000.6/manual/whats-new/unity66): meshes needed on the CPU by a Mesh Collider, Particle System Shape module or Terrain Detail must have **Read/Write** enabled, or the build fails.
