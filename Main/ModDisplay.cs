using Checker;
using System.Collections.Generic;
using UnityEngine;

namespace GorillaInfo
{
    public class ModDisplay
    {
        private TextMesh[] _modTexts;
        private GameObject[] _modContainers;
        private Collider _prevButtonCol, _nextButtonCol;
        private TextMesh _userHasText;
        private List<string> _allMods = new(32);
        private int _currentPage;
        private const int VisibleRows = 8;
        private const int StatusRowIndex = 0;
        private const int ModsPerPage = VisibleRows - 1;
        private readonly Dictionary<int, bool> _buttonTouchStates = new(2);

        public void Initialize(GameObject miscPanel)
        {
            if (miscPanel == null) return;

            _userHasText = miscPanel.transform.Find("UserHas")?.GetComponent<TextMesh>();
            _modTexts = new TextMesh[8];
            _modContainers = new GameObject[8];

            for (int i = 0; i < 8; i++)
            {
                Transform container = miscPanel.transform.Find($"ModShowThing{i + 1}");
                if (container != null)
                {
                    _modContainers[i] = container.gameObject;
                    _modTexts[i] = container.Find($"Mod{i + 1}")?.GetComponent<TextMesh>();
                }
            }

            Transform prevBtn = miscPanel.transform.Find("PrevButton");
            if (prevBtn != null)
                _prevButtonCol = prevBtn.GetComponent<Collider>() ?? prevBtn.GetComponentInChildren<Collider>();

            Transform nextBtn = miscPanel.transform.Find("NextButton");
            if (nextBtn != null)
                _nextButtonCol = nextBtn.GetComponent<Collider>() ?? nextBtn.GetComponentInChildren<Collider>();
        }

        public void SetMods(List<string> mods)
        {
            _allMods = mods != null ? new List<string>(mods) : new List<string>(0);
            _currentPage = 0;
            RefreshDisplay();
        }

        public void DetectAndDisplayMods(VRRig rig)
        {
            if (rig == null) return;

            _allMods.Clear();
            string[] customPropMods = rig.GetCustomProperties();
            foreach (string mod in customPropMods)
            {
                if (!_allMods.Contains(mod))
                    _allMods.Add(mod);
            }

            _currentPage = 0;
            RefreshDisplay();
        }

        public void Update()
        {
            GameObject fingerSphere = GorillaInfoMain.Instance.buttonClick?.fingerSphere;
            if (fingerSphere == null) return;

            Vector3 fingerPos = fingerSphere.transform.position;
            PageButton(_prevButtonCol, -1, fingerPos);
            PageButton(_nextButtonCol, 1, fingerPos);
        }

        private void PageButton(Collider col, int pageChange, Vector3 fingerPos)
        {
            if (col == null) return;

            int colId = col.GetInstanceID();
            bool touching = col.bounds.Contains(fingerPos);
            _buttonTouchStates.TryGetValue(colId, out bool wasTouching);

            if (touching && !wasTouching)
            {
                int newPage = _currentPage + pageChange;
                int maxPage = Mathf.Max(0, (_allMods.Count + ModsPerPage - 1) / ModsPerPage - 1);

                if (newPage >= 0 && newPage <= maxPage)
                {
                    _currentPage = newPage;
                    RefreshDisplay();
                }
            }

            _buttonTouchStates[colId] = touching;
        }

        private void RefreshDisplay()
        {
            UpdateModCount();

            int startIdx = _currentPage * ModsPerPage;
            bool flagged = _allMods.Count > 0;

            for (int i = 0; i < VisibleRows; i++)
            {
                if (_modContainers[i] == null || _modTexts[i] == null) continue;

                if (i == StatusRowIndex)
                {
                    _modTexts[i].text = flagged
                        ? "<color=#FF5E5E>STATUS: FLAGGED</color>"
                        : "<color=#7CFF7C>STATUS: CLEAN</color>";
                    _modContainers[i].SetActive(true);
                    continue;
                }

                int modIdx = startIdx + (i - 1);
                if (modIdx < _allMods.Count)
                {
                    _modTexts[i].text = _allMods[modIdx];
                    _modContainers[i].SetActive(true);
                }
                else
                {
                    _modContainers[i].SetActive(false);
                }
            }
        }

        private void UpdateModCount()
        {
            if (_userHasText != null)
            {
                if (_allMods.Count > 0)
                    _userHasText.text = $"<color=#FF5E5E>FLAGGED</color> | {_allMods.Count} detections";
                else
                    _userHasText.text = "<color=#7CFF7C>CLEAN</color> | 0 detections";
            }
        }
    }
}
