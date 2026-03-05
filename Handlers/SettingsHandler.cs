using UnityEngine;
using GorillaInfo;
using BepInEx.Configuration;

public class SettingsHandler
{
    private static readonly string[] GunStyles = { "Purple", "Red", "Green", "Yellow" };
    private TextMesh _lockOnText, _nametagsText, _gunStyleText, _passThroughText;
    private bool _lockOnEnabled, _nametagsEnabled, _passThroughEnabled;
    private int _gunStyleIndex;
    private bool _configInitialized;
    private ConfigEntry<bool> _lockOnConfig;
    private ConfigEntry<bool> _nametagsConfig;
    private ConfigEntry<bool> _passThroughConfig;
    private ConfigEntry<int> _gunStyleConfig;
    private ConfigEntry<bool> _lockPointerConfig;

    public void InitializeSettings()
    {
        EnsureConfigBindings();

        Transform settings = GorillaInfoMain.Instance.menuLoader.settingsPanel?.transform;
        if (settings == null) return;

        _lockOnText = FindButtonLabel(settings, "LockOn");
        _nametagsText = FindButtonLabel(settings, "Nametags");
        _gunStyleText = FindButtonLabel(settings, "GunStyle");
        _passThroughText = FindButtonLabel(settings, "PassThroughGun");

        var gunLib = GorillaInfoMain.Instance.gunLib;
        if (gunLib != null)
        {
            _lockOnEnabled = _lockOnConfig.Value;
            _nametagsEnabled = _nametagsConfig.Value;
            _passThroughEnabled = _passThroughConfig.Value;
            _gunStyleIndex = Mathf.Clamp(_gunStyleConfig.Value, 0, GunStyles.Length - 1);

            gunLib.autoLockEnabled = _lockOnEnabled;
            gunLib.nametagsEnabled = _nametagsEnabled;
            gunLib.passThroughEnabled = _passThroughEnabled;
            gunLib.lockPointerEnabled = _lockPointerConfig.Value;
            gunLib.SetGunStyle(_gunStyleIndex);
        }

        UpdateAllTexts();
        GorillaInfoMain.Instance.updMain?.UpdateMainPage();
    }

    private void EnsureConfigBindings()
    {
        if (_configInitialized)
            return;

        var cfg = GorillaInfoMain.Instance.Config;
        _lockOnConfig = cfg.Bind("CheckerSettings", "LockOnEnabled", false, "Enable lock-on mode.");
        _nametagsConfig = cfg.Bind("CheckerSettings", "NametagsEnabled", false, "Enable nametags.");
        _passThroughConfig = cfg.Bind("CheckerSettings", "PassThroughEnabled", false, "Allow pass-through target detection.");
        _gunStyleConfig = cfg.Bind("CheckerSettings", "GunStyleIndex", 0, "Current gun style index.");
        _lockPointerConfig = cfg.Bind("CheckerSettings", "LockPointerEnabled", true, "Show pointer line when lock-on is ON.");
        _configInitialized = true;
    }

    private TextMesh FindButtonLabel(Transform root, string buttonName)
    {
        Transform btn = FindDeepChild(root, buttonName);
        if (btn == null)
            return null;

        TextMesh tm = btn.GetComponent<TextMesh>();
        if (tm != null)
            return tm;

        return btn.GetComponentInChildren<TextMesh>(true);
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        Transform direct = parent.Find(name);
        if (direct != null)
            return direct;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    public void ToggleLockOn()
    {
        _lockOnEnabled = !_lockOnEnabled;
        GorillaInfoMain.Instance.gunLib.autoLockEnabled = _lockOnEnabled;
        _lockOnConfig.Value = _lockOnEnabled;
        if (_lockOnText != null)
            _lockOnText.text = _lockOnEnabled ? "LockOn: ON" : "LockOn: OFF";
        GorillaInfoMain.Instance.Config.Save();
        GorillaInfoMain.Instance.updMain?.UpdateMainPage();
    }

    public void ToggleNametags()
    {
        _nametagsEnabled = !_nametagsEnabled;
        GorillaInfoMain.Instance.gunLib.nametagsEnabled = _nametagsEnabled;
        _nametagsConfig.Value = _nametagsEnabled;
        if (_nametagsText != null)
            _nametagsText.text = _nametagsEnabled ? "Nametags: ON" : "Nametags: OFF";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void CycleGunStyle()
    {
        _gunStyleIndex = (_gunStyleIndex + 1) % GunStyles.Length;
        _gunStyleConfig.Value = _gunStyleIndex;
        GorillaInfoMain.Instance.gunLib.SetGunStyle(_gunStyleIndex);
        if (_gunStyleText != null)
            _gunStyleText.text = $"GunStyle: {GunStyles[_gunStyleIndex]}";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void TogglePassThroughGun()
    {
        _passThroughEnabled = !_passThroughEnabled;
        GorillaInfoMain.Instance.gunLib.passThroughEnabled = _passThroughEnabled;
        _passThroughConfig.Value = _passThroughEnabled;
        if (_passThroughText != null)
            _passThroughText.text = _passThroughEnabled ? "PassThrough: ON" : "PassThrough: OFF";
        GorillaInfoMain.Instance.Config.Save();
    }

    private void UpdateAllTexts()
    {
        if (_lockOnText != null) _lockOnText.text = _lockOnEnabled ? "LockOn: ON" : "LockOn: OFF";
        if (_nametagsText != null) _nametagsText.text = _nametagsEnabled ? "Nametags: ON" : "Nametags: OFF";
        if (_gunStyleText != null) _gunStyleText.text = $"GunStyle: {GunStyles[_gunStyleIndex]}";
        if (_passThroughText != null) _passThroughText.text = _passThroughEnabled ? "PassThrough: ON" : "PassThrough: OFF";
    }
}
