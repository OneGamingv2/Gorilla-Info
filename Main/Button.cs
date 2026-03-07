using UnityEngine;
using System.Collections.Generic;
using GorillaInfo;

public class Button
{
    private static readonly Dictionary<Transform, bool> _buttonTouchStates = new(16);
    private float _nextAllowedClickTime;
    private float _interactionRadius = 0.005f;
    private bool _touchLatchActive;
    private bool _anyTouchThisFrame;
    private Collider _latchedCollider;
    private const float ClickCooldown = 0.2f;
    private const float OpenClickGuardSeconds = 0.28f;
    private const float MaxButtonColliderExtent = 0.22f;
    private bool _wasMenuOpen;

    public void checkbuttons()
    {
        var menu = GorillaInfoMain.Instance?.menuLoader?.menuInstance;
        var sphere = GorillaInfoMain.Instance?.buttonClick?.fingerSphere;

        if (menu == null || sphere == null || GorillaInfoMain.Instance.menuState != GorillaInfoMain.MenuState.Open)
        {
            _wasMenuOpen = false;
            _touchLatchActive = false;
            _latchedCollider = null;
            _buttonTouchStates.Clear();
            return;
        }

        if (!_wasMenuOpen)
        {
            _wasMenuOpen = true;
            _nextAllowedClickTime = Time.time + OpenClickGuardSeconds;
            _touchLatchActive = true;
            _latchedCollider = null;
            _buttonTouchStates.Clear();
        }

        Transform sections = FindDeepChild(menu.transform, "Sections");
        if (sections == null) return;

        _anyTouchThisFrame = false;

        Vector3 spherePos = sphere.transform.position;
        SphereCollider sphereCollider = sphere.GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            Vector3 lossy = sphere.transform.lossyScale;
            float scaleMax = Mathf.Max(lossy.x, Mathf.Max(lossy.y, lossy.z));
            _interactionRadius = Mathf.Clamp(sphereCollider.radius * scaleMax, 0.0035f, 0.006f);
        }

        if (TryPressButton(FindAnyDeepChild(sections, "HomeButton", "Home"), GorillaInfoMain.Instance.misc.EnableMain, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "MiscButton", "Misc"), GorillaInfoMain.Instance.misc.EnableMisc, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "SettingsButton", "Settings"), GorillaInfoMain.Instance.misc.EnableSettings, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "ActionsButton", "Actions"), GorillaInfoMain.Instance.misc.Enableactions, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "LobbyButton", "Lobby"), GorillaInfoMain.Instance.misc.EnableLobby, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "MusicButton", "Music"), GorillaInfoMain.Instance.misc.EnableMusic, spherePos)) return;

        var settingsPanel = GorillaInfoMain.Instance.menuLoader.settingsPanel;
        if (settingsPanel != null)
        {
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "Notifications", "Notification"), GorillaInfoMain.Instance.settingsHandler.ToggleNotifications, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "LockOn", "Lock On", "Lockon", "LockOnButton"), GorillaInfoMain.Instance.settingsHandler.ToggleLockOn, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "Nametags", "NameTags", "Nametag"), GorillaInfoMain.Instance.settingsHandler.ToggleNametags, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "GunStyle", "Gun Style"), GorillaInfoMain.Instance.settingsHandler.CycleGunStyle, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "PassThroughGun", "PassThrough", "Pass Through"), GorillaInfoMain.Instance.settingsHandler.TogglePassThroughGun, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "GunSize", "Gun Size"), GorillaInfoMain.Instance.settingsHandler.CycleGunSize, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "LockPointer", "Pointer"), GorillaInfoMain.Instance.settingsHandler.ToggleLockPointer, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "TargetSphere", "Sphere"), GorillaInfoMain.Instance.settingsHandler.ToggleTargetSphere, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "GunRay", "Ray"), GorillaInfoMain.Instance.settingsHandler.ToggleGunRay, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "ResetSettings", "Reset"), GorillaInfoMain.Instance.settingsHandler.ResetDefaults, spherePos)) return;
        }

        var actionsPanel = GorillaInfoMain.Instance.menuLoader.actionsPanel;
        if (actionsPanel != null)
        {
            if (TryPressButton(FindAnyDeepChild(actionsPanel.transform, "Scan Players", "ScanPlayers", "ScanPlayer", "Scan"), GorillaInfoMain.Instance.updMain.ScanAllPlayers, spherePos)) return;
            if (TryPressButton(FindDeepChild(actionsPanel.transform, "LobbyHop"), GorillaInfoMain.Instance.updMain.LobbyHop, spherePos)) return;
            if (TryPressButton(FindDeepChild(actionsPanel.transform, "JoinPrivate"), GorillaInfoMain.Instance.updMain.JoinPrivate, spherePos)) return;
            if (TryPressButton(FindDeepChild(actionsPanel.transform, "Disconnect"), GorillaInfoMain.Instance.updMain.Disconnect, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(actionsPanel.transform, "ClearSelection", "Clear Selection", "ClearTarget"), () =>
            {
                GorillaInfoMain.Instance.gunLib?.ClearSelection();
                GorillaInfoMain.Instance.updMain?.UpdateMainPage();
            }, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(actionsPanel.transform, "MoreInfoButton", "MoreInfo", "More Info"), () => GorillaInfoMain.Instance.moreInfoHandler?.ToggleMoreInfo(), spherePos)) return;
        }

        var musicPanel = GorillaInfoMain.Instance.menuLoader.musicPanel;
        if (musicPanel != null)
        {
            if (GorillaInfoMain.Instance.musicHandler != null)
            {
                if (TryPressButton(FindAnyDeepChild(musicPanel.transform, "Previous", "Prev"), GorillaInfoMain.Instance.musicHandler.PreviousTrack, spherePos)) return;
                if (TryPressButton(FindAnyDeepChild(musicPanel.transform, "PauseButton", "PlayPause", "Pause"), GorillaInfoMain.Instance.musicHandler.PlayPauseMusic, spherePos)) return;
                if (TryPressButton(FindAnyDeepChild(musicPanel.transform, "Next", "NextButton"), GorillaInfoMain.Instance.musicHandler.NextTrack, spherePos)) return;
                if (TryPressButton(FindAnyDeepChild(musicPanel.transform, "VolDown", "VolumeDown", "Vol -"), GorillaInfoMain.Instance.musicHandler.VolumeDown, spherePos)) return;
                if (TryPressButton(FindAnyDeepChild(musicPanel.transform, "Mute", "VolumeMute"), GorillaInfoMain.Instance.musicHandler.ToggleMute, spherePos)) return;
                if (TryPressButton(FindAnyDeepChild(musicPanel.transform, "VolUp", "VolumeUp", "Vol +"), GorillaInfoMain.Instance.musicHandler.VolumeUp, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "SpotifyButton"), GorillaInfoMain.Instance.musicHandler.OpenSpotify, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "Spotify"), GorillaInfoMain.Instance.musicHandler.OpenSpotify, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "YouTubeButton"), GorillaInfoMain.Instance.musicHandler.OpenYouTube, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "YouTube"), GorillaInfoMain.Instance.musicHandler.OpenYouTube, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "OpenBrowser"), GorillaInfoMain.Instance.musicHandler.OpenCurrentInBrowser, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "RefreshButton"), GorillaInfoMain.Instance.musicHandler.RefreshNowPlaying, spherePos)) return;
            }
        }

        var lobbyPanel = GorillaInfoMain.Instance.menuLoader.lobbyPanel;
        if (lobbyPanel != null)
        {
            for (int i = 0; i < 10; i++)
            {
                Transform selectBtn = FindDeepChild(lobbyPanel.transform, $"SelectPlayer{i}");
                if (selectBtn != null)
                {
                    int playerIdx = i;
                    if (TryPressButton(selectBtn, () => GorillaInfoMain.Instance.lobbyHandler?.SelectPlayer(playerIdx), spherePos)) return;
                }
            }
        }

        if (_latchedCollider != null && !IsTouchingCollider(_latchedCollider, spherePos))
        {
            _touchLatchActive = false;
            _latchedCollider = null;
        }

        if (!_anyTouchThisFrame)
        {
            _touchLatchActive = false;
            _latchedCollider = null;
        }
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

    private Transform FindAnyDeepChild(Transform parent, params string[] names)
    {
        if (parent == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeepChild(parent, names[i]);
            if (found != null)
                return ResolveButtonTransform(found);
        }

        for (int i = 0; i < names.Length; i++)
        {
            string normalizedTarget = NormalizeName(names[i]);
            Transform found = FindByNormalizedName(parent, normalizedTarget);
            if (found != null)
                return ResolveButtonTransform(found);
        }

        return null;
    }

    private Transform FindByNormalizedName(Transform parent, string normalizedName)
    {
        if (parent == null || string.IsNullOrEmpty(normalizedName))
            return null;

        if (NormalizeName(parent.name) == normalizedName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindByNormalizedName(parent.GetChild(i), normalizedName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Transform ResolveButtonTransform(Transform candidate)
    {
        if (candidate == null)
            return null;

        Collider candidateCollider = candidate.GetComponent<Collider>();
        if (IsLikelyButtonCollider(candidateCollider))
            return candidate;

        Transform parent = candidate.parent;
        if (parent != null && IsLikelyButtonCollider(parent.GetComponent<Collider>()))
            return parent;

        return null;
    }

    private bool IsLikelyButtonCollider(Collider col)
    {
        if (col == null)
            return false;

        Bounds b = col.bounds;
        if (b.extents.x > MaxButtonColliderExtent ||
            b.extents.y > MaxButtonColliderExtent ||
            b.extents.z > MaxButtonColliderExtent)
        {
            return false;
        }

        return true;
    }

    private bool IsTouchingCollider(Collider col, Vector3 spherePos)
    {
        if (col == null)
            return false;

        Vector3 closest = col.ClosestPoint(spherePos);
        return (spherePos - closest).sqrMagnitude <= (_interactionRadius * _interactionRadius);
    }

    private string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private bool TryPressButton(Transform btn, System.Action onPress, Vector3 spherePos)
    {
        Transform target = ResolveButtonTransform(btn);
        if (target == null) return false;

        Collider col = target.GetComponent<Collider>();
        if (col == null) return false;

        if (!_buttonTouchStates.TryGetValue(target, out bool wasTouching))
            wasTouching = false;

        bool touching = IsTouchingCollider(col, spherePos);
        if (touching)
            _anyTouchThisFrame = true;

        if (_touchLatchActive && _latchedCollider != null && col != _latchedCollider)
        {
            _buttonTouchStates[target] = touching;
            return false;
        }

        if (touching && !wasTouching && !_touchLatchActive && Time.time >= _nextAllowedClickTime)
        {
            AudioHelper.PlaySound("CreamyClick.wav");
            onPress?.Invoke();
            _nextAllowedClickTime = Time.time + ClickCooldown;
            _touchLatchActive = true;
            _latchedCollider = col;
            _buttonTouchStates[target] = true;
            return true;
        }

        _buttonTouchStates[target] = touching;
        return false;
    }
}
