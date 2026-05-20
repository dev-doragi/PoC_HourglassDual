using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CombatTimelineView : MonoBehaviour
{
    [SerializeField] private Transform _entryRoot;
    [SerializeField] private CombatTimelineEntryView _entryPrefab;
    [SerializeField] private TMP_Text _fallbackText;

    private readonly List<CombatTimelineEntryView> _spawned = new List<CombatTimelineEntryView>();

    public void SetTimeline(CombatTimelineEntrySnapshot[] entries)
    {
        if (_entryRoot == null || _entryPrefab == null)
        {
            if (_fallbackText != null)
            {
                _fallbackText.text = BuildFallback(entries);
            }

            return;
        }

        EnsurePool(entries != null ? entries.Length : 0);
        for (int i = 0; i < _spawned.Count; i++)
        {
            bool active = entries != null && i < entries.Length;
            _spawned[i].gameObject.SetActive(active);
            if (active)
            {
                _spawned[i].Apply(entries[i]);
            }
        }
    }

    private void EnsurePool(int count)
    {
        while (_spawned.Count < count)
        {
            CombatTimelineEntryView view = Instantiate(_entryPrefab, _entryRoot);
            view.gameObject.SetActive(false);
            _spawned.Add(view);
        }
    }

    private static string BuildFallback(CombatTimelineEntrySnapshot[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            return "ENEMY ORDER\n-";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder("ENEMY ORDER\n");
        for (int i = 0; i < entries.Length; i++)
        {
            CombatTimelineEntrySnapshot e = entries[i];
            string suffix = string.Empty;
            if (e.status == "Skip") suffix = " X";
            else if (e.status == "Delay") suffix = " ↓";
            else if (e.status == "Dead") suffix = " (Dead)";
            else if (e.status == "EnemyOrderEntryStarted") suffix = " ▶";
            builder.Append($"{i + 1}. {e.label}{suffix}");
            if (i < entries.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }
}
