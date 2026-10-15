using System.Collections.Generic;
using System.Threading;
using Coffee.UIExtensions;
using Cysharp.Threading.Tasks;
using Gwent.UI;
using UnityEngine;

namespace Gwent.Battle
{
    // Particle effects on the effects layer (CardMotion.Layer), drawn in the canvas by ParticleEffectForUGUI: a host at a
    // point of the layer holds particle systems made here, and particles are emitted where they belong. One particle unit
    // is one layout unit, as for the rest of the layer (the host keeps UIParticle's Transform scaling at scale 1, and no
    // particle's size is capped to a share of the view). The art is Resources/W3/Particles (Gwent > UI > Regenerate
    // Particle Art): "Spark", "Smoke" (blended over), "Blaze" (smoke added on) and "Fire" (added on). In game time: the
    // pause menu stops them.
    internal sealed class ParticleFx
    {
        private readonly UIParticle host;
        private readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        private ParticleFx(UIParticle host)
        {
            this.host = host;
        }

        // A host centred at a point of the layer (particles are placed from there)
        public static ParticleFx At(string name, Vector2 center)
        {
            RectTransform rect = UIKit.Rect(name, CardMotion.Layer);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = Vector2.zero;
            UIParticle host = rect.gameObject.AddComponent<UIParticle>();
            host.scale = 1;
            host.raycastTarget = false;
            return new ParticleFx(host);
        }

        // A system drawn with one of the art's materials, over the ones added before it. Its particles fall by gravity
        // (layout units per second squared; negative rises), go through the colours over their life (times the colour
        // each is born with) and the sizes (a factor of their size at birth), spin up to spin radians a second either way,
        // drift in a turbulence of that strength (layout units a second) and, with frames, show one of the art's 2 x 2.
        public ParticleSystem Add(string material, Gradient colors, AnimationCurve sizes, float gravity = 0, float spin = 0,
            float turbulence = 0, bool frames = false)
        {
            GameObject go = new GameObject(material);
            go.transform.SetParent(host.transform, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1;
            main.startSpeed = 0;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = gravity / -Physics.gravity.y;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false; // emitted by hand, where they belong
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = colors;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, sizes);
            if (spin > 0)
            {
                ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-spin, spin);
            }
            if (turbulence > 0)
            {
                ParticleSystem.NoiseModule noise = system.noise;
                noise.enabled = true;
                noise.strength = turbulence;
                noise.frequency = 0.012f; // swirls about 80 layout units across
                noise.scrollSpeed = 0.6f;
                noise.damping = false;
                noise.quality = ParticleSystemNoiseQuality.Medium;
            }
            if (frames)
            {
                ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 0.999f); // one of the four, kept
            }

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = Material(material);
            renderer.minParticleSize = 0;
            renderer.maxParticleSize = 100;

            systems.Add(system);
            host.RefreshParticles();
            system.Play();
            return system;
        }

        // One particle at a point (from the host), moving (layout units a second), its size, life, colour and angle (in
        // degrees) at birth
        public static void Emit(ParticleSystem system, Vector2 at, Vector2 velocity, float size, float life, Color color, float angle = 0)
        {
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = at,
                velocity = velocity,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = angle,
                applyShapeToPosition = false,
            };
            system.Emit(particle, 1);
        }

        // Whether a particle is still alive
        public bool Alive
        {
            get
            {
                foreach (ParticleSystem system in systems)
                    if (system != null && system.particleCount > 0)
                        return true;
                return false;
            }
        }

        // The host goes once its last particle has died
        public async UniTask Finish(CancellationToken ct)
        {
            try
            {
                while (host != null && Alive)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            finally
            {
                Destroy();
            }
        }

        public void Destroy()
        {
            if (host != null)
                Object.Destroy(host.gameObject);
        }

        private static Material Material(string name)
        {
            Material material;
            if (!materials.TryGetValue(name, out material) || material == null)
                materials[name] = material = Resources.Load<Material>("W3/Particles/" + name);
            return material;
        }

        //----------------------------------------Curves----------------------------------------//
        // Colours over a particle's life, each at its time (0 to 1)
        public static Gradient Colors(params (float time, Color color)[] keys)
        {
            GradientColorKey[] colors = new GradientColorKey[keys.Length];
            GradientAlphaKey[] alphas = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colors[i] = new GradientColorKey(keys[i].color, keys[i].time);
                alphas[i] = new GradientAlphaKey(keys[i].color.a, keys[i].time);
            }
            Gradient gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        // Sizes over a particle's life (a factor of its size at birth), each at its time (0 to 1)
        public static AnimationCurve Sizes(params (float time, float size)[] keys)
        {
            AnimationCurve curve = new AnimationCurve();
            foreach ((float time, float size) in keys)
                curve.AddKey(time, size);
            return curve;
        }
    }
}
