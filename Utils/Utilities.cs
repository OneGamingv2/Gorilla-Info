using ExitGames.Client.Photon;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Checker;
using UnityEngine;

public class CheckerUtilities
{
    private struct MotionSample
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Time;
        public float DistanceToLocal;
        public bool HasDistanceToLocal;
    }

    private static readonly List<string> _detectedModsBuffer = new List<string>(32);
    private static readonly HashSet<string> _detectedModsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<VRRig, MotionSample> _motionSamples = new Dictionary<VRRig, MotionSample>(24);
    private const int LowFpsThreshold = 25;
    private const float SuspiciousSpeedThreshold = 7.5f;
    private const float SpeedBoostThreshold = 11f;
    private const float ExtremeSpeedThreshold = 16f;
    private const float FlyVerticalVelocityThreshold = 6f;
    private const float FlyBoostVerticalVelocityThreshold = 10f;
    private const float TeleportDistanceThreshold = 6f;
    private const float TeleportSampleMaxInterval = 0.25f;
    private const float SuspiciousAccelerationThreshold = 38f;
    private const float PullApproachSpeedThreshold = 10f;
    private const float PullDistanceDeltaThreshold = 2f;
    private const float FlingVelocityThreshold = 18f;
    private const float FlingAccelerationThreshold = 65f;
    private const float SteamLargeScaleThreshold = 1.18f;
    private const float SteamExtremeScaleThreshold = 1.35f;
    private const float SteamSmallScaleThreshold = 0.85f;
    private const int SuspiciousPropCountThreshold = 26;
    private const int ExcessivePropCountThreshold = 42;
    private const int SuspiciousPropPairsThreshold = 5;
    private static readonly string[] _signatureKeywords =
    {
        "NOCLIP MOD", "SPEED", "GHOST", "INVIS", "TAGALL", "AUTOTAG", "RIGGUN",
        "PULL", "SPEED BOOST", "CRASH GUN", "LAG GUN", "TELEPORT GUN", "ESP",
        "ANTI-REPORT", "NAME CHANGER", "EXTERNAL MENU", "MODDED CLIENT",
        "Heavy Spoofer", "Spoofer", "SPOOFED PROPS", "EXTERNAL SIGNATURE", "COSMETX",
        "VOID", "WURST", "II STUPID", "PHANTOM", "ECLIPSE", "CRIMSON", "AZURE",
        "SENTINEL", "LUNAR", "SHADOW", "CELESTIAL", "NOVA", "APEX", "VIPER",
        "ZEPHYR", "PRISM", "PULSAR", "OBLIVION", "FROST", "INFERNO", "SPECTRE",
        "VENOM", "HYDRA", "GLITCH", "MIASMA", "COSMOS", "TWILIGHT", "STEREO",
        "FLUX", "GALAX", "NEBULA PAID", "RESURGENCE", "ELIXIR", "MANGO", "PLASMA",
        "PULL ALL", "PULL MOD", "FLING", "FLING GUN", "GRAB ALL", "GRAB GUN",
        "TELEPORT", "TP", "TP GUN", "NO CLIP", "WALL WALK", "AIRWALK",
        "LONG ARMS", "PLATFORMS", "BAN GUN", "KICK GUN", "SLOW ALL",
        "FREEZE ALL", "ANTI BAN", "MOD MENU", "PRIVATE MENU", "CLIENT",
        "PAID MENU", "FREE MENU", "PROTECTION", "SPOOFER", "FAKE PROPS",
        "SILENT AIM", "TRACERS", "AIMBOT", "BEACON", "RADAR"
    };
    private static readonly string[] _suspiciousPropertyKeywords =
    {
        "pull", "fling", "speed", "boost", "teleport", "tp", "noclip", "ghost",
        "invis", "crash", "lag", "ban", "kick", "freeze", "mod", "menu", "spoofer",
        "antireport", "anti-report", "platform", "airwalk", "longarm", "tagall", "autotag"
    };

    public List<string> DetectAllMods(VRRig rig)
    {
        _detectedModsBuffer.Clear();
        _detectedModsSet.Clear();

        if (rig == null)
            return _detectedModsBuffer;

        AddModsFromRigCache(rig);
        AddModsFromReflectionProps(rig);
        AddModsFromPhotonCustomProps(rig);
        AddBehavioralSignals(rig);
        CleanupMotionCache(rig);

        return _detectedModsBuffer;
    }

    public List<string> DetectModsFromCustomProps(VRRig rig)
    {
        return DetectAllMods(rig);
    }

    private void AddModsFromRigCache(VRRig rig)
    {
        string[] cachedMods = rig.GetPlayerMods();
        if (cachedMods == null || cachedMods.Length == 0)
            return;

        for (int i = 0; i < cachedMods.Length; i++)
            TryAddMod(cachedMods[i]);
    }

    private void AddModsFromReflectionProps(VRRig rig)
    {
        string[] reflectionMods = rig.GetCustomProperties();
        if (reflectionMods == null || reflectionMods.Length == 0)
            return;

        for (int i = 0; i < reflectionMods.Length; i++)
            TryAddMod(reflectionMods[i]);
    }

    private void AddModsFromPhotonCustomProps(VRRig rig)
    {
        Player p = rig?.Creator?.GetPlayerRef();

        if (p?.CustomProperties == null || p.CustomProperties.Count == 0)
            return;

        if (p.CustomProperties.Count >= 40)
            TryAddMod("Spoofer");

        if (p.CustomProperties.Count >= ExcessivePropCountThreshold)
            TryAddMod("PROP SPOOFER");
        else if (p.CustomProperties.Count >= SuspiciousPropCountThreshold)
            TryAddMod("CUSTOM PROP SPAM");

        int suspiciousPairs = 0;

        foreach (var kvp in p.CustomProperties)
        {
            string key = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(key) && Checker.Extensions.SpecialModsList.TryGetValue(key, out string modName))
            {
                TryAddMod(modName);
            }
            TryAddMatchedSignaturesFromText(key);

            string value = kvp.Value?.ToString();
            if (!string.IsNullOrEmpty(value) && Checker.Extensions.SpecialModsList.TryGetValue(value, out string valueModName))
            {
                TryAddMod(valueModName);
            }
            TryAddMatchedSignaturesFromText(value);

            if (IsSuspiciousPropertyPair(key, value))
                suspiciousPairs++;
        }

        if (suspiciousPairs >= SuspiciousPropPairsThreshold)
            TryAddMod("PROP SPOOFER");
    }

    private bool IsSuspiciousPropertyPair(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(value))
            return false;

        int score = 0;
        if (!string.IsNullOrEmpty(key) && key.Length >= 24)
            score++;
        if (!string.IsNullOrEmpty(value) && value.Length >= 40)
            score++;

        if (!string.IsNullOrEmpty(key))
        {
            string loweredKey = key.ToLowerInvariant();
            if (_suspiciousPropertyKeywords.Any(k => loweredKey.Contains(k)))
                score += 2;
        }

        if (!string.IsNullOrEmpty(value))
        {
            string loweredValue = value.ToLowerInvariant();
            if (_suspiciousPropertyKeywords.Any(k => loweredValue.Contains(k)))
                score += 2;
        }

        return score >= 2;
    }

    private void TryAddMatchedSignaturesFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        for (int i = 0; i < _signatureKeywords.Length; i++)
        {
            string keyword = _signatureKeywords[i];
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                TryAddMod(keyword);
        }
    }

    private void AddBehavioralSignals(VRRig rig)
    {
        Platform platform = rig.GetPlatform();

        int fps = rig.GetFPS();
        if (fps > 0 && fps <= LowFpsThreshold)
            TryAddMod("LOW FPS");

        float worldScale = WorldScaleResolver.GetWorldScale(rig);
        if (platform == Platform.Steam)
        {
            if (worldScale >= SteamExtremeScaleThreshold)
                TryAddMod("EXTREME SCALE");
            else if (worldScale >= SteamLargeScaleThreshold)
                TryAddMod("LARGE SCALE");
            else if (worldScale <= SteamSmallScaleThreshold)
                TryAddMod("SMALL SCALE");
        }

        Rigidbody rb = rig.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed >= SuspiciousSpeedThreshold)
            TryAddMod("SPEED");

        if (speed >= SpeedBoostThreshold)
            TryAddMod("SPEED BOOST");

        if (Mathf.Abs(velocity.y) >= FlyVerticalVelocityThreshold)
            TryAddMod("FLY");

        if (Mathf.Abs(velocity.y) >= FlyBoostVerticalVelocityThreshold)
            TryAddMod("FLY BOOST");

        AddMotionHeuristics(rig, velocity, speed);
    }

    private void AddMotionHeuristics(VRRig rig, Vector3 currentVelocity, float currentSpeed)
    {
        if (rig == null)
            return;

        float now = Time.time;
        Vector3 currentPosition = rig.transform.position;
        float currentDistanceToLocal = GetDistanceToLocalPlayer(currentPosition, out bool hasLocalDistance);

        if (!_motionSamples.TryGetValue(rig, out MotionSample sample) || sample.Time <= 0f)
        {
            _motionSamples[rig] = new MotionSample
            {
                Position = currentPosition,
                Velocity = currentVelocity,
                Time = now,
                DistanceToLocal = currentDistanceToLocal,
                HasDistanceToLocal = hasLocalDistance
            };
            return;
        }

        float dt = now - sample.Time;
        if (dt <= 0.0001f)
            return;

        float displacement = Vector3.Distance(currentPosition, sample.Position);
        float measuredSpeed = displacement / dt;
        float acceleration = (currentVelocity - sample.Velocity).magnitude / dt;

        if (currentSpeed >= ExtremeSpeedThreshold || measuredSpeed >= ExtremeSpeedThreshold)
            TryAddMod("SUPER SPEED");

        if (dt <= TeleportSampleMaxInterval && displacement >= TeleportDistanceThreshold)
            TryAddMod("TELEPORT");

        if (acceleration >= SuspiciousAccelerationThreshold)
            TryAddMod("SPEED BOOST");

        if (currentSpeed >= FlingVelocityThreshold && acceleration >= FlingAccelerationThreshold)
            TryAddMod("FLING");

        if (sample.HasDistanceToLocal && hasLocalDistance)
        {
            float distanceDelta = sample.DistanceToLocal - currentDistanceToLocal;
            float approachSpeed = distanceDelta / dt;
            if (distanceDelta >= PullDistanceDeltaThreshold && approachSpeed >= PullApproachSpeedThreshold)
                TryAddMod("PULL MOD");
        }

        _motionSamples[rig] = new MotionSample
        {
            Position = currentPosition,
            Velocity = currentVelocity,
            Time = now,
            DistanceToLocal = currentDistanceToLocal,
            HasDistanceToLocal = hasLocalDistance
        };
    }

    private static float GetDistanceToLocalPlayer(Vector3 targetPosition, out bool hasLocalDistance)
    {
        hasLocalDistance = false;

        Transform local = GorillaTagger.Instance?.offlineVRRig?.transform;
        if (local == null)
            return 0f;

        hasLocalDistance = true;
        return Vector3.Distance(local.position, targetPosition);
    }

    private void CleanupMotionCache(VRRig currentRig)
    {
        if (_motionSamples.Count <= 48)
            return;

        List<VRRig> toRemove = null;
        foreach (var entry in _motionSamples)
        {
            VRRig rig = entry.Key;
            if (rig == null || (currentRig != null && rig != currentRig && Time.time - entry.Value.Time > 25f))
            {
                toRemove ??= new List<VRRig>();
                toRemove.Add(rig);
            }
        }

        if (toRemove == null)
            return;

        for (int i = 0; i < toRemove.Count; i++)
            _motionSamples.Remove(toRemove[i]);
    }

    private void TryAddMod(string modName)
    {
        if (string.IsNullOrWhiteSpace(modName))
            return;

        modName = NormalizeModName(modName);

        if (_detectedModsSet.Add(modName))
            _detectedModsBuffer.Add(modName);
    }

    private static string NormalizeModName(string modName)
    {
        string trimmed = modName.Trim();

        if (trimmed.IndexOf("COSMET", StringComparison.OrdinalIgnoreCase) >= 0)
            return "COSMETX";

        if (trimmed.Equals("Heavy Spoofer", StringComparison.OrdinalIgnoreCase))
            return "SPOOFER";

        if (trimmed.Equals("TP", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("TP GUN", StringComparison.OrdinalIgnoreCase))
            return "TELEPORT";

        if (trimmed.Equals("NO CLIP", StringComparison.OrdinalIgnoreCase))
            return "NOCLIP";

        if (trimmed.Equals("PULL", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("PULL ALL", StringComparison.OrdinalIgnoreCase))
            return "PULL MOD";

        if (trimmed.Equals("MOD MENU", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("PRIVATE MENU", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("PAID MENU", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("FREE MENU", StringComparison.OrdinalIgnoreCase))
            return "MENU CLIENT";

        return trimmed;
    }

    public string FormatModsTextMultiline(List<string> mods, int maxChars)
    {
        if (mods == null || mods.Count == 0) return "";

        string joined = string.Join(", ", mods);
        return joined.Length > maxChars ? joined.Substring(0, maxChars) : joined;
    }
}
