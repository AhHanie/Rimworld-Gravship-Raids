using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class EnemyGravshipEffects
    {
        private const float SkyfallerAltitudeYOffset = 0.07317074f;

        private const float MetaOverlaysAltitudeYOffset = 0.03658537f;

        private static readonly int ShaderPropertyGravshipHeight = Shader.PropertyToID("_GravshipHeight");

        private static readonly int ShaderPropertyIsTakeoff = Shader.PropertyToID("_IsTakeoff");

        private readonly Map map;

        private readonly Rot4 shipRotation;

        private readonly IntVec3 launchDirection;

        private readonly Material matDownwash;

        private readonly Material matDistortion;

        private readonly Material matLensFlare;

        private readonly MaterialPropertyBlock flareBlock = new MaterialPropertyBlock();

        private readonly MaterialPropertyBlock thrusterFlameBlock = new MaterialPropertyBlock();

        private readonly Dictionary<Thing, EventQueue> exhaustTimersByThing = new Dictionary<Thing, EventQueue>();

        private readonly Dictionary<int, EventQueue> exhaustTimersByIndex = new Dictionary<int, EventQueue>();

        public EnemyGravshipEffects(Map map, Rot4 shipRotation, IntVec3 launchDirection)
        {
            this.map = map;
            this.shipRotation = shipRotation;
            this.launchDirection = launchDirection;
            matDownwash = MatLoader.LoadMat("Map/Gravship/GravshipDownwash");
            matDistortion = MatLoader.LoadMat("Map/Gravship/GravshipDistortion");
            matLensFlare = MatLoader.LoadMat("Map/Gravship/GravshipLensFlare");
        }

        public static IntVec3 MajorityLaunchDirection(IEnumerable<Rot4> thrusterRotations)
        {
            Dictionary<IntVec3, int> counts = new Dictionary<IntVec3, int>();
            IntVec3 best = IntVec3.Zero;
            int bestCount = 0;
            foreach (Rot4 rot in thrusterRotations)
            {
                IntVec3 vec = rot.AsIntVec3;
                counts.TryGetValue(vec, out int count);
                count++;
                counts[vec] = count;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = vec;
                }
            }
            return best;
        }

        public void Tick(float deltaTime)
        {
            foreach (EventQueue timer in exhaustTimersByThing.Values)
            {
                timer.Push(deltaTime);
            }
            foreach (EventQueue timer in exhaustTimersByIndex.Values)
            {
                timer.Push(deltaTime);
            }
        }

        public void DrawThrusters(IReadOnlyList<Thing> thrusters, Vector3 worldOffset = default)
        {
            for (int i = 0; i < thrusters.Count; i++)
            {
                Thing thruster = thrusters[i];
                CompProperties_GravshipThruster props = thruster?.TryGetComp<CompGravshipThruster>()?.Props;
                if (props == null)
                {
                    continue;
                }
                EventQueue timer = GetOrAddTimer(exhaustTimersByThing, thruster, props);
                DrawOneThruster(props, thruster.Position, thruster.Rotation, thruster.def, worldOffset, timer);
            }
        }
        public void DrawThrusters(IReadOnlyList<(PrefabThingData data, IntVec3 cell, Rot4 rot)> pieces, Vector3 worldOffset = default)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                (PrefabThingData data, IntVec3 cell, Rot4 rot) = pieces[i];
                CompProperties_GravshipThruster props = data.def?.GetCompProperties<CompProperties_GravshipThruster>();
                if (props == null)
                {
                    continue;
                }
                EventQueue timer = GetOrAddTimer(exhaustTimersByIndex, i, props);
                DrawOneThruster(props, cell, rot, data.def, worldOffset, timer);
            }
        }
        private void DrawOneThruster(CompProperties_GravshipThruster props, IntVec3 cell, Rot4 rot, ThingDef def, Vector3 worldOffset, EventQueue exhaustTimer)
        {
            if (rot.AsIntVec3 == -launchDirection)
            {
                return;
            }

            float flameLen = def.size.x * props.flameSize;
            Vector3 flameOffset = rot.AsQuat * props.flameOffsetsPerDirection[rot.AsInt];
            Vector3 basePos = GenThing.TrueCenter(cell, rot, def.size, 0f)
                - rot.AsIntVec3.ToVector3() * ((float)def.size.z * 0.5f + flameLen * 0.5f)
                + flameOffset;
            Vector3 flamePos = (basePos + worldOffset).SetToAltitude(AltitudeLayer.Skyfaller).WithYOffset(SkyfallerAltitudeYOffset);

            MaterialRequest req = new MaterialRequest(props.FlameShaderType.Shader)
            {
                renderQueue = 3201
            };
            Material flameMat = MaterialPool.MatFrom(req);
            thrusterFlameBlock.Clear();
            foreach (ShaderParameter parameter in props.flameShaderParameters)
            {
                parameter.Apply(thrusterFlameBlock);
            }
            GenDraw.DrawQuad(flameMat, flamePos, rot.AsQuat, flameLen, thrusterFlameBlock);

            Vector3 viewportPos = Find.Camera.WorldToViewportPoint(flamePos);
            flareBlock.Clear();
            flareBlock.SetVector(ShaderPropertyIDs.DrawPos, viewportPos);
            DrawFullscreenLayer(matLensFlare, Find.Camera.transform.position.SetToAltitude(AltitudeLayer.MetaOverlays).WithYOffset(MetaOverlaysAltitudeYOffset), flareBlock);

            if (props.exhaustSettings != null && props.exhaustSettings.enabled && exhaustTimer != null)
            {
                exhaustTimer.Push(0f);
                while (exhaustTimer.Pop())
                {
                    EmitExhaust(props.exhaustSettings, flamePos, rot.AsQuat);
                }
            }
        }

        public void DrawDownwash(Vector3 groundCenter, float intensity01)
        {
            if (map != null && map.Biome != null && map.Biome.inVacuum)
            {
                return;
            }
            matDownwash.SetFloat(ShaderPropertyIDs.Progress, intensity01);
            matDownwash.SetFloat(ShaderPropertyGravshipHeight, 1f);
            matDownwash.SetVector(ShaderPropertyIDs.DrawPos, Find.Camera.WorldToViewportPoint(groundCenter));
            matDownwash.SetFloat(ShaderPropertyIsTakeoff, 0f);
            DrawFullscreenLayer(matDownwash, Find.Camera.transform.position.SetToAltitude(AltitudeLayer.Gas).WithYOffset(MetaOverlaysAltitudeYOffset));
        }

        public void DrawDistortion(Vector3 shipCenter, float intensity01, bool isTakeoff)
        {
            matDistortion.SetFloat(ShaderPropertyIDs.Progress, intensity01);
            matDistortion.SetFloat(ShaderPropertyGravshipHeight, intensity01);
            matDistortion.SetVector(ShaderPropertyIDs.DrawPos, Find.Camera.WorldToViewportPoint(shipCenter));
            matDistortion.SetFloat(ShaderPropertyIsTakeoff, isTakeoff ? 1f : 0f);
            DrawFullscreenLayer(matDistortion, Find.Camera.transform.position.SetToAltitude(AltitudeLayer.Weather).WithYOffset(SkyfallerAltitudeYOffset));
        }

        public void End()
        {
            exhaustTimersByThing.Clear();
            exhaustTimersByIndex.Clear();
        }

        private static EventQueue GetOrAddTimer<TKey>(Dictionary<TKey, EventQueue> timers, TKey key, CompProperties_GravshipThruster props)
        {
            if (props.exhaustSettings == null || !props.exhaustSettings.enabled)
            {
                return null;
            }
            if (!timers.TryGetValue(key, out EventQueue timer))
            {
                float emissionsPerSecond = Mathf.Max(props.exhaustSettings.emissionsPerSecond, 0.01f);
                timer = new EventQueue(1f / emissionsPerSecond);
                timers[key] = timer;
            }
            return timer;
        }

        private void EmitExhaust(CompProperties_GravshipThruster.ExhaustSettings settings, Vector3 position, Quaternion thrusterRotation)
        {
            if (map == null)
            {
                return;
            }
            Quaternion rotation = Quaternion.identity;
            if (settings.inheritThrusterRotation)
            {
                rotation = thrusterRotation * rotation;
            }
            if (settings.inheritGravshipRotation)
            {
                rotation = shipRotation.AsQuat * rotation;
            }
            map.flecks.CreateFleck(new FleckCreationData
            {
                def = settings.ExhaustFleckDef,
                spawnPosition = position + rotation * settings.spawnOffset + Random.insideUnitSphere.WithY(0f).normalized * settings.spawnRadiusRange.RandomInRange,
                scale = settings.scaleRange.RandomInRange,
                velocity = rotation * Quaternion.Euler(0f, settings.velocityRotationRange.RandomInRange, 0f) * (settings.velocity * settings.velocityMultiplierRange.RandomInRange),
                rotationRate = settings.rotationOverTimeRange.RandomInRange,
                ageTicksOverride = -1
            });
        }
        private static void DrawFullscreenLayer(Material mat, Vector3 position)
        {
            DrawFullscreenLayer(mat, position, null);
        }

        private static void DrawFullscreenLayer(Material mat, Vector3 position, MaterialPropertyBlock props)
        {
            float size = Find.Camera.orthographicSize * 2f;
            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(size * Find.Camera.aspect, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, props);
        }
    }
}
