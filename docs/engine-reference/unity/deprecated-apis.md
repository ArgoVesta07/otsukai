# Unity 6.3 LTS — Deprecated APIs

**Last verified:** 2026-02-13 (6.3 LTS content); 2026-11-24 (6.4–6.6 sections appended at the end)

Quick lookup table for deprecated APIs and their replacements.
Format: **Don't use X** → **Use Y instead**

---

## Input

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Input.GetKey()` | `Keyboard.current[Key.X].isPressed` | New Input System |
| `Input.GetKeyDown()` | `Keyboard.current[Key.X].wasPressedThisFrame` | New Input System |
| `Input.GetMouseButton()` | `Mouse.current.leftButton.isPressed` | New Input System |
| `Input.GetAxis()` | `InputAction` callbacks | New Input System |
| `Input.mousePosition` | `Mouse.current.position.ReadValue()` | New Input System |

**Migration:** Install `com.unity.inputsystem` package.

---

## UI

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Canvas` (UGUI) | `UIDocument` (UI Toolkit) | UI Toolkit is now production-ready |
| `Text` component | `TextMeshPro` or UI Toolkit `Label` | Better rendering, fewer draw calls |
| `Image` component | UI Toolkit `VisualElement` with background | More flexible styling |

**Migration:** UGUI still works, but UI Toolkit is recommended for new projects.

---

## DOTS/Entities

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `ComponentSystem` | `ISystem` (unmanaged) | Entities 1.0+ complete rewrite |
| `JobComponentSystem` | `ISystem` with `IJobEntity` | Burst-compatible |
| `GameObjectEntity` | Pure ECS workflow | No GameObject conversion |
| `EntityManager.CreateEntity()` (old signature) | `EntityManager.CreateEntity(EntityArchetype)` | Explicit archetype |
| `ComponentDataFromEntity<T>` | `ComponentLookup<T>` | Entities 1.0+ rename |

**Migration:** See Entities package migration guide. Major refactor required.

---

## Rendering

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `CommandBuffer.DrawMesh()` | RenderGraph API | URP/HDRP render passes |
| `OnPreRender()` / `OnPostRender()` | `RenderPipelineManager` callbacks | SRP compatibility |
| `Camera.SetReplacementShader()` | Custom render pass | Not supported in SRP |

---

## Physics

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Physics.RaycastAll()` | `Physics.RaycastNonAlloc()` | Avoid GC allocations |
| `Rigidbody.velocity` (direct write) | `Rigidbody.AddForce()` | Better physics stability |

---

## Asset Loading

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Resources.Load()` | Addressables | Better memory control, async loading |
| Synchronous asset loading | `Addressables.LoadAssetAsync()` | Non-blocking |

---

## Animation

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| Legacy Animation component | Animator Controller | Mecanim system |
| `Animation.Play()` | `Animator.Play()` | State machine control |

---

## Particles

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| Legacy Particle System | Visual Effect Graph | GPU-accelerated, more performant |

---

## Scripting

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `WWW` class | `UnityWebRequest` | Modern async networking |
| `Application.LoadLevel()` | `SceneManager.LoadScene()` | Scene management |

---

## Platform-Specific

### WebGL
| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| WebGL 1.0 | WebGL 2.0 or WebGPU | Unity 6+ defaults to WebGPU |

---

## Quick Migration Patterns

### Input Example
```csharp
// ❌ Deprecated
if (Input.GetKeyDown(KeyCode.Space)) {
    Jump();
}

// ✅ New Input System
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) {
    Jump();
}
```

### Asset Loading Example
```csharp
// ❌ Deprecated
var prefab = Resources.Load<GameObject>("Enemies/Goblin");

// ✅ Addressables
var handle = Addressables.LoadAssetAsync<GameObject>("Enemies/Goblin");
await handle.Task;
var prefab = handle.Result;
```

### UI Example
```csharp
// ❌ Deprecated (UGUI)
GetComponent<Text>().text = "Score: 100";

// ✅ TextMeshPro
GetComponent<TextMeshProUGUI>().text = "Score: 100";

// ✅ UI Toolkit
rootVisualElement.Q<Label>("score-label").text = "Score: 100";
```

---

**Sources:**
- https://docs.unity3d.com/6000.0/Documentation/Manual/deprecated-features.html
- https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Migration.html

---

## Unity 6.4 – 6.6

**Last verified:** 2026-11-24 — sources:
https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity64,
https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65,
https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66

### Object identity (removed 6.5)

| Don't use | Use instead |
|-----------|-------------|
| `GetInstanceID()` / `int instanceID` | `GetEntityId()` / `EntityId` (8 bytes — keep the type, never downcast to `int`) |
| `EntityId.GetHashCode()` as a dictionary key | `EntityId` as the key |

### GameObject / Component shortcuts (removed 6.5)

| Don't use | Use instead |
|-----------|-------------|
| `GameObject.AddComponent(string)` | `AddComponent<T>()` |
| `.rigidbody2D` / `.collider2d` | `GetComponent<Rigidbody2D>()` / `GetComponent<Collider2D>()` |
| `.rigidbody` / `.collider` / `.hingeJoint` / `.constantForce` | `GetComponent<Rigidbody>()` / `<Collider>` / `<HingeJoint>` / `<ConstantForce>` |
| `.renderer` / `.camera` / `.light` | `GetComponent<Renderer>()` / `<Camera>` / `<Light>` |
| `.audio` | `GetComponent<AudioSource>()` |
| `.animation` / `.particleSystem` / `.networkView` | `GetComponent<Animation>()` / `<ParticleSystem>` / `<NetworkView>` |
| `GameObject.SampleAnimation(clip, t)` | `AnimationClip.SampleAnimation(gameObject, t)` |
| `GameObject.PlayAnimation(obj)` / `StopAnimation()` | `animation.Play()` / `animation.Stop()` |

### 2D physics low-level API (renamed 6.5)

| Don't use | Use instead |
|-----------|-------------|
| `using UnityEngine.LowLevelPhysics2D;` | `using Unity.U2D.Physics;` (Physics Core 2D) |

### ModelImporter (errors since 6.5)

| Don't use | Use instead |
|-----------|-------------|
| `isFileScaleUsed` | `useFileScale` |
| `normalImportMode` | `importNormals` |
| `optimizeMesh` | `optimizeMeshPolygons` and/or `optimizeMeshVertices` |
| `resampleRotations` | `resampleCurves` |
| `splitTangentsAcrossSeams` | `tangentImportMode` |
| `tangentImportMode` (old meaning) | `importTangents` |

### Entities (removed 6.5)

| Don't use | Use instead |
|-----------|-------------|
| `Entities.ForEach` | `IJobEntity` and `SystemAPI.Query` |
| `Job.WithCode` | `IJob` |
| `IAspect` | Component and `EntityQuery` APIs |

### Scripting symbols (deprecated 6.6, removed 6.8)

| Don't use | Use instead |
|-----------|-------------|
| `#if DEVELOPMENT_BUILD` (checks/asserts) | `#if UNITY_ENABLE_CHECKS` |
| `[Conditional("DEVELOPMENT_BUILD")]` (logging/profiling) | `[Conditional("UNITY_INCLUDE_INSTRUMENTATION")]` |
| `#if DEVELOPMENT_BUILD` around code that must never ship | runtime `if (Debug.isDebugBuild)` |
| `#if UNITY_64` | runtime `IntPtr.Size == 8` |
| `BuildOptions.ForceEnableAssertions` | `PlayerSettings.SetManagedCodeVariant(target, ManagedCodeVariant.Checked)` |

### UI Toolkit (removed 6.6)

| Don't use | Use instead |
|-----------|-------------|
| `UxmlFactory<T>` / `UxmlTraits` | `[UxmlElement]` on the class, `[UxmlAttribute]` on properties |
| `UIToolkitInputConfiguration.SetRuntimeInputBackend(...)` | delete the call (no replacement needed) |

### Rendering

| Don't use | Use instead |
|-----------|-------------|
| URP Compatibility Mode / `URP_COMPATIBILITY_MODE` (removed 6.4) | Render Graph for custom passes |
| Dynamic batching (removed 6.6) | SRP Batcher / GPU Resident Drawer |
| `IVolume` (removed 6.4) | `VolumeGizmoDrawer` |
| `LightShadowCasterMode.NonLightmappedOnly` / `.Everything` | `.ShadowMask` / `.DistanceShadowMask` |
| Reflection Probe Shader Graph node (deprecated 6.5) | Custom Function node: `SHADERGRAPH_REFLECTION_PROBE(ViewDir, Normal, LOD)` |
| `DebugState`, `DebugState<T>`, legacy `DebugUIDrawer` (errors 6.6) | `ISerializedDebugDisplaySettings`, `DebugUI.Widget.Create()` |
| `CoreBuildData.developmentBuild` | `CoreBuildData.useDiagnosticChecks` |
