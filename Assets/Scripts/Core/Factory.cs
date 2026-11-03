using UnityEngine;

public enum PoolObjectType
{
    WoodChip = 0,
    // 새 종류는 여기에 추가
}

public class Factory : Singleton<Factory>
{
    // 풀이 씬의 UI 안에 있으므로 씬이 바뀌면 같이 사라지게 한다.
    // (여러 씬을 오가며 써야 한다면 이 줄을 지우고 아래 참고를 보세요)
    protected override bool IsPersistent => false;

    // 오브젝트 풀들
    WoodChip_Pool woodChip;

    protected override void OnInitialize()
    {
        base.OnInitialize();

        woodChip = FindPool<WoodChip_Pool>();
        if (woodChip != null) woodChip.Initialize();

        // 새 풀은 위 두 줄처럼 여기에 추가
    }

    /// <summary>
    /// 풀 컴포넌트 찾기. Factory 자식(일반 오브젝트)을 먼저 보고, 없으면 씬 전체(UI 등)에서 찾는다.
    /// </summary>
    private T FindPool<T>() where T : Component
    {
        T pool = GetComponentInChildren<T>(true);
        if (pool == null)
            pool = FindAnyObjectByType<T>(FindObjectsInactive.Include);

        if (pool == null)
            Debug.LogWarning($"{typeof(T).Name} 을 찾을 수 없습니다.");

        return pool;
    }

    /// <summary>
    /// 종류로 오브젝트를 꺼내는 함수
    /// </summary>
    public GameObject GetObject(PoolObjectType type)
    {
        GameObject result = null;
        switch (type)
        {
            case PoolObjectType.WoodChip:
                result = GetWoodChip()?.gameObject;
                break;
                // 새 종류는 여기에 case 추가
        }
        return result;
    }

    // 우드칩 --------------------------------------------------------------

    public WoodChip GetWoodChip()
    {
        return woodChip != null ? woodChip.GetObject() : null;
    }
}