// Assets/Boss/BossSequenceController.cs
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class BossEntry
{
    public GameObject prefab;
    public int overrideMaxHP = 0;
    public string displayName;
    public AudioClip bgm;
}

public class BossSequenceController : MonoBehaviour
{
    [Header("Sequence")]
    public BossEntry[] bosses;     // 0=���콺, 1=�����̵���
    public Transform spawnPoint;

    [Header("Shared UI (reuse)")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public TMP_Text nameText;

    [Header("Stage Bindings (scene refs)")]
    public Transform[] stageSkyPoints;   // SkyPoints(�� ������Ʈ��)
    public Transform stageBeamSpawnL;    // ���� �� ����
    public Transform stageBeamSpawnR;    // ������ �� ����
    public Transform stageFirePointOverride; // (����) firePoint�� ������ ������ ����

    [Header("Transition")]
    public float spawnDelay = 1.2f;
    public AudioSource sfx;
    public AudioClip bossChangeSfx;
    public AudioSource music;
    public Animator screenFx;

    int _index = -1;
    BossBase _current;

    void Start() => SpawnNext();
    void OnDestroy() => Unhook(_current);

    public void SpawnNext() => StartCoroutine(SpawnNextRoutine());

    IEnumerator SpawnNextRoutine()
    {
        if (_index >= 0)
        {
            if (sfx && bossChangeSfx) sfx.PlayOneShot(bossChangeSfx);
            if (screenFx) screenFx.SetTrigger("BossChange");
            yield return new WaitForSeconds(spawnDelay);
        }

        _index++;
        if (_index >= bosses.Length) { Debug.Log("[BossSeq] All bosses defeated!"); yield break; }

        var entry = bosses[_index];
        var go = Instantiate(entry.prefab, spawnPoint ? spawnPoint.position : Vector3.zero, Quaternion.identity);
        var boss = go.GetComponent<BossBase>();

        // UI ������
        boss.hpSlider = hpSlider;
        boss.hpText = hpText;

        // HP �������̵�(Init ���� ����)
        if (entry.overrideMaxHP > 0) boss.maxHP = entry.overrideMaxHP;

        Hook(boss);

        // �������� ���� �� ���� (���콺/�����̵� �������� BossStage1�� �Ⱦ���)
        var s1 = boss.GetComponent<BossStage1>();
        if (s1)
        {
            if (stageFirePointOverride) s1.firePoint = stageFirePointOverride;
            if (stageSkyPoints != null && stageSkyPoints.Length > 0) s1.skyPoints = stageSkyPoints;
            if (stageBeamSpawnL) s1.beamSpawnL = stageBeamSpawnL;
            if (stageBeamSpawnR) s1.beamSpawnR = stageBeamSpawnR;
        }

        // ǥ��/����
        if (nameText) nameText.text = string.IsNullOrEmpty(entry.displayName) ? go.name : entry.displayName;
        if (music && entry.bgm) { music.clip = entry.bgm; music.Play(); }
    }

    void Hook(BossBase b)
    {
        Unhook(_current);
        _current = b;
        if (_current == null) return;
        _current.OnBossDie += HandleBossDie;
        _current.OnHpChanged += HandleHpChanged;
        HandleHpChanged(_current.GetCurrentHP(), _current.maxHP);
    }

    void Unhook(BossBase b)
    {
        if (b == null) return;
        b.OnBossDie -= HandleBossDie;
        b.OnHpChanged -= HandleHpChanged;
    }

    void HandleBossDie(BossBase dead) => SpawnNext();

    void HandleHpChanged(int cur, int max)
    {
        if (hpSlider) { hpSlider.maxValue = max; hpSlider.value = cur; }
        if (hpText) { hpText.text = $"{cur} / {max}"; }
    }
}
