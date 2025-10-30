using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 보스 공격 패턴 시스템 스크립트 (보스 오브젝트에 부착)
/// - Inspector에서 정의된 패턴 목록을 기반으로 보스가 자동으로 공격 패턴을 실행한다.
/// - 일정 체력 이하로 떨어지면 고정 패턴 모드로 전환하여 정해진 공격들을 순서대로 실행한다.
/// - 보스 사망 시 모든 패턴 동작을 즉시 중지한다.
/// </summary>
public class Pattern : MonoBehaviour
{
    /// <summary>
    /// 개별 보스 공격 패턴 구조체 정의
    /// </summary>
    [Serializable]
    public struct BossPattern
    {
        [Tooltip("패턴 발동 최소 보스 체력 (%)")]
        public int minHpPercent;
        [Tooltip("패턴 발동 최대 보스 체력 (%)")]
        public int maxHpPercent;
        [Tooltip("공격 프리팹 (예: 투사체)")]
        public GameObject prefab;
        [Tooltip("프리팹 생성 위치 (보스 기준 Transform)")]
        public Transform spawnPoint;
        [Tooltip("사용 후 보스 이동 목표 지점 (없으면 이동 없음)")]
        public Transform moveTarget;
        [Tooltip("이동 방식: False=천천히 이동, True=순간이동")]
        public bool teleport;
        [Tooltip("다음 패턴 딜레이 최소 시간 (초)")]
        public float delayMin;
        [Tooltip("다음 패턴 딜레이 최대 시간 (초)")]
        public float delayMax;
        [Tooltip("패턴 실행 시 재생할 사운드")]
        public AudioClip sfx;
        [Tooltip("패턴 사운드 재생 볼륨")]
        [Range(0f, 1f)] public float sfxVolume;
    }

    [Header("패턴 목록 (랜덤 패턴들)")]
    public BossPattern[] randomPatterns;   // 일반 패턴 리스트 (무작위 선택용)

    [Header("고정 패턴 설정")]
    [Tooltip("고정 패턴 발동 체력 임계값 (%)")]
    public int fixedPatternTriggerPercent; // 이 체력 이하로 내려가면 고정 패턴 모드로 전환
    [Tooltip("고정 패턴 목록 (순서대로 실행)")]
    public BossPattern[] fixedPatterns;    // 고정 패턴 리스트
    [Tooltip("고정 패턴 목록 반복 횟수")]
    public int fixedPatternRepeatCount = 1; // 고정 패턴 전체를 몇 번 반복 실행할지

    private BossBase _bossBase;    // 보스의 Base (체력 및 이벤트 관리용)
    private bool _fixedTriggered;  // 고정 패턴 모드 진입 여부 플래그

    void Start()
    {
        // 보스 베이스 컴포넌트 가져오기
        _bossBase = GetComponent<BossBase>();
        if (_bossBase == null)
        {
            Debug.LogWarning("Pattern: BossBase를 찾을 수 없습니다. 패턴 동작이 정상적으로 이루어지지 않을 수 있습니다.");
        }
        else
        {
            // 보스 사망 이벤트에 메소드 등록
            _bossBase.OnBossDie += OnBossDie;
        }

        // 패턴 실행 코루틴 시작
        StartCoroutine(PatternRoutine());
    }

    void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (_bossBase != null)
        {
            _bossBase.OnBossDie -= OnBossDie;
        }
    }

    /// <summary>
    /// 보스 패턴 자동 실행 루프 코루틴
    /// </summary>
    private IEnumerator PatternRoutine()
    {
        // 메인 루프: 보스가 살아있는 동안 패턴을 반복 실행
        while (true)
        {
            // 보스가 없거나 사망한 경우 루프 종료
            if (_bossBase != null)
            {
                if (_bossBase.IsDead)
                    yield break;
            }

            // 현재 보스 체력 퍼센트 계산
            float currentHpPercent = 100f;
            if (_bossBase != null && _bossBase.GetMaxHP() > 0)
            {
                currentHpPercent = (_bossBase.GetCurrentHP() / (float)_bossBase.GetMaxHP()) * 100f;
            }

            // 고정 패턴 모드 진입 조건 확인
            if (!_fixedTriggered && fixedPatterns != null && fixedPatterns.Length > 0
                && currentHpPercent <= fixedPatternTriggerPercent)
            {
                // 고정 패턴 모드 시작
                _fixedTriggered = true;
                // 고정 패턴 시퀀스 실행 (설정된 횟수만큼 반복)
                yield return StartCoroutine(ExecuteFixedPatterns());
                // 고정 패턴 완료 후 랜덤 패턴 루프로 복귀 (계속 진행)
                // (고정 패턴은 한 번만 실행; _fixedTriggered 플래그로 재진입 방지)
            }

            // 랜덤 패턴 선택 및 실행
            BossPattern? chosenPattern = ChooseRandomPattern(currentHpPercent);
            if (chosenPattern == null)
            {
                // 현재 체력에 사용할 수 있는 패턴이 없다면 루프 종료
                yield break;
            }

            // 패턴 실행 (공격 생성, 이동, 사운드 등)
            yield return StartCoroutine(ExecutePattern(chosenPattern.Value));
            // 패턴 실행 후, 해당 패턴의 딜레이만큼 대기 (코루틴 내부에서 처리됨)

            // 루프 반복 (다음 패턴 실행)
        }
    }

    /// <summary>
    /// 조건에 맞는 랜덤 패턴 하나를 선택한다.
    /// </summary>
    /// <param name="currentHpPercent">현재 보스 체력 (%)</param>
    /// <returns>선택된 BossPattern (없으면 null)</returns>
    private BossPattern? ChooseRandomPattern(float currentHpPercent)
    {
        if (randomPatterns == null || randomPatterns.Length == 0)
            return null;
        // 현재 체력 조건을 만족하는 패턴들 필터링
        var candidates = new System.Collections.Generic.List<BossPattern>();
        foreach (BossPattern pattern in randomPatterns)
        {
            if (currentHpPercent >= pattern.minHpPercent && currentHpPercent <= pattern.maxHpPercent)
            {
                candidates.Add(pattern);
            }
        }
        if (candidates.Count == 0)
        {
            // 조건에 맞는 패턴이 없으면 null 반환
            return null;
        }
        // 후보 중 무작위 하나 선택
        int index = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[index];
    }

    /// <summary>
    /// 지정된 패턴을 실행한다 (프리팹 생성, 보스 이동, 사운드 재생 등 포함).
    /// </summary>
    /// <param name="pattern">실행할 BossPattern</param>
    private IEnumerator ExecutePattern(BossPattern pattern)
    {
        // 1. 공격 프리팹 생성
        if (pattern.prefab != null)
        {
            Vector3 spawnPos;
            Quaternion spawnRot;
            if (pattern.spawnPoint != null)
            {
                spawnPos = pattern.spawnPoint.position;
                spawnRot = pattern.spawnPoint.rotation;
            }
            else
            {
                spawnPos = transform.position;
                spawnRot = Quaternion.identity;
            }
            Instantiate(pattern.prefab, spawnPos, spawnRot);
        }

        // 2. 사운드 재생
        if (pattern.sfx != null)
        {
            if (_bossBase != null && _bossBase.audioSrc != null)
            {
                // 보스 자체 AudioSource로 재생
                _bossBase.audioSrc.PlayOneShot(pattern.sfx, pattern.sfxVolume);
            }
            else
            {
                // AudioSource가 없으면 해당 위치에서 1회성 소리 재생
                AudioSource.PlayClipAtPoint(pattern.sfx, transform.position, pattern.sfxVolume);
            }
        }

        // 3. 보스 이동 (필요 시)
        if (pattern.moveTarget != null)
        {
            if (pattern.teleport)
            {
                // 순간이동: 즉시 위치 변경
                if (_bossBase != null && _bossBase.actor != null)
                {
                    _bossBase.actor.position = pattern.moveTarget.position;
                }
                else
                {
                    // 액터 없으면 현재 오브젝트 이동
                    transform.position = pattern.moveTarget.position;
                }
            }
            else
            {
                // 천천히 이동: 일정 시간 동안 보스를 moveTarget까지 이동시킴
                float moveDuration = 1.0f; // 이동에 걸리는 시간 (초) - 필요에 따라 조정 가능
                float elapsed = 0f;
                Vector3 startPos = (_bossBase != null && _bossBase.actor != null)
                                    ? _bossBase.actor.position
                                    : transform.position;
                Vector3 endPos = pattern.moveTarget.position;
                // 선형 보간을 사용하여 moveDuration 동안 이동
                while (elapsed < moveDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / moveDuration);
                    Vector3 newPos = Vector3.Lerp(startPos, endPos, t);
                    if (_bossBase != null && _bossBase.actor != null)
                        _bossBase.actor.position = newPos;
                    else
                        transform.position = newPos;
                    yield return null; // 다음 프레임까지 대기
                }
                // 마지막에 정확히 목표 지점으로 위치 설정
                if (_bossBase != null && _bossBase.actor != null)
                    _bossBase.actor.position = endPos;
                else
                    transform.position = endPos;
            }
        }

        // 4. 다음 패턴 전까지 딜레이 대기
        float waitTime = UnityEngine.Random.Range(pattern.delayMin, pattern.delayMax);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
        // 패턴 실행 코루틴 끝 (다음 패턴으로 이어짐)
    }

    /// <summary>
    /// 고정 패턴 시퀀스를 실행하는 코루틴 (순차적으로 실행).
    /// </summary>
    private IEnumerator ExecuteFixedPatterns()
    {
        if (fixedPatterns == null || fixedPatterns.Length == 0)
            yield break;
        // 정해진 횟수만큼 전체 시퀀스 반복
        for (int cycle = 0; cycle < fixedPatternRepeatCount; cycle++)
        {
            foreach (BossPattern pattern in fixedPatterns)
            {
                // 보스 사망 시 즉시 중지
                if (_bossBase != null && _bossBase.IsDead)
                    yield break;
                // 개별 패턴 실행 (고정 패턴도 BossPattern 구조체 재사용)
                yield return StartCoroutine(ExecutePattern(pattern));
            }
        }
        // 고정 패턴 시퀀스 완료 후 반환 (random 패턴으로 복귀)
    }

    /// <summary>
    /// 보스 사망 시 호출되는 이벤트 핸들러 (OnBossDie 이벤트 연결).
    /// 모든 패턴 동작을 중지한다.
    /// </summary>
    /// <param name="boss">사망한 BossBase 객체 (사용 안 함)</param>
    private void OnBossDie(BossBase boss)
    {
        // 모든 코루틴 정지하여 패턴 실행 중단
        StopAllCoroutines();
        // 필요한 경우 이 시점에 추가 처리 가능 (예: 오디오 정지 등)
    }
}
