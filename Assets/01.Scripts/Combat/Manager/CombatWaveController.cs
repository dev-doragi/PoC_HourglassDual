using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-38)]
public class CombatWaveController : MonoBehaviour
{
    [SerializeField] private CombatActorDataSO playerDataOverride;
    [SerializeField] private CombatActorDataSO[] enemyWaveDatas;
    [SerializeField] private bool startOnEnable = true;
    [SerializeField] private float nextWaveDelay = 0.6f;
    [SerializeField] private bool carryPlayerHpBetweenWaves;

    private HourglassCombatManager _combatManager;
    private Coroutine _nextWaveRoutine;
    private int _currentWaveIndex = -1;
    private int _carriedPlayerHp = -1;
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

        if (enemyWaveDatas == null || enemyWaveDatas.Length == 0)
        {
            Debug.LogError("[CombatWaveController] enemyWaveDatas is empty.", this);
            return;
        }

        _currentWaveIndex = -1;
        _carriedPlayerHp = -1;
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

        if (carryPlayerHpBetweenWaves)
        {
            int carriedHp = 0;
            if (evt.Snapshot.allies != null && evt.Snapshot.allies.Length > 0)
            {
                carriedHp = evt.Snapshot.allies[0].hp;
            }

            _carriedPlayerHp = Mathf.Max(0, carriedHp);
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
        if (enemyWaveDatas == null || nextWaveIndex >= enemyWaveDatas.Length)
        {
            _isWaveRunning = false;
            Debug.Log("[CombatWaveController] All waves cleared.");
            return;
        }

        CombatActorDataSO waveEnemyData = enemyWaveDatas[nextWaveIndex];
        if (waveEnemyData == null)
        {
            Debug.LogError($"[CombatWaveController] enemyWaveDatas[{nextWaveIndex}] is null.", this);
            _isWaveRunning = false;
            return;
        }

        _currentWaveIndex = nextWaveIndex;
        _combatManager.ConfigureCombatActors(playerDataOverride, waveEnemyData);
        if (carryPlayerHpBetweenWaves && _carriedPlayerHp >= 0)
        {
            _combatManager.SetNextCombatPlayerStartHpOverride(_carriedPlayerHp);
        }

        _combatManager.StartCombat();
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
