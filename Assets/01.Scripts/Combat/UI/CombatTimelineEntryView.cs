using TMPro;
using UnityEngine;

public class CombatTimelineEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;

    public void Apply(CombatTimelineEntrySnapshot entry)
    {
        if (_text == null)
        {
            return;
        }

        string marker = string.Empty;
        if (entry.status == "Skip")
        {
            marker = " X";
        }
        else if (entry.status == "Delay")
        {
            marker = " ↓";
        }
        else if (entry.status == "Dead")
        {
            marker = " (Dead)";
        }
        else if (entry.status == "EnemyOrderEntryStarted")
        {
            marker = " ▶";
        }

        _text.text = $"{entry.timeline_index + 1}. {entry.label}{marker}";
    }
}
