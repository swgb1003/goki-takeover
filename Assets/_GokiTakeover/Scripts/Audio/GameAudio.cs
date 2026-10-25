using System;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Phase 6 sound effects. Every clip is synthesised here at startup (tones, filtered noise), so the game
    /// ships no audio files and needs no licence checks. The component watches the game state each frame
    /// (pick-ups, deliveries, the resident, hazards, allies, the stage flow) and plays the matching sound;
    /// menus call <see cref="Ui"/>. Volume follows the master volume setting (AudioListener.volume).
    /// In the title scene only the menu sounds are used (no game references are set).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class GameAudio : MonoBehaviour
    {
        public enum Sfx
        {
            Click, Focus, Pickup, Deliver, Breed, Capture, Order, Discovered, Warning, Whoosh, SlipperHit,
            Hurt, AllyDeath, Spray, Fogger, Eat, Clear, Defeat, Siren,
            CatMeow, CatHiss
        }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private StageProgress progress;
        private const int Rate = 22050;
        private AudioClip[] clips;
        private AudioSource source, steps;
        private System.Random random = new System.Random(11);
        private static GameAudio current;

        // Last seen state, to play a sound once when it changes.
        private bool carried, swinging, sprayWarn, spraying, nestOpen;
        private int collected, born, captured, deaths, eaten, hp, orders;
        // One entry per member of the household (Stage 2 has three; earlier scenes one).
        private HumanAIController.HumanState[] humanStates;
        private bool[] swings;
        private int attackPhases;
        private PetCat pet;
        private PetCat.Phase petPhase;
        private PhaseTwoSession.RunState runState;
        private StageProgress.Phase phase;
        private SmokeFogger.Phase[] fogger;
        private bool primed;
        public int Played { get; private set; }
        public Sfx LastPlayed { get; private set; }

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, ColonyManager nest, StageProgress stage)
        { session = game; commander = swarm; colony = nest; progress = stage; }

        /// <summary>Menu click / focus sound from any screen (no-op where the scene has no GameAudio).</summary>
        public static void Ui(Sfx sfx = Sfx.Click) { if (current) current.Play(sfx, 0.7f); }

        private void Awake()
        {
            current = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.ignoreListenerPause = true;
            steps = gameObject.AddComponent<AudioSource>();
            steps.playOnAwake = false; steps.loop = true; steps.spatialBlend = 0; steps.volume = 0;
            clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
            for (int i = 0; i < clips.Length; i++) clips[i] = Build((Sfx)i);
            steps.clip = Skitter();
            if (session) for (int i = 0; i < session.HumanCount; i++) session.HumanAt(i).SlipperLanding += OnSlipperLanding;
            if (session) pet = FindFirstObjectByType<PetCat>();
        }
        private void OnDestroy()
        {
            if (current == this) current = null;
            if (session) for (int i = 0; i < session.HumanCount; i++) if (session.HumanAt(i)) session.HumanAt(i).SlipperLanding -= OnSlipperLanding;
            if (clips != null) foreach (var c in clips) if (c) Destroy(c);
            if (steps && steps.clip) Destroy(steps.clip);
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            source.PlayOneShot(clips[(int)sfx], volume);
            Played++;
            LastPlayed = sfx;
        }

        private void OnSlipperLanding(Vector3 point, bool crushed)
        {
            float near = 1 - Mathf.Clamp01(Vector3.Distance(session.Player.position, point) / 9f);
            Play(Sfx.SlipperHit, 0.35f + 0.65f * near);
        }

        // ---------------- Watching the game ----------------
        private void Update()
        {
            if (!session) return;
            var reaction = colony ? colony.Reaction : null;
            if (!primed) { Remember(); primed = true; return; }

            if (session.State != runState)
            {
                if (session.State == PhaseTwoSession.RunState.Cleared) Play(Sfx.Clear);
                else if (session.State == PhaseTwoSession.RunState.Dead) Play(Sfx.Defeat);
            }
            if (session.Interactor.Carried && !carried) Play(Sfx.Pickup, 0.8f);
            if (session.Hp < hp) Play(Sfx.Hurt);
            if (colony)
            {
                if (colony.TotalCollected > collected) Play(Sfx.Deliver);
                if (colony.Born > born) Play(Sfx.Breed);
                if (colony.NestMenuOpen != nestOpen) Play(Sfx.Click, 0.6f);
            }
            if (commander)
            {
                if (commander.AllyDeaths > deaths) Play(Sfx.AllyDeath, 0.8f);
                if (commander.OrdersIssued > orders) Play(Sfx.Order, 0.7f);
            }
            if (progress)
            {
                if (progress.CapturedZones > captured) Play(Sfx.Capture);
                if (progress.CorpsesEaten > eaten) Play(Sfx.Eat);
                if (progress.Current == StageProgress.Phase.FinalDefence && phase != StageProgress.Phase.FinalDefence) Play(Sfx.Siren);
                var foggers = progress.Foggers;
                for (int i = 0; i < foggers.Length && i < fogger.Length; i++)
                {
                    var f = foggers[i].Active ? foggers[i].Current : SmokeFogger.Phase.Idle;
                    if (f != fogger[i]) { if (f == SmokeFogger.Phase.Warning) Play(Sfx.Warning, 0.8f); else if (f == SmokeFogger.Phase.Smoking) Play(Sfx.Fogger); }
                    fogger[i] = f;
                }
            }
            for (int i = 0; i < session.HumanCount; i++)
            {
                var human = session.HumanAt(i);
                if (!session.HumanPresent(i)) continue;
                if (human.State != humanStates[i])
                {
                    if (human.State == HumanAIController.HumanState.Chase && humanStates[i] == HumanAIController.HumanState.Routine) Play(Sfx.Discovered);
                    if (human.State == HumanAIController.HumanState.Attack) Play(Sfx.Warning, 0.7f);
                }
                // The whoosh starts with the downswing, a little before the sole lands.
                bool swing = human.State == HumanAIController.HumanState.Attack && !human.SlipperLanded && human.AttackProgress > 0.6f;
                if (swing && !swings[i]) Play(Sfx.Whoosh, 0.8f);
                swings[i] = swing;
            }
            if (progress) HouseholdSounds();
            if (pet)
            {
                if (pet.Current != petPhase)
                {
                    if (pet.Current == PetCat.Phase.Stalk && pet.TargetIsPlayer) Play(Sfx.CatMeow, 0.8f);
                    else if (pet.Current == PetCat.Phase.Crouch) Play(Sfx.Warning, 0.7f);
                    else if (pet.Current == PetCat.Phase.Leap) Play(Sfx.CatHiss, 0.8f);
                }
                petPhase = pet.Current;
            }
            if (reaction)
            {
                var spray = reaction.Spray;
                if (spray.Telegraphing && !sprayWarn) Play(Sfx.Warning, 0.8f);
                if (spray.Spraying && !spraying) Play(Sfx.Spray);
                sprayWarn = spray.Telegraphing; spraying = spray.Spraying;
            }
            Remember();
            Footsteps();
        }

        // Stage 2: the vacuum's warning and suction, the net's warning, swing and catch. Phases packed 2 bits per attack.
        private int AttackPhases()
        {
            int packed = 0, shift = 0;
            foreach (var a in progress.Attacks)
            {
                int phase = a is VacuumAttack v ? (int)v.Current : a is BugNetAttack n ? (int)n.Current : 0;
                packed |= phase << shift; shift += 2;
            }
            return packed;
        }
        private void HouseholdSounds()
        {
            int now = AttackPhases(), shift = 0;
            foreach (var a in progress.Attacks)
            {
                int before = (attackPhases >> shift) & 3, after = (now >> shift) & 3; shift += 2;
                if (before == after) continue;
                if (a is VacuumAttack)
                {
                    if (after == (int)VacuumAttack.Phase.Aim) Play(Sfx.Warning, 0.8f);
                    else if (after == (int)VacuumAttack.Phase.Suck) Play(Sfx.Fogger, 1f);
                }
                else if (a is BugNetAttack net)
                {
                    if (after == (int)BugNetAttack.Phase.Windup) Play(Sfx.Warning, 0.8f);
                    else if (before == (int)BugNetAttack.Phase.Windup) { Play(Sfx.Whoosh, 0.9f); if (net.PlayerCaught) Play(Sfx.SlipperHit, 0.7f); }
                }
            }
        }

        private void Remember()
        {
            runState = session.State;
            carried = session.Interactor.Carried;
            hp = session.Hp;
            if (humanStates == null || humanStates.Length != session.HumanCount) { humanStates = new HumanAIController.HumanState[session.HumanCount]; swings = new bool[session.HumanCount]; }
            for (int i = 0; i < humanStates.Length; i++) humanStates[i] = session.HumanAt(i).State;
            if (progress) attackPhases = AttackPhases();
            if (colony) { collected = colony.TotalCollected; born = colony.Born; nestOpen = colony.NestMenuOpen; }
            if (commander) { deaths = commander.AllyDeaths; orders = commander.OrdersIssued; }
            if (progress)
            {
                captured = progress.CapturedZones; eaten = progress.CorpsesEaten; phase = progress.Current;
                if (fogger == null) fogger = new SmokeFogger.Phase[progress.Foggers.Length];
            }
            else if (fogger == null) fogger = new SmokeFogger.Phase[0];
        }

        // A quiet skitter while the player moves; louder and faster when running.
        private void Footsteps()
        {
            var motor = session.Motor;
            float speed = session.IsRunning ? Mathf.Clamp01(motor.CurrentSpeed / 3.3f) : 0;
            float target = speed > 0.05f ? 0.12f + 0.16f * speed : 0;
            steps.volume = Mathf.MoveTowards(steps.volume, target, Time.unscaledDeltaTime * 1.5f);
            steps.pitch = 0.85f + 0.45f * speed;
            if (steps.volume > 0 && !steps.isPlaying) steps.Play();
            else if (steps.volume <= 0 && steps.isPlaying) steps.Pause();
        }

        // ---------------- Synthesis ----------------
        private AudioClip Build(Sfx sfx)
        {
            float[] s;
            switch (sfx)
            {
                case Sfx.Click: s = Tone(0.035f, 1250, 1250, Wave.Sine, 0.5f); Add(s, Noise(0.012f, 0.25f, 0.6f), 0); break;
                case Sfx.Focus: s = Tone(0.03f, 820, 820, Wave.Sine, 0.35f); break;
                case Sfx.Pickup: s = Tone(0.09f, 480, 920, Wave.Triangle, 0.5f); break;
                case Sfx.Deliver: s = Notes(new[] { 660f, 990f }, 0.075f, Wave.Triangle, 0.5f); break;
                case Sfx.Breed: s = Notes(new[] { 523f, 659f, 784f }, 0.08f, Wave.Triangle, 0.5f); break;
                case Sfx.Capture: s = Notes(new[] { 392f, 523f, 659f, 784f }, 0.085f, Wave.Square, 0.28f, 0.25f); break;
                case Sfx.Order: s = Notes(new[] { 900f, 1200f }, 0.04f, Wave.Sine, 0.4f); break;
                case Sfx.Discovered: s = Notes(new[] { 880f, 660f }, 0.12f, Wave.Saw, 0.3f); break;
                case Sfx.Warning: s = Notes(new[] { 1000f, 0f, 1000f }, 0.06f, Wave.Square, 0.22f); break;
                case Sfx.Whoosh: s = Filtered(Noise(0.22f, 0.7f, 0), 0.08f, 0.35f, true); break;
                case Sfx.SlipperHit: s = Tone(0.16f, 140, 55, Wave.Sine, 0.9f); Add(s, Filtered(Noise(0.07f, 0.8f, 0), 0.2f, 0.2f, false), 0); break;
                case Sfx.Hurt: s = Tone(0.2f, 230, 90, Wave.Saw, 0.45f); Add(s, Noise(0.12f, 0.45f, 0), 0); break;
                case Sfx.AllyDeath: s = Filtered(Noise(0.12f, 0.6f, 0), 0.12f, 0.12f, false); Add(s, Tone(0.1f, 320, 160, Wave.Sine, 0.3f), 0); break;
                case Sfx.Spray: s = Filtered(Noise(1.2f, 0.45f, 0.15f), 0.6f, 0.6f, true); break;
                case Sfx.Fogger: s = Filtered(Noise(1.8f, 0.4f, 0.3f), 0.25f, 0.25f, true); Add(s, Tone(1.8f, 70, 60, Wave.Sine, 0.15f), 0); break;
                case Sfx.Eat:
                    s = new float[(int)(Rate * 0.3f)];
                    for (int k = 0; k < 3; k++) Add(s, Filtered(Noise(0.05f, 0.6f, 0), 0.3f, 0.3f, false), (int)(Rate * 0.09f * k));
                    break;
                case Sfx.Clear: s = Notes(new[] { 523f, 659f, 784f, 1047f, 1047f }, 0.13f, Wave.Triangle, 0.5f, 0.6f); break;
                case Sfx.CatMeow: s = Tone(0.16f, 560, 940, Wave.Triangle, 0.38f); Add(s, Tone(0.34f, 940, 470, Wave.Triangle, 0.38f), (int)(Rate * 0.14f)); break;
                case Sfx.CatHiss: s = Filtered(Noise(0.45f, 0.5f, 0.1f), 0.5f, 0.5f, true); break;
                case Sfx.Defeat: s = Notes(new[] { 392f, 330f, 262f, 196f }, 0.2f, Wave.Triangle, 0.45f, 0.4f); break;
                default:   // Siren
                    s = new float[(int)(Rate * 1.4f)];
                    for (int k = 0; k < 2; k++) Add(s, Tone(0.35f, 600, 900, Wave.Saw, 0.22f), (int)(Rate * 0.7f * k));
                    for (int k = 0; k < 2; k++) Add(s, Tone(0.35f, 900, 600, Wave.Saw, 0.22f), (int)(Rate * (0.35f + 0.7f * k)));
                    break;
            }
            var clip = AudioClip.Create("Sfx_" + sfx, s.Length, 1, Rate, false);
            clip.SetData(s, 0);
            return clip;
        }

        private AudioClip Skitter()
        {
            // One second of tiny irregular ticks (six legs on tiles), looped.
            var s = new float[Rate];
            for (int t = 0; t < 26; t++)
            {
                int at = (int)((t + (float)random.NextDouble() * 0.6f) / 26f * Rate);
                var tick = Filtered(Noise(0.008f, 0.35f + 0.25f * (float)random.NextDouble(), 0), 0.5f, 0.5f, true);
                Add(s, tick, Mathf.Min(at, s.Length - tick.Length));
            }
            var clip = AudioClip.Create("Sfx_Skitter", s.Length, 1, Rate, false);
            clip.SetData(s, 0);
            return clip;
        }

        private enum Wave { Sine, Triangle, Square, Saw }
        private static float Osc(Wave wave, float phase)
        {
            float t = phase - Mathf.Floor(phase);
            switch (wave)
            {
                case Wave.Triangle: return 1 - 4 * Mathf.Abs(t - 0.5f);
                case Wave.Square: return t < 0.5f ? 1 : -1;
                case Wave.Saw: return 2 * t - 1;
                default: return Mathf.Sin(t * Mathf.PI * 2);
            }
        }
        // A pitch sweep with a short attack and an exponential-looking release.
        private static float[] Tone(float seconds, float from, float to, Wave wave, float volume)
        {
            int n = Mathf.Max(1, (int)(Rate * seconds));
            var s = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += Mathf.Lerp(from, to, t) / Rate;
                float env = Mathf.Min(1, i / (Rate * 0.004f)) * (1 - t) * (1 - t);
                s[i] = Osc(wave, phase) * env * volume;
            }
            return s;
        }
        private static float[] Notes(float[] freqs, float each, Wave wave, float volume, float tail = 0)
        {
            int step = (int)(Rate * each);
            var s = new float[step * freqs.Length + (int)(Rate * tail)];
            for (int k = 0; k < freqs.Length; k++)
            {
                if (freqs[k] <= 0) continue;
                bool last = k == freqs.Length - 1;
                Add(s, Tone(each * 1.6f + (last ? tail : 0), freqs[k], freqs[k], wave, volume), step * k);
            }
            return s;
        }
        private float[] Noise(float seconds, float volume, float sustain)
        {
            int n = Mathf.Max(1, (int)(Rate * seconds));
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Min(1, i / (Rate * 0.003f)) * Mathf.Lerp((1 - t) * (1 - t), 1 - t * t, sustain);
                s[i] = ((float)random.NextDouble() * 2 - 1) * env * volume;
            }
            return s;
        }
        // One-pole filter sweeping its coefficient from a to b: low-pass, or high-pass (hiss) when high is set.
        private static float[] Filtered(float[] s, float a, float b, bool high)
        {
            float low = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float k = Mathf.Lerp(a, b, i / (float)s.Length);
                low += (s[i] - low) * k;
                s[i] = high ? s[i] - low : low;
            }
            return s;
        }
        private static void Add(float[] into, float[] what, int offset)
        {
            for (int i = 0; i < what.Length && offset + i < into.Length; i++) into[offset + i] = Mathf.Clamp(into[offset + i] + what[i], -1, 1);
        }
    }
}
