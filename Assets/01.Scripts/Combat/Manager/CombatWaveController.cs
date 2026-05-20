using System.Collections;
using UnityEngine;

[System.Serializable]
public class CombatWaveData
{
    public string waveName = "Wave";
    public CombatActorDataSO[] enemyPartyActors = new CombatActorDataSO[3];
}

[DefaultExecutionOrder(-38)]
public class CombatWaveController : MonoBehaviour
{
    [Header("Ally Party (3)")]
    [SerializeField] private CombatActorDataSO[] allyPartyActors = new CombatActorDataSO[3];

    [Header("Enemy Waves (3v3 each)")]
    [SerializeField] private CombatWaveData[] waves;

    [Header("Flow")]
    [SerializeField] private bool startOnEnable = true;
    [SerializeField] private float nextWaveDelay = 0.6f;
    [SerializeField] private bool carryFrontAllyHpBetweenWaves;

    private HourglassCombatManager _combatManager;
    private Coroutine _nextWaveRoutine;
    private int _currentWaveIndex = -1;
    private int _carriedFrontAllyHp = -1;
    private bool _isWaveRunning;

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<CombatEndedEvent>(OnCombatEnded);
    }

    private void Start()
    {
        CacheCombatManager();
        if (startOnEnable)
        {
            StartFromFirstWave();
        }
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<CombatEndedEvent>(OnCombatEnded);
        if (_nextWaveRoutine != null)
        {
            StopCoroutine(_nextWaveRoutine);
            _nextWaveRoutine = null;
        }
    }

    public void StartFromFirstWave()
    {
        CacheCombatManager();
        if (_combatManager == null)
        {
            Debug.LogError("[CombatWaveController] HourglassCombatManager is not available.", this);
            return;
        }

        if (!ValidateAllyParty() || waves == null || waves.Length == 0)
        {
            Debug.LogError("[CombatWaveController] Wave configuration is invalid (requires 3 allies and at least one 3-enemy wave).", this);
            return;
        }

        _currentWaveIndex = -1;
        _carriedFrontAllyHp = -1;
        _isWaveRunning = true;
        StartNextWave();
    }

    private void OnCombatEnded(CombatEndedEvent evt)
    {
        if (!_isWaveRunning)
        {
            return;
        }

        if (!evt.PlayerWon)
        {
            _isWaveRunning = false;
            if (_nextWaveRoutine != null)
            {
                StopCoroutine(_nextWaveRoutine);
                _nextWaveRoutine = null;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.GameOver);
            }
            else
            {
                Debug.LogError("[CombatWaveController] GameManager.Instance is null. Cannot show GameOver panel.", this);
            }

            return;
        }

        if (carryFrontAllyHpBetweenWaves)
        {
            int carriedHp = 0;
            if (evt.Snapshot.allies != null && evt.Snapshot.allies.Length > 0)
            {
                carriedHp = evt.Snapshot.allies[0].hp;
            }

            _carriedFrontAllyHp = Mathf.Max(0, carriedHp);
        }

        if (_nextWaveRoutine != null)
        {
            StopCoroutine(_nextWaveRoutine);
        }

        _nextWaveRoutine = StartCoroutine(StartNextWaveAfterDelay());
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        float delay = Mathf.Max(0f, nextWaveDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        _nextWaveRoutine = null;
        StartNextWave();
    }

    private void StartNextWave()
    {
        if (_combatManager == null)
        {
            Debug.LogError("[CombatWaveController] HourglassCombatManager is not available.", this);
            _isWaveRunning = false;
            return;
        }

        int nextWaveIndex = _currentWaveIndex + 1;
        if (waves == null || nextWaveIndex >= waves.Length)
        {
            _isWaveRunning = false;
            Debug.Log("[CombatWaveController] All waves cleared.");
            return;
        }

        CombatWaveData wave = waves[nextWaveIndex];
        if (!ValidateWave(wave, nextWaveIndex))
        {
            _isWaveRunning = false;
            return;
        }

        _currentWaveIndex = nextWaveIndex;
        _combatManager.ConfigureCombatParties(allyPartyActors, wave.enemyPartyActors);
        if (carryFrontAllyHpBetweenWaves && _carriedFrontAllyHp >= 0)
        {
            _combatManager.SetNextCombatPlayerStartHpOverride(_carriedFrontAllyHp);
        }

        _combatManager.StartCombat();
    }

    private bool ValidateAllyParty()
    {
        if (allyPartyActors == null || allyPartyActors.Length < 3)
        {
            return false;
        }

        for (int i = 0; i < 3; i++)
        {
            if (allyPartyActors[i] == null)
            {
                Debug.LogError($"[CombatWaveController] allyPartyActors[{i}] is null.", this);
                return false;
            }
        }

        return true;
    }

    private bool ValidateWave(CombatWaveData wave, int index)
    {
        if (wave == null || wave.enemyPartyActors == null || wave.enemyPartyActors.Length < 3)
        {
            Debug.LogError($"[CombatWaveController] waves[{index}] is invalid (needs 3 enemy actors).", this);
            return false;
        }

        for (int i = 0; i < 3; i++)
        {
            if (wave.enemyPartyActors[i] == null)
            {
                Debug.LogError($"[CombatWaveController] waves[{index}].enemyPartyActors[{i}] is null.", this);
                return false;
            }
        }

        return true;
    }

    private void CacheCombatManager()
    {
        if (_combatManager != null)
        {
            return;
        }

        _combatManager = HourglassCombatManager.Instance;
        if (_combatManager == null)
        {
            Debug.LogError("[CombatWaveController] HourglassCombatManager.Instance is null.", this);
        }
    }
}
