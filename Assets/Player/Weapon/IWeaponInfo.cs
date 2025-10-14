using UnityEngine;

/// <summary>
/// HUD에 무기 정보를 전달하기 위한 최소 인터페이스
/// </summary>
public interface IWeaponInfo
{
    string DisplayName { get; }   // HUD에 표시될 무기 이름
    Sprite Icon { get; }          // 무기 아이콘(없으면 null)
    int Ammo { get; }             // 남은 탄(무한이면 0 권장)
    bool IsInfinite { get; }      // 무한 탄 여부
}
