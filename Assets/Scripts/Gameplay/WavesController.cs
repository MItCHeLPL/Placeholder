using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEditor;
using System.Linq;
using static UnityEngine.EventSystems.EventTrigger;

public class WavesController : MonoBehaviour
{
    [Header("Waves")]
    [SerializeField] public List<Wave> Waves = new List<Wave>();
    public Wave CurrentWave => Waves.Count > 0 ? Waves[currentWaveId] : null;
    [HideInInspector] public int currentWaveId = 0;

    [Header("Settings")]
    [SerializeField] private float initialCooldown = 5;
    [SerializeField] private float maxEnemiesOnScene = 100;
    [SerializeField] private float cooldownBetweenEnemySpawn = 0.1f;

    [Header("States")]
    [HideInInspector] public bool isWaveRunning = false;
    [HideInInspector] public bool allEnemiesSpawned = false;
    [HideInInspector] public int enemiesSpawnedInCurrentWave = 0;

    [Header("Timers")]
    [HideInInspector] public Timer waveTimer = new Timer();
    [HideInInspector] public CountdownTimer cooldownTimer = new CountdownTimer();

    [Header("Events")]
    public UnityEvent OnWaveStart;
    public UnityEvent OnWaveFinish;

    [Header("References")]
    private EnemySpawner enemySpawner;


    private void Start()
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.WavesController = this;
        }

        enemySpawner = GameController.Instance.EnemySpawner;
        enemySpawner.OnNoEnemiesLeft.AddListener(FinishWave);

        currentWaveId = 0;
        isWaveRunning = false;
        allEnemiesSpawned = false;
        enemiesSpawnedInCurrentWave = 0;

        //Start first wave
        StartCoroutine(WaveCooldown(0, initialCooldown));
    }

    private void OnDisable()
    {
        if(enemySpawner != null)
        {
            enemySpawner.OnNoEnemiesLeft.RemoveListener(FinishWave);
        }
    }


    public void StartNextWave()
    {
        StartCoroutine(WaveCooldown((byte)(currentWaveId + 1), CurrentWave.CooldownAfterWave));
    }


    private void StartWave(byte waveId)
    {
        isWaveRunning = true;

        currentWaveId = waveId;

        allEnemiesSpawned = false;
        enemiesSpawnedInCurrentWave = 0;

        waveTimer.StartTimer();

        StartCoroutine(EnemySpawning());

        GameController.Instance.AudioController.Play("WaveStart");

        OnWaveStart.Invoke();
    }

    public void FinishWave()
    {
        if (allEnemiesSpawned == true)
        {
            if (Waves.Count <= currentWaveId + 1) //Finished all waves
            {
                isWaveRunning = false;
                GameController.Instance.StopGame(true);
            }
            else //Finish this wave, start next
            {
                DisableWave();

                OnWaveFinish.Invoke();

                //StartNextWave();
            }
        }
    }
    private void DisableWave()
    {
        isWaveRunning = false;

        waveTimer.StopTimer();

        CurrentWave.Finished = true;
        CurrentWave.Time = waveTimer.GetTime();

        GameController.Instance.AudioController.Play("WaveFinish");
    }
    
    public IEnumerator WaveCooldown(byte waveIdToLaunch, float time)
    {
        cooldownTimer.StartTimer(time);

        yield return new WaitForSeconds(time);

        cooldownTimer.StopTimer();

        StartWave(waveIdToLaunch);
    }

    private IEnumerator EnemySpawning()
    {
        //Highest amount of enemeies under single type
        float maxEnemyAmountToSpawn = CurrentWave.Enemies.dictionary.Values.Max();

        //Assign default values
        Dictionary<EnemySO, int> waitCyclesToSpawn = new();
        Dictionary<EnemySO, int> enemiesSpawned = new();

        foreach (EnemySO enemy in CurrentWave.Enemies.dictionary.Keys)
        {
            //wait spawn cycles before spawning this type of enemy
            float enemyAmountToSpawn = CurrentWave.Enemies.dictionary[enemy];

            float spawnEvery = maxEnemyAmountToSpawn / enemyAmountToSpawn;

            waitCyclesToSpawn.Add(enemy, (int)spawnEvery);

            //Enemies of type spawned counter
            enemiesSpawned.Add(enemy, 0);
        }

        //Cycle through enemies and spawn them
        while (enemiesSpawnedInCurrentWave < CurrentWave.TotalEnemyCount)
        {
            while (enemySpawner.TotalCount < maxEnemiesOnScene)
            {
                foreach (EnemySO enemy in CurrentWave.Enemies.dictionary.Keys)
                {
                    int enemyAmountToSpawn = CurrentWave.Enemies.dictionary[enemy];

                    //if there should be more enemies of this type spawned
                    if(enemiesSpawned[enemy] < enemyAmountToSpawn)
                    {
                        waitCyclesToSpawn[enemy]--;

                        if (waitCyclesToSpawn[enemy] == 0)
                        {
                            //Spawn enemy
                            enemySpawner.SpawnEnemy(enemy);

                            enemiesSpawned[enemy]++;
                            enemiesSpawnedInCurrentWave++;


                            //Reset waitCycles value
                            float spawnEvery = maxEnemyAmountToSpawn / enemyAmountToSpawn;
                            waitCyclesToSpawn[enemy] = (int)spawnEvery;
                        }
                    }

                    //All enemies got spawned
                    if (enemiesSpawnedInCurrentWave >= CurrentWave.TotalEnemyCount)
                    {
                        allEnemiesSpawned = true;
                        yield break;
                    }
                }

                yield return new WaitForSeconds(cooldownBetweenEnemySpawn);
            }

            yield return new WaitUntil(() => enemySpawner.TotalCount < maxEnemiesOnScene * 0.5f);
        }
    }


    private void OnGUI()
    {
        if (GameController.Instance.ShowDebug)
        {
            GUI.color = Color.black;

            GUILayout.BeginArea(new Rect(0, Screen.height - 250f, 250f, 250));

            GUILayout.Label($"CurrentWaveId: {currentWaveId}");
            GUILayout.Label($"IsWaveRunning: {isWaveRunning}");

            GUILayout.Label($"WaveTimer: {TimeConverter.ConvertTimeStripped(waveTimer.GetTime())}");
            GUILayout.Label($"cooldownTimer: {TimeConverter.ConvertTimeStripped(cooldownTimer.GetTime())}");

            GUILayout.Label($"maxEnemiesOnScene: {maxEnemiesOnScene}");
            GUILayout.Label($"AliveEnemiesCount: {enemySpawner.TotalCount}");
            GUILayout.Label($"EnemiesSpawnedInCurrentWave: {enemiesSpawnedInCurrentWave}");
            GUILayout.Label($"CurrentWave.TotalEnemyCount: {CurrentWave.TotalEnemyCount}");
            GUILayout.Label($"cooldownBetweenEnemySpawn: {cooldownBetweenEnemySpawn}");
            GUILayout.Label($"AllEnemiesSpawned: {allEnemiesSpawned}");

            GUILayout.EndArea();
        }
    }
}
