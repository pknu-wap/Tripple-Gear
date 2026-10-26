using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways] // 에디터 모드(비실행 상태)에서도 Inspector 변경을 즉시 반영
public class UIBarController : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private Image barFillImage;

    [Header("Bar Settings")]
    [Range(0f, 100f)]
    [SerializeField] private float currentValue = 100f;
    private const float MaxValue = 100f;

    private void OnValidate()
    {
        UpdateBar();
    }

    private void UpdateBar()
    {
        if (barFillImage != null)
        {
            // 0~100 사이 값으로 제한 후 0.0~1.0 비율로 변환
            currentValue = Mathf.Clamp(currentValue, 0f, MaxValue);
            barFillImage.fillAmount = currentValue / MaxValue;
        }
    }

    // 외부 스크립트에서 값을 변경할 때 사용할 수 있는 함수
    public void SetValue(float newValue)
    {
        currentValue = newValue;
        UpdateBar();
    }
}
