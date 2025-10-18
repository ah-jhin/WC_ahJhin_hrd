using UnityEngine;

/// <summary>
/// 무기 관리(입력/교체/발사 쿨타임만 담당).
/// - 발사 간격은 활성 무기의 IWeaponInfo.FireInterval을 사용.
/// - Z 또는 Fire1을 누르고 있으면 자동 연사.
/// - 무기 탄약 0이 되면 자동 제거 후 기본무기로 복귀.
/// </summary>
public class WP_Manager : MonoBehaviour
{
    [Header("무기 슬롯 (0=권총, 1~2=보급)")]
    public GameObject[] weaponSlots = new GameObject[3]; // 슬롯 컨테이너(무기 오브젝트)

    [Header("입력")]
    public KeyCode nextKey = KeyCode.A;          // 무기 순환
    public KeyCode shootKey = KeyCode.Z;         // 발사
    public UIHUD hud;                             // HUD 참조(선택)

    [Header("권총 프리팹")]
    public GameObject pistolPrefab;              // 시작 시 0번 비면 스폰

    int _cur = 0;                                // 현재 슬롯
    float _nextFireTime;                         // 다음 발사 가능 시각

    void Start()
    {
        // 0번 슬롯 보장
        if (weaponSlots[0] == null && pistolPrefab != null)
        {
            weaponSlots[0] = Instantiate(pistolPrefab, transform);
            weaponSlots[0].name = "Pistol(runtime)";
        }
        ActivateCurrent(); // 0번 활성화
        _nextFireTime = Time.time;
    }

    void Update()
    {
        HandleSwapInput();
        HandleFireInput(); // 자동연사
    }

    void HandleSwapInput()
    {
        if (Input.GetKeyDown(nextKey)) SwapNext();
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwapTo(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwapTo(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwapTo(2);
    }

    void HandleFireInput()
    {
        bool firePressed = Input.GetButton("Fire1") || Input.GetKey(shootKey);
        if (!firePressed) return;

        var go = GetActiveWeapon(); if (!go) return;

        // 무기별 발사 간격 사용
        var wi = go.GetComponent<IWeaponInfo>();
        float interval = wi != null ? Mathf.Max(0.01f, wi.FireInterval) : 0.2f;
        if (Time.time < _nextFireTime) return;

        go.SendMessage("Shoot", SendMessageOptions.DontRequireReceiver);
        _nextFireTime = Time.time + interval;

        // HUD 탄약 갱신(구현 시)
        if (hud && wi != null) hud.SetWeapon(wi.Icon, wi.DisplayName, wi.Ammo, wi.IsInfinite);

        // 비무한 + 탄약 0이면 제거
        if (wi != null && !wi.IsInfinite && wi.Ammo <= 0)
            OnWeaponEmpty(go.GetComponent<WP_Pistol>()); // 해당 무기 타입 전달(없으면 무시)
    }

    // 무기 비었을 때 호출됨(무기가 SendMessageUpwards로 부름)
    void OnWeaponEmpty(object _)
    {
        var go = GetActiveWeapon(); if (!go) return;

        // 0번이면 남겨두고, 보급(1~2)이면 제거 후 0번 복귀
        if (_cur > 0)
        {
            Destroy(go);
            weaponSlots[_cur] = null;
            _cur = 0;
            ActivateCurrent();
        }
    }
    // 보급 상자 등이 호출: 무기 프리팹을 슬롯(1~2)에 장착
    public bool AddWeapon(GameObject weaponPrefab, bool select = true)
    {
        if (!weaponPrefab) return false;

        // 1) 빈 슬롯 찾기(0은 기본 무기이므로 1부터)
        int slot = -1;
        for (int i = 1; i < weaponSlots.Length; i++)
            if (weaponSlots[i] == null) { slot = i; break; }
        if (slot == -1) return false; // 빈 슬롯 없음

        // 2) 인스턴스 생성 후 장착
        var go = Instantiate(weaponPrefab, transform);
        weaponSlots[slot] = go;

        // 3) 선택 여부
        if (select) { _cur = slot; ActivateCurrent(); }
        else        { go.SetActive(false); }

        return true;
    }

    public void SwapNext()
    {
        int start = _cur;
        do { _cur = (_cur + 1) % weaponSlots.Length; }
        while (weaponSlots[_cur] == null && _cur != start);
        ActivateCurrent();
    }

    public void SwapTo(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;
        if (weaponSlots[index] == null) return;
        _cur = index; ActivateCurrent();
    }

    void ActivateCurrent()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
            if (weaponSlots[i] != null) weaponSlots[i].SetActive(i == _cur);

        // HUD 갱신
        var go = GetActiveWeapon();
        var wi = go ? go.GetComponent<IWeaponInfo>() : null;
        if (hud && wi != null) hud.SetWeapon(wi.Icon, wi.DisplayName, wi.Ammo, wi.IsInfinite);

        // 즉시 발사 가능
        _nextFireTime = Time.time;
    }

    GameObject GetActiveWeapon()
    {
        if (_cur < 0 || _cur >= weaponSlots.Length) return null;
        return weaponSlots[_cur];
    }
}
