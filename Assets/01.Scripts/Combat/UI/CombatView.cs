using DG.Tweening;
using UnityEngine;

[DefaultExecutionOrder(-39)]
public class CombatView : MonoBehaviour
{
    [Header("3v3 Pivots (slot index order)")]
    [SerializeField] private Transform[] allyPivots = new Transform[3];
    [SerializeField] private Transform[] enemyPivots = new Transform[3];
    [SerializeField] private SpriteRenderer[] allyRenderers = new SpriteRenderer[3];
    [SerializeField] private SpriteRenderer[] enemyRenderers = new SpriteRenderer[3];

    private HourglassCombatManager _combatManager;
    private readonly Vector3[] _allyInitialLocalPositions = new Vector3[3];
    private readonly Vector3[] _enemyInitialLocalPositions = new Vector3[3];
    private readonly Sequence[] _allySequences = new Sequence[3];
    private readonly Sequence[] _enemySequences = new Sequence[3];

    private void Awake()
    {
        CacheInitialLocalPositions();
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<CombatRoundStartedEvent>(OnCombatRoundStarted);
        EventBus.Instance.Subscribe<CombatAllyCommandResolvedEvent>(OnCombatAllyCommandResolved);
        EventBus.Instance.Subscribe<CombatEnemyOrderEntryResolvedEvent>(OnCombatEnemyOrderEntryResolved);
        EventBus.Instance.Subscribe<CombatActorDamagedEvent>(OnCombatActorDamaged);
        EventBus.Instance.Subscribe<CombatActorKilledEvent>(OnCombatActorKilled);
    }

    private void Start()
    {
        CacheCombatManager();
        InitializeAllActors();
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<CombatRoundStartedEvent>(OnCombatRoundStarted);
        EventBus.Instance.Unsubscribe<CombatAllyCommandResolvedEvent>(OnCombatAllyCommandResolved);
        EventBus.Instance.Unsubscribe<CombatEnemyOrderEntryResolvedEvent>(OnCombatEnemyOrderEntryResolved);
        EventBus.Instance.Unsubscribe<CombatActorDamagedEvent>(OnCombatActorDamaged);
        EventBus.Instance.Unsubscribe<CombatActorKilledEvent>(OnCombatActorKilled);
        KillAllSequences();
    }

    private void OnCombatRoundStarted(CombatRoundStartedEvent evt)
    {
        CacheCombatManager();
        InitializeAllActors();
    }

    private void OnCombatAllyCommandResolved(CombatAllyCommandResolvedEvent evt)
    {
        if (!evt.Succeeded)
        {
            return;
        }

        PlayAttackSequence(evt.Command.SourceActorId);
    }

    private void OnCombatEnemyOrderEntryResolved(CombatEnemyOrderEntryResolvedEvent evt)
    {
        if (evt.Message == "EnemyOrderEntryStarted")
        {
            PlayAttackSequence(evt.Entry.source_actor_id);
        }
    }

    private void OnCombatActorDamaged(CombatActorDamagedEvent evt)
    {
        PlayHitSequence(evt.ActorId);
    }

    private void OnCombatActorKilled(CombatActorKilledEvent evt)
    {
        PlayDeathSequence(evt.ActorId);
    }

    private void CacheCombatManager()
    {
        if (_combatManager != null)
        {
            return;
        }

        _combatManager = HourglassCombatManager.Instance;
    }

    private void CacheInitialLocalPositions()
    {
        for (int i = 0; i < 3; i++)
        {
            Transform allyPivot = GetPivot(CombatActorType.Ally, i);
            Transform enemyPivotAtSlot = GetPivot(CombatActorType.Enemy, i);
            _allyInitialLocalPositions[i] = allyPivot != null ? allyPivot.localPosition : Vector3.zero;
            _enemyInitialLocalPositions[i] = enemyPivotAtSlot != null ? enemyPivotAtSlot.localPosition : Vector3.zero;
        }
    }

    private void InitializeAllActors()
    {
        if (_combatManager == null || _combatManager.RuntimeState == null)
        {
            return;
        }

        InitializeTeam(_combatManager.RuntimeState.Allies, CombatActorType.Ally);
        InitializeTeam(_combatManager.RuntimeState.Enemies, CombatActorType.Enemy);
    }

    private void InitializeTeam(System.Collections.Generic.List<CombatActorRuntime> actors, CombatActorType actorType)
    {
        for (int slot = 0; slot < 3; slot++)
        {
            CombatActorRuntime actor = (actors != null && slot < actors.Count) ? actors[slot] : null;
            InitializeSlotVisual(actorType, slot, actor);
        }
    }

    private void InitializeSlotVisual(CombatActorType actorType, int slotIndex, CombatActorRuntime actor)
    {
        Transform pivot = GetPivot(actorType, slotIndex);
        SpriteRenderer renderer = GetRenderer(actorType, slotIndex);
        if (pivot != null)
        {
            pivot.localPosition = GetInitialLocalPosition(actorType, slotIndex);
        }

        if (renderer == null)
        {
            return;
        }

        if (actor == null)
        {
            renderer.gameObject.SetActive(false);
            return;
        }

        renderer.gameObject.SetActive(true);
        SetRendererAlpha(renderer, 1f);
        CombatActorDataSO data = actor.SourceData;
        if (data != null && data.idleSprite != null)
        {
            renderer.sprite = data.idleSprite;
        }
    }

    private void PlayAttackSequence(int actorId)
    {
        if (!TryResolveActorRef(actorId, out CombatActorRuntime actor, out Transform pivot, out SpriteRenderer renderer))
        {
            return;
        }

        CombatActorDataSO data = actor.SourceData;
        int slot = Mathf.Clamp(actor.SlotIndex, 0, 2);
        Vector3 initialLocalPosition = GetInitialLocalPosition(actor.ActorType, slot);

        KillSequence(actor.ActorType, slot);
        if (data != null && data.attackSprite != null)
        {
            renderer.sprite = data.attackSprite;
        }

        Vector3 moveOffset = data != null ? data.attackMoveOffset : new Vector3(0.35f, 0f, 0f);
        if (actor.ActorType == CombatActorType.Enemy)
        {
            moveOffset = -moveOffset;
        }

        float moveDuration = data != null ? Mathf.Max(0f, data.attackMoveDuration) : 0.12f;
        float returnDuration = data != null ? Mathf.Max(0f, data.attackReturnDuration) : 0.14f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(pivot.DOLocalMove(initialLocalPosition + moveOffset, moveDuration).SetEase(Ease.OutQuad));
        sequence.Append(pivot.DOLocalMove(initialLocalPosition, returnDuration).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            if (pivot != null)
            {
                pivot.localPosition = initialLocalPosition;
            }

            if (renderer != null && data != null && data.idleSprite != null)
            {
                renderer.sprite = data.idleSprite;
            }
        });

        SetSequence(actor.ActorType, slot, sequence);
    }

    private void PlayHitSequence(int actorId)
    {
        if (!TryResolveActorRef(actorId, out CombatActorRuntime actor, out Transform pivot, out SpriteRenderer renderer))
        {
            return;
        }

        CombatActorDataSO data = actor.SourceData;
        int slot = Mathf.Clamp(actor.SlotIndex, 0, 2);
        Vector3 initialLocalPosition = GetInitialLocalPosition(actor.ActorType, slot);

        KillSequence(actor.ActorType, slot);
        if (data != null && data.hitSprite != null)
        {
            renderer.sprite = data.hitSprite;
        }

        float duration = data != null ? Mathf.Max(0f, data.hitShakeDuration) : 0.18f;
        float strength = data != null ? data.hitShakeStrength : 0.12f;
        int vibrato = data != null ? Mathf.Max(0, data.hitShakeVibrato) : 12;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(pivot.DOShakePosition(duration, strength, vibrato, 90f, false, true));
        sequence.OnComplete(() =>
        {
            if (pivot != null)
            {
                pivot.localPosition = initialLocalPosition;
            }

            if (actor.IsDead)
            {
                PlayDeathSequence(actorId);
            }
            else if (renderer != null && data != null && data.idleSprite != null)
            {
                renderer.sprite = data.idleSprite;
            }
        });

        SetSequence(actor.ActorType, slot, sequence);
    }

    private void PlayDeathSequence(int actorId)
    {
        if (!TryResolveActorRef(actorId, out CombatActorRuntime actor, out _, out SpriteRenderer renderer))
        {
            return;
        }

        CombatActorDataSO data = actor.SourceData;
        int slot = Mathf.Clamp(actor.SlotIndex, 0, 2);
        KillSequence(actor.ActorType, slot);

        if (renderer == null)
        {
            return;
        }

        if (data != null && data.deathSprite != null)
        {
            renderer.sprite = data.deathSprite;
        }

        float fadeDuration = data != null ? Mathf.Max(0f, data.deathFadeDuration) : 0.7f;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(DOTween.To(
            () => renderer.color.a,
            alpha => SetRendererAlpha(renderer, alpha),
            0f,
            fadeDuration).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            if (renderer != null)
            {
                renderer.gameObject.SetActive(false);
            }
        });

        SetSequence(actor.ActorType, slot, sequence);
    }

    private bool TryResolveActorRef(int actorId, out CombatActorRuntime actor, out Transform pivot, out SpriteRenderer renderer)
    {
        actor = null;
        pivot = null;
        renderer = null;
        if (_combatManager == null || _combatManager.RuntimeState == null)
        {
            return false;
        }

        actor = _combatManager.RuntimeState.GetActorById(actorId);
        if (actor == null)
        {
            return false;
        }

        int slot = Mathf.Clamp(actor.SlotIndex, 0, 2);
        pivot = GetPivot(actor.ActorType, slot);
        renderer = GetRenderer(actor.ActorType, slot);
        return pivot != null && renderer != null;
    }

    private Transform GetPivot(CombatActorType actorType, int slotIndex)
    {
        Transform[] array = actorType == CombatActorType.Enemy ? enemyPivots : allyPivots;
        if (array != null && slotIndex >= 0 && slotIndex < array.Length)
        {
            return array[slotIndex];
        }

        return null;
    }

    private SpriteRenderer GetRenderer(CombatActorType actorType, int slotIndex)
    {
        SpriteRenderer[] array = actorType == CombatActorType.Enemy ? enemyRenderers : allyRenderers;
        if (array != null && slotIndex >= 0 && slotIndex < array.Length)
        {
            return array[slotIndex];
        }

        return null;
    }

    private Vector3 GetInitialLocalPosition(CombatActorType actorType, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
        {
            return Vector3.zero;
        }

        return actorType == CombatActorType.Enemy ? _enemyInitialLocalPositions[slotIndex] : _allyInitialLocalPositions[slotIndex];
    }

    private void SetSequence(CombatActorType actorType, int slotIndex, Sequence sequence)
    {
        if (slotIndex < 0 || slotIndex > 2)
        {
            return;
        }

        if (actorType == CombatActorType.Enemy)
        {
            _enemySequences[slotIndex] = sequence;
            return;
        }

        _allySequences[slotIndex] = sequence;
    }

    private void KillSequence(CombatActorType actorType, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
        {
            return;
        }

        Sequence[] array = actorType == CombatActorType.Enemy ? _enemySequences : _allySequences;
        if (array[slotIndex] != null)
        {
            array[slotIndex].Kill(false);
            array[slotIndex] = null;
        }
    }

    private void KillAllSequences()
    {
        for (int i = 0; i < 3; i++)
        {
            KillSequence(CombatActorType.Ally, i);
            KillSequence(CombatActorType.Enemy, i);
        }
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }
}
