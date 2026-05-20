using DG.Tweening;
using UnityEngine;

[DefaultExecutionOrder(-39)]
public class CombatView : MonoBehaviour
{
    [SerializeField] private Transform playerPivot;
    [SerializeField] private Transform enemyPivot;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private SpriteRenderer enemySpriteRenderer;

    private HourglassCombatManager _combatManager;
    private CombatActorDataSO _playerData;
    private CombatActorDataSO _enemyData;
    private Vector3 _playerInitialLocalPosition;
    private Vector3 _enemyInitialLocalPosition;
    private Sequence _playerSequence;
    private Sequence _enemySequence;

    private void Awake()
    {
        CacheInitialLocalPositions();
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<CombatRoundStartedEvent>(OnCombatRoundStarted);
        EventBus.Instance.Subscribe<CombatTimelineEntryResolvedEvent>(OnCombatTimelineEntryResolved);
        EventBus.Instance.Subscribe<CombatActorDamagedEvent>(OnCombatActorDamaged);
        EventBus.Instance.Subscribe<CombatActorKilledEvent>(OnCombatActorKilled);
        EventBus.Instance.Subscribe<CombatEndedEvent>(OnCombatEnded);
    }

    private void Start()
    {
        CacheCombatManager();
        CacheActorData();
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<CombatRoundStartedEvent>(OnCombatRoundStarted);
        EventBus.Instance.Unsubscribe<CombatTimelineEntryResolvedEvent>(OnCombatTimelineEntryResolved);
        EventBus.Instance.Unsubscribe<CombatActorDamagedEvent>(OnCombatActorDamaged);
        EventBus.Instance.Unsubscribe<CombatActorKilledEvent>(OnCombatActorKilled);
        EventBus.Instance.Unsubscribe<CombatEndedEvent>(OnCombatEnded);
        KillAllSequences();
    }

    private void OnCombatRoundStarted(CombatRoundStartedEvent evt)
    {
        CacheCombatManager();
        CacheActorData();
        InitializeActorVisual(CombatActorType.Ally);
        InitializeActorVisual(CombatActorType.Enemy);
    }

    private void OnCombatTimelineEntryResolved(CombatTimelineEntryResolvedEvent evt)
    {
        if (!evt.Succeeded)
        {
            return;
        }

        if (evt.Entry.side == CombatTimelineSide.Ally)
        {
            PlayAttackSequence(CombatActorType.Ally);
        }
        else
        {
            PlayAttackSequence(CombatActorType.Enemy);
        }
    }

    private void OnCombatActorDamaged(CombatActorDamagedEvent evt)
    {
        CombatActorType side = ResolveActorType(evt.Snapshot, evt.ActorId);
        if (side == CombatActorType.Ally)
        {
            PlayHitSequence(CombatActorType.Ally, IsAlliesWiped(evt.Snapshot));
        }
        else if (side == CombatActorType.Enemy)
        {
            PlayHitSequence(CombatActorType.Enemy, IsEnemiesWiped(evt.Snapshot));
        }
    }

    private void OnCombatActorKilled(CombatActorKilledEvent evt)
    {
        CombatActorType side = ResolveActorType(evt.Snapshot, evt.ActorId);
        if (side == CombatActorType.Ally && IsAlliesWiped(evt.Snapshot))
        {
            PlayDeathSequence(CombatActorType.Ally);
        }
        else if (side == CombatActorType.Enemy && IsEnemiesWiped(evt.Snapshot))
        {
            PlayDeathSequence(CombatActorType.Enemy);
        }
    }

    private void OnCombatEnded(CombatEndedEvent evt)
    {
        if (evt.PlayerWon)
        {
            PlayDeathSequence(CombatActorType.Enemy);
        }
        else
        {
            PlayDeathSequence(CombatActorType.Ally);
        }
    }

    private void CacheCombatManager()
    {
        if (_combatManager != null)
        {
            return;
        }

        _combatManager = HourglassCombatManager.Instance;
    }

    private void CacheActorData()
    {
        if (_combatManager == null)
        {
            return;
        }

        _playerData = _combatManager.PlayerData;
        _enemyData = _combatManager.EnemyData;
    }

    private void CacheInitialLocalPositions()
    {
        _playerInitialLocalPosition = playerPivot != null ? playerPivot.localPosition : Vector3.zero;
        _enemyInitialLocalPosition = enemyPivot != null ? enemyPivot.localPosition : Vector3.zero;
    }

    private void InitializeActorVisual(CombatActorType actorType)
    {
        Transform pivot = GetPivot(actorType);
        SpriteRenderer renderer = GetRenderer(actorType);
        CombatActorDataSO data = GetActorData(actorType);
        Vector3 initialLocalPosition = GetInitialLocalPosition(actorType);

        if (renderer != null)
        {
            renderer.gameObject.SetActive(true);
            SetRendererAlpha(renderer, 1f);
        }

        if (pivot != null)
        {
            pivot.localPosition = initialLocalPosition;
        }

        if (renderer != null && data != null && data.idleSprite != null)
        {
            renderer.sprite = data.idleSprite;
        }
    }

    private void PlayAttackSequence(CombatActorType actorType)
    {
        Transform pivot = GetPivot(actorType);
        SpriteRenderer renderer = GetRenderer(actorType);
        CombatActorDataSO data = GetActorData(actorType);
        Vector3 initialLocalPosition = GetInitialLocalPosition(actorType);
        if (pivot == null || renderer == null || data == null)
        {
            return;
        }

        KillSequence(actorType);
        if (data.attackSprite != null)
        {
            renderer.sprite = data.attackSprite;
        }

        Vector3 directionOffset = actorType == CombatActorType.Ally ? data.attackMoveOffset : -data.attackMoveOffset;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(pivot.DOLocalMove(initialLocalPosition + directionOffset, Mathf.Max(0f, data.attackMoveDuration)).SetEase(Ease.OutQuad));
        sequence.Append(pivot.DOLocalMove(initialLocalPosition, Mathf.Max(0f, data.attackReturnDuration)).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            pivot.localPosition = initialLocalPosition;
            if (data.idleSprite != null)
            {
                renderer.sprite = data.idleSprite;
            }
        });

        SetSequence(actorType, sequence);
    }

    private void PlayHitSequence(CombatActorType actorType, bool isDead)
    {
        Transform pivot = GetPivot(actorType);
        SpriteRenderer renderer = GetRenderer(actorType);
        CombatActorDataSO data = GetActorData(actorType);
        Vector3 initialLocalPosition = GetInitialLocalPosition(actorType);
        if (pivot == null || renderer == null || data == null)
        {
            return;
        }

        KillSequence(actorType);
        if (data.hitSprite != null)
        {
            renderer.sprite = data.hitSprite;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Append(pivot.DOShakePosition(
            Mathf.Max(0f, data.hitShakeDuration),
            data.hitShakeStrength,
            Mathf.Max(0, data.hitShakeVibrato),
            90f,
            false,
            true));
        sequence.OnComplete(() =>
        {
            pivot.localPosition = initialLocalPosition;
            if (isDead)
            {
                PlayDeathSequence(actorType);
            }
            else if (data.idleSprite != null)
            {
                renderer.sprite = data.idleSprite;
            }
        });

        SetSequence(actorType, sequence);
    }

    private void PlayDeathSequence(CombatActorType actorType)
    {
        SpriteRenderer renderer = GetRenderer(actorType);
        CombatActorDataSO data = GetActorData(actorType);
        if (renderer == null || data == null)
        {
            return;
        }

        KillSequence(actorType);
        if (data.deathSprite != null)
        {
            renderer.sprite = data.deathSprite;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Append(DOTween.To(
            () => renderer.color.a,
            alpha => SetRendererAlpha(renderer, alpha),
            0f,
            Mathf.Max(0f, data.deathFadeDuration)).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            if (renderer != null)
            {
                renderer.gameObject.SetActive(false);
            }
        });

        SetSequence(actorType, sequence);
    }

    private Transform GetPivot(CombatActorType actorType)
    {
        return actorType == CombatActorType.Enemy ? enemyPivot : playerPivot;
    }

    private SpriteRenderer GetRenderer(CombatActorType actorType)
    {
        return actorType == CombatActorType.Enemy ? enemySpriteRenderer : playerSpriteRenderer;
    }

    private CombatActorDataSO GetActorData(CombatActorType actorType)
    {
        return actorType == CombatActorType.Enemy ? _enemyData : _playerData;
    }

    private Vector3 GetInitialLocalPosition(CombatActorType actorType)
    {
        return actorType == CombatActorType.Enemy ? _enemyInitialLocalPosition : _playerInitialLocalPosition;
    }

    private void SetSequence(CombatActorType actorType, Sequence sequence)
    {
        if (actorType == CombatActorType.Enemy)
        {
            _enemySequence = sequence;
            return;
        }

        _playerSequence = sequence;
    }

    private void KillSequence(CombatActorType actorType)
    {
        if (actorType == CombatActorType.Enemy)
        {
            if (_enemySequence != null)
            {
                _enemySequence.Kill(false);
                _enemySequence = null;
            }

            return;
        }

        if (_playerSequence != null)
        {
            _playerSequence.Kill(false);
            _playerSequence = null;
        }
    }

    private void KillAllSequences()
    {
        KillSequence(CombatActorType.Ally);
        KillSequence(CombatActorType.Enemy);
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

    private static CombatActorType ResolveActorType(CombatLogSnapshot snapshot, int actorId)
    {
        for (int i = 0; i < snapshot.allies.Length; i++)
        {
            if (snapshot.allies[i].actor_id == actorId)
            {
                return CombatActorType.Ally;
            }
        }

        for (int i = 0; i < snapshot.enemies.Length; i++)
        {
            if (snapshot.enemies[i].actor_id == actorId)
            {
                return CombatActorType.Enemy;
            }
        }

        return CombatActorType.None;
    }

    private static bool IsAlliesWiped(CombatLogSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.allies.Length; i++)
        {
            if (!snapshot.allies[i].is_dead)
            {
                return false;
            }
        }

        return snapshot.allies.Length > 0;
    }

    private static bool IsEnemiesWiped(CombatLogSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.enemies.Length; i++)
        {
            if (!snapshot.enemies[i].is_dead)
            {
                return false;
            }
        }

        return snapshot.enemies.Length > 0;
    }
}
